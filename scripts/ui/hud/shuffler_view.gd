class_name ShufflerView
extends Control
## The shuffler beside the hand (owner: like Clash Royale's next card): a small card window that shows the NEXT card. When a card is used it
## drops into the hand and the used card comes here: the window shows it dimmed with its cooldown running (a ring and the seconds left);
## when that is over, the card goes to the back of the deck and the window shows the new next card with a little flash.
## It only draws; it never takes a touch.

const FLASH_TIME: float = 0.4
## The new NEXT card swells this much (a fraction) as it arrives.
const POP: float = 0.16
const LABEL_FONT: int = UiMetrics.CAPTION
const NEXT_COLOR: Color = Color(0.45, 0.72, 1.0)

var next_def: WeaponDef
var shuffle_def: WeaponDef
var shuffle_left: float = 0.0
var shuffle_total: float = 0.0
var _flash: float = 0.0


func _init() -> void:
	name = "Shuffler"
	mouse_filter = Control.MOUSE_FILTER_IGNORE


## Reads the local player's shuffler (call every frame). Returns nothing; redraws only when something changed.
func show_state(sim: MatchSim, player: PlayerState) -> void:
	var next_now: WeaponDef = sim.weapon_defs.get(player.next_card) as WeaponDef
	var shuffle_now: WeaponDef = sim.weapon_defs.get(player.shuffle_card) as WeaponDef
	var left_now: float = snappedf(player.shuffle_left, 0.1)
	if next_now == next_def and shuffle_now == shuffle_def and is_equal_approx(left_now, shuffle_left):
		return
	if next_now != null and next_now != next_def and not Settings.reduced_motion:
		_flash = FLASH_TIME
		set_process(true)
	next_def = next_now
	shuffle_def = shuffle_now
	shuffle_left = left_now
	shuffle_total = player.shuffle_total
	visible = player.cards
	queue_redraw()


func _process(delta: float) -> void:
	_flash = maxf(0.0, _flash - delta)
	if _flash <= 0.0:
		set_process(false)
	queue_redraw()


## The window's card, in this view's own coordinates.
func card_rect() -> Rect2:
	return Rect2(Vector2(0.0, LABEL_FONT + 8.0), Vector2(size.x, size.y - LABEL_FONT - 8.0))


func _draw() -> void:
	var font: Font = KalyeahTheme.ACTION_FONT
	var label: String = UiText.t("NEXT")
	var label_size: Vector2 = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, LABEL_FONT)
	draw_string(font, Vector2((size.x - label_size.x) / 2.0, LABEL_FONT), label, HORIZONTAL_ALIGNMENT_LEFT, -1.0, LABEL_FONT, NEXT_COLOR)
	var card: Rect2 = card_rect()
	if shuffle_def != null:
		# the used card fills with colour from the bottom as its cooldown runs down: bright means it is about to come back round
		var centre: Vector2 = card.get_center()
		CardStyle.draw_filling(self, card, shuffle_def, 1.0 - clampf(shuffle_left / maxf(shuffle_total, 0.001), 0.0, 1.0))
		var seconds: String = "%d" % ceili(shuffle_left)
		var seconds_size: Vector2 = font.get_string_size(seconds, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE)
		draw_string_outline(font, centre + Vector2(-seconds_size.x / 2.0, 12.0), seconds, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, UiSkin.TEXT_LINE, UiSkin.LINE_COLOR)
		draw_string(font, centre + Vector2(-seconds_size.x / 2.0, 12.0), seconds, HORIZONTAL_ALIGNMENT_LEFT, -1.0, UiMetrics.TITLE, Color.WHITE)
	elif next_def != null:
		var pop: float = 1.0
		if _flash > 0.0:
			pop += POP * sin((1.0 - _flash / FLASH_TIME) * PI)
			draw_set_transform(card.get_center() * (1.0 - pop), 0.0, Vector2(pop, pop))
		CardStyle.draw(self, card, next_def)
		if _flash > 0.0:
			draw_rect(card, Color(1.0, 0.9, 0.4, 0.45 * _flash / FLASH_TIME), true)
			draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	else:
		CardStyle.draw_empty(self, card)
