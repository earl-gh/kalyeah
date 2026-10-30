class_name ClockStageFlash
extends Control
## The word that announces a new clock stage (owner): when Regular ends, "Extended" flashes big in the middle of the screen, then
## shrinks and glides to the top right clock, where it becomes the caption in place of "Time left:"; when that minute ends, the same
## with "Overtime". Only a stage that changes while it is watched flashes (not one already under way when you join or reconnect, not
## the restart after a point), and not with Reduce motion (the caption just changes). It only draws and never takes a touch. Its time
## is the sim's: the HUD feeds it `0` while the match stands still, so the flash waits with it.

## The word at the same size as GO! and the other big words.
const FONT: int = UiMetrics.HERO
## The pause in the middle (the word pops in over its first `POP_TIME`), then the flight to the caption.
const HOLD_TIME: float = 0.7
const FLY_TIME: float = 0.75
const POP_FROM: float = 1.4
const POP_TIME: float = 0.25
## Where the word flashes: across the screen's width, this share of its height down (like GO!).
const CENTER_Y: float = 0.3
## The in-out ease of the flight (Godot's `ease()` curve: negative = slow at both ends).
const EASE_CURVE: float = -2.0
## A stage that began longer ago than this when it is first seen does not flash (a client that joined or reconnected late).
const FRESH_WINDOW: float = 2.0
## How small the word ends: the caption's font over the big one.
const END_SCALE: float = float(ClockChip.CAPTION_FONT) / float(FONT)

var _word: Label
var _seen: int = -1
var _time: float = 0.0
var _flying: bool = false
var _landing_color: Color = Color.WHITE


func _init() -> void:
	name = "ClockStageFlash"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_word = Label.new()
	_word.mouse_filter = Control.MOUSE_FILTER_IGNORE
	UiKit.letter(_word, FONT, RoyaleStyle.GOLD)
	_word.visible = false
	add_child(_word)


## True while the word is on its way (the clock's caption waits empty for it).
func flying() -> bool:
	return _flying


## The word on screen, or "".
func word_text() -> String:
	return _word.text if _word.visible else ""


## Every frame: the clock's stage, how long ago it began (round seconds), the time that passed in the match (0 while it stands still),
## and where the caption is (this control's coordinates) and the colour it ends in. A stage that rises to Extended or Overtime
## while watched starts the flash.
func sync(stage: MatchSim.ClockStage, into_stage: float, delta: float, caption_at: Vector2, caption_color: Color) -> void:
	if _seen < 0:
		_seen = stage
	elif stage != _seen:
		var rising: bool = stage > _seen
		_seen = stage
		cancel()
		if rising and into_stage <= FRESH_WINDOW and not Settings.reduced_motion:
			_begin(stage, caption_color)
	if not _flying:
		return
	if Settings.reduced_motion:
		cancel()
		return
	_time += delta
	_landing_color = caption_color
	if _time >= HOLD_TIME + FLY_TIME:
		cancel()
		return
	_draw_word(caption_at)


## Stops the flash at once (a point was scored, the stage restarted, or the match is over).
func cancel() -> void:
	_flying = false
	_word.visible = false


func _begin(stage: MatchSim.ClockStage, caption_color: Color) -> void:
	_word.text = UiText.t("Extended" if stage == MatchSim.ClockStage.EXTENDED else "Overtime")
	_word.reset_size()
	_word.pivot_offset = _word.size / 2.0
	_time = 0.0
	_flying = true
	_landing_color = caption_color
	_word.visible = true


## The word at `_time`: popping in at the middle, held, then easing down in size and colour to the caption.
func _draw_word(caption_at: Vector2) -> void:
	var middle: Vector2 = Vector2(size.x * 0.5, size.y * CENTER_Y)
	var at: Vector2 = middle
	var word_scale: float = 1.0
	var color: Color = RoyaleStyle.GOLD
	var alpha: float = 1.0
	if _time < HOLD_TIME:
		var pop: float = clampf(_time / POP_TIME, 0.0, 1.0)
		word_scale = lerpf(POP_FROM, 1.0, ease(pop, 0.5))
		alpha = clampf(_time / (POP_TIME * 0.5), 0.0, 1.0)
	else:
		var along: float = ease(clampf((_time - HOLD_TIME) / FLY_TIME, 0.0, 1.0), EASE_CURVE)
		at = middle.lerp(caption_at, along)
		word_scale = lerpf(1.0, END_SCALE, along)
		color = RoyaleStyle.GOLD.lerp(_landing_color, along)
	_word.position = at - _word.size / 2.0
	_word.scale = Vector2.ONE * word_scale
	_word.modulate.a = alpha
	_word.add_theme_color_override("font_color", color)
