class_name MatchHud
extends Control
## The whole in-match HUD, built in code (no scene text to patch): overhead bars,
## top row (the settings button on the left, the score on the right; the whole street is
## always on screen, so there is no minimap), ## the capture countdown, the settings menu, and the controls (docs/GDD.md "Controls: tap to move, cards to cast"):
## the hand at the bottom (NEXT, four cards, the elixir bar and your health) within thumb reach, taps on the street to
## walk (StreetTouch) and the ring where you are walking to (MoveMarker).
## The match screen feeds it sim state through `sync_player()` and `sync_score()`.

signal swap_pressed
signal again_pressed
## A card in the tagged panel was tapped: follow player `id`.
signal exit_pressed
## The tray's height changed (the bottom inset did): the camera keeps this much of the screen clear.
signal console_height_changed(height: float)
## The panel along one side of the screen (the cards of Cards tray = Sides) is this wide (0: none), on the left or not: the camera frames the street in the rest.
signal side_reserve_changed(width: float, left: bool)
## The header's height (with the top inset) changed: the camera keeps this much of the screen clear at the top.
signal header_height_changed(height: float)

const BANNER_TIME: float = 2.0
## GO!, SCORED and VICTORY / DEFEAT on the street (visual pass: were 44 px with a 12 px outline): big gold words in a thick navy
## outline with a drop shadow, lettered at `UiMetrics.HERO` like every big word (`UiKit.letter`); a long one is set smaller to keep `BANNER_MARGIN` from the screen's sides. They pop in from
## `BANNER_POP_FROM` times their size over `BANNER_POP_TIME` (not with Reduce motion).
const BANNER_FONT: int = UiMetrics.HERO
## The lettered outline at that size (`UiSkin.letter_outline`), kept clear of the screen's sides.
const BANNER_OUTLINE: int = roundi(BANNER_FONT * UiSkin.LETTER_OUTLINE_SHARE)
const BANNER_MARGIN: float = 40.0
const BANNER_POP_FROM: float = 1.5
const BANNER_POP_TIME: float = 0.28
## The short word on the hand's cards for each status that stops casting.
const CAST_BLOCK_TAGS: Dictionary[int, String] = {
	StatusEffects.Type.STUN: "STUNNED",
	StatusEffects.Type.AIRBORNE: "AIRBORNE",
	StatusEffects.Type.BOUNCE: "BOUNCED",
	StatusEffects.Type.KNOCKOUT: "OUT",
	StatusEffects.Type.POLYMORPH: "CAN",
}
## What the cards say while you stand where nothing can be cast: in the enemy base, or in the rescue area with a teammate waiting.
const CAPTURING_TAG: String = "CAPTURING"
const RESCUING_TAG: String = "RESCUING"
## Let go of a card with nobody to throw at in range: nothing is thrown, the card stays, and it says why for this long.
const NO_TARGET_TAG: String = "NO TARGET"
const NO_TARGET_MS: int = 1200
## The header at the top of the screen (floating over the street, like Clash Royale): the settings button, the score and the clock sit in this row.
const TOP_ROW_Y: float = 12.0
const HEADER_HEIGHT: float = TOP_ROW_Y * 2.0 + 88.0
const MARGIN: float = 14.0
const GEAR_SIZE: float = UiMetrics.TOUCH
const SCORE_SIZE: Vector2 = Vector2(330.0, 88.0)
const CLOCK_SIZE: Vector2 = Vector2(176.0, 112.0)
const CAPTURE_COLOR: Color = Color(1.0, 0.95, 0.85)
## How long the capture words and the warning stay up when the capture stops for a moment (seconds).
const CAPTURE_LINGER: float = 0.4
## The tray at the bottom of the screen (the hand and the elixir bar): the arena sits above it. The camera reserves the same height.
const CONSOLE_HEIGHT: float = 255.0
## The tray of Tumbang Preso and Patintero (no hand: the role line and the Throw button) is this tall, plus the bottom inset.
const STREET_TRAY: float = 210.0
## Tumbang Preso's score column stands on the left sidewalk this far under the header, and the quick chat button under the clock is this big.
const SCORE_COLUMN_GAP: float = 14.0
const STREET_CHAT_SIZE: float = 84.0
## The street games' quick chat opens a vertical list this wide.
const STREET_CHAT_WIDTH: float = 250.0
## The most the bottom inset can add (a tall gesture bar), so the tray never eats the street.
const MAX_BOTTOM_INSET: float = 80.0

var overhead: OverheadHud
var _capture_linger: float = 0.0
var _capture_alerting: bool = false
## The red glow round the screen edge when you are hit (under the controls).
var pulse: HurtPulse
## The tray at the bottom (it only draws).
var console: ConsolePanel
## The quick chat button, outside the plate: above NEXT, or in the Sides layout at the foot of the cards' column; what it says shows over your kid.
var chat: QuickChat
## Quick chat is for playing with others: off in offline practice (nobody to talk to).
var chat_enabled: bool = true
## UI pixels of the screen's bottom edge the system keeps (home bar), set by the match screen.
var bottom_inset: float = 0.0
## UI pixels the notch or status bar keeps at the top: the header grows by it.
var top_inset: float = 0.0
var header: HeaderPanel
var top: Control
var scoreboard: Scoreboard
## The match clock at the right of the header.
var clock_chip: ClockChip
## The stage word that flashes in the middle and flies to the clock's caption.
var clock_flash: ClockStageFlash
## True while the match stands still (offline, behind the menu): the clock's stage word waits too.
var sim_held: bool = false
## The hand: NEXT, four cards, the elixir bar and your health (it takes the touches on the tray).
var hand: CardHand
## Tumbang Preso and Patintero: no skills, so no hand; this stands on the tray instead (the role line and the Throw button).
var street_pad: StreetGamePad
## Tumbang Preso's kids' points, on the left sidewalk (null elsewhere: Patintero's team score is the card at the top centre).
var score_column: ScoreColumn
## True in those two games (set by the match screen before the first frame).
var street_game: bool = false
## Taps on the street (move orders).
var street: StreetTouch
## The ring where your kid is walking to.
var marker: MoveMarker
var tagged_panel: TaggedPanel
## The opening show's banner along the bottom ("TAG THEIR BASE!").
var intro_bar: Control
var intro_title: Label
var intro_sub: Label
var swap_button: Button
var again_button: Button
var point_result: PointResult
var _covered: bool = false
var results: MatchResults
var coach: CoachMarks
## What the results card says the match was ("Practice", "Hotspot", "Online").
var results_mode: String = "Practice"
## Big countdown in the middle while a capture is running (not an enemy taking your base: that is `capture_alert`).
var capture_label: Label
## The warning for an enemy taking your base: a triangle in the middle of the scoreboard and two lines under its row.
var capture_alert: CaptureAlert
var settings_button: TextureButton
var menu: MatchMenu
var banner: Label

var feedback: MatchFeedback
## Per hand slot: letting go now would throw nothing (nobody in range); until when the card says so after a release.
var _no_target: Array[bool] = [false, false, false, false]
var _no_target_until: Array[int] = [0, 0, 0, 0]
var connection_blocked: bool = false

var _banner_tween: Tween

var _banner_left: float = 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build()
	Settings.accessibility_changed.connect(_on_accessibility_changed)
	Settings.controls_changed.connect(layout_controls)
	resized.connect(layout_controls)
	layout_controls()


func set_top_inset(pixels: float) -> void:
	top.position.y = pixels
	top_inset = pixels
	header.set_header_height(header_height())
	header_height_changed.emit(header_height())


## The header's height now: its own plus the notch's inset.
func header_height() -> float:
	return HEADER_HEIGHT + top_inset


## The home bar or gesture area under the screen (UI pixels): the tray grows by it and the cards ride above it,
## so a thumb that rests on the bar never lands on a card, and the camera keeps the taller tray clear.
func set_bottom_inset(pixels: float) -> void:
	var inset: float = clampf(pixels, 0.0, MAX_BOTTOM_INSET)
	if is_equal_approx(inset, bottom_inset):
		return
	bottom_inset = inset
	layout_controls()


func set_chat_enabled(on: bool) -> void:
	chat_enabled = on
	chat.visible = on and hand.visible
	if not on:
		chat.set_open(false)


## The tray's height now: its own plus the bottom inset.
func console_height() -> float:
	if street_game:
		return STREET_TRAY + bottom_inset
	return (CardHand.SIDES_TRAY if sides_tray() else CONSOLE_HEIGHT) + bottom_inset


## True with Settings > Cards tray = Sides: the cards stand on the sides of the street and the tray is only the ice water bar's strip.
func sides_tray() -> bool:
	return Settings.cards_tray == &"sides" and not street_game


## The width of the side panel the cards stand on in the Sides layout (0 in the bottom tray), and its side: the writing hand's.
func side_reserve() -> float:
	return CardHand.SIDES_COLUMN if sides_tray() else 0.0


func side_reserve_left() -> bool:
	return Settings.left_handed


## The height of the blue plate (and of the tagged panel on it): the whole tray, or in the Sides layout only the bar's strip while you
## play (the plate grows to the full tray while you wait on the sidewalk, so the tagged panel has room; the street stays framed the same).
func console_panel_height() -> float:
	if street_game:
		return console_height()
	if sides_tray() and hand != null and hand.visible:
		return console_height()
	return CONSOLE_HEIGHT + bottom_inset


## The column beside the blue plate while the hand shows (the plate is full width while you wait on the sidewalk): Bottom's holds NEXT and the
## chat button, Sides' is the cards' column with the chat button at its foot; both stand outside the plate, over the street.
func sync_plate_column() -> void:
	var shown: bool = hand != null and hand.visible
	var width: float = (CardHand.SIDES_COLUMN if sides_tray() else CardHand.COLUMN) if shown else 0.0
	console.set_column(width, Settings.left_handed if sides_tray() else not Settings.left_handed)


func set_controls_active(active: bool) -> void:
	active = active and not connection_blocked and not _covered and not (chat != null and chat.is_open())
	if not active:
		cancel_controls()
	hand.set_process_input(active)
	street_pad.set_process_input(active)
	street.set_process_unhandled_input(active)


## Clears every live gesture (a held card is dropped, a finger on the street lets go) while leaving input processing as it is.
func cancel_controls() -> void:
	hand.cancel_hold()
	street_pad.cancel()
	street.release()


func controls_active() -> bool:
	return hand.is_processing_input()


func show_banner(text: String) -> void:
	if _covered:
		return
	banner.text = text
	banner.add_theme_font_size_override("font_size", banner_font_size(text))
	banner.visible = true
	_banner_left = BANNER_TIME
	if is_instance_valid(_banner_tween):
		_banner_tween.kill()
	if Settings.reduced_motion:
		banner.scale = Vector2.ONE
		banner.modulate.a = 1.0
		return
	# pop in: starts big and transparent, settles with a little overshoot
	banner.pivot_offset = banner.size / 2.0
	banner.scale = Vector2.ONE * BANNER_POP_FROM
	banner.modulate.a = 0.0
	_banner_tween = create_tween().set_parallel(true)
	_banner_tween.tween_property(banner, "scale", Vector2.ONE, BANNER_POP_TIME).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	_banner_tween.tween_property(banner, "modulate:a", 1.0, 0.12)


## The banner's font size for `text`: `BANNER_FONT`, or smaller so the words keep `BANNER_MARGIN` from both sides.
func banner_font_size(text: String) -> int:
	var room: float = maxf(size.x - BANNER_MARGIN * 2.0 - BANNER_OUTLINE * 2.0, 1.0)
	var width: float = KalyeahTheme.DISPLAY_FONT.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, BANNER_FONT).x
	if width <= room:
		return BANNER_FONT
	return int(floorf(float(BANNER_FONT) * room / width))


func banner_text() -> String:
	return banner.text if banner.visible else ""


## The hand, the street taps and the hint for the local player.
func sync_player(sim: MatchSim, player: PlayerState, pick_open: bool) -> void:
	tagged_panel.sync(sim, player, pick_open)
	# no hand while you wait on the sidewalk (the tagged panel takes the tray)
	var playing: bool = not player.tagged and sim.phase == MatchSim.Phase.PLAYING and not pick_open and not connection_blocked
	# during the opening show the first hand is on the tray, dimmed and locked, instead of an empty tray
	var shown: bool = playing or (sim.phase == MatchSim.Phase.INTRO and not player.tagged and not pick_open and not connection_blocked)
	if street_game:
		_sync_street_pad(sim, player, shown, playing)
		return
	var shown_before: bool = hand.visible
	hand.visible = shown
	chat.visible = shown and chat_enabled
	if shown != shown_before and sides_tray():
		layout_controls()  # the plate is the strip while the hand shows, the whole tray while you wait
	sync_plate_column()
	street.active = playing
	if not playing:
		street.release()
	for slot: int in _no_target.size():
		_no_target[slot] = playing and sim.lacks_target(player, slot)
	var notices: Array[String] = []
	for slot: int in _no_target.size():
		notices.append(_notice(slot) if playing else "")
	hand.show_state(sim, player, playing, _block_tag(sim, player) if playing else "", notices)
	pulse.sync_health(player.hp, sim.rules.player_max_hp, playing)


## Tumbang Preso and Patintero take the tray over (no hand): the tray is its own height and the pad stands on it. Call before the first frame.
func set_street_game(on: bool) -> void:
	street_game = on
	hand.visible = not on
	street_pad.visible = on
	console.visible = not on  # no tray: the knob and the buttons float over the street
	chat.set_vertical(on)
	layout_controls()


## Tumbang Preso: every kid's points stand on the left sidewalk.
func setup_street_scores(sim: MatchSim, local_id: int) -> void:
	score_column = ScoreColumn.new()
	add_child(score_column)
	score_column.setup(sim, local_id)
	layout_controls()


## Tumbang Preso and Patintero: the role line on the tray and, for a thrower with its slipper behind the line, the Throw button. There is no
## hand, no chat and no cards.
func _sync_street_pad(sim: MatchSim, player: PlayerState, shown: bool, playing: bool) -> void:
	hand.visible = false
	street.active = false  # the knob moves the kid, not a tap on the street
	if not playing:
		street.release()
	var game: TumbangPreso = sim.mode as TumbangPreso
	var lines: Patintero = sim.mode as Patintero
	var button: StreetGamePad.Action = StreetGamePad.Action.NONE
	var fill: float = 0.0
	var can_act: bool = false
	if playing and game != null and game.is_attacker(player):
		fill = game.charge(player.id)
		if game.can_throw(player):
			button = StreetGamePad.Action.THROW
		elif not game.has_slipper(player.id):
			button = StreetGamePad.Action.KICK
			can_act = game.can_kick(player)
	elif playing and lines != null and not lines.is_jailed(player.id):
		button = StreetGamePad.Action.DIVE
		fill = lines.dive_ready_share(player.id)
		can_act = lines.can_dive(player)
	var on_sidewalk: bool = lines != null and lines.is_jailed(player.id)
	var status: Array[String] = PracticeMatchPresentation.street_status(sim, player)
	chat.visible = shown and chat_enabled
	street_pad.visible = shown and not player.tagged
	street_pad.show_state(status[0], status[1], button, fill, can_act, on_sidewalk)  # (a tagged runner's knob is in the middle of the bottom)
	if score_column != null:
		score_column.sync(sim)
	sync_plate_column()
	pulse.sync_health(player.hp, sim.rules.player_max_hp, playing)


## The hint over the tray while a card is held (`release`: what letting go now would do; `nobody`: it would throw nothing).
func sync_hold_hint(player: PlayerState, playing: bool, release: MatchInput.Release, nobody: bool) -> void:
	feedback.sync_hint(player, playing, release, nobody)


## A card let go with nobody in range threw nothing: say so on it for a moment.
func note_released(slot: int, cancelled: bool) -> void:
	if slot >= 0 and slot < _no_target.size() and not cancelled and _no_target[slot]:
		_no_target_until[slot] = Time.get_ticks_msec() + NO_TARGET_MS


func _notice(slot: int) -> String:
	return NO_TARGET_TAG if Time.get_ticks_msec() < _no_target_until[slot] else ""


## What to print on the cards while a status (or the place you stand in) stops you casting ("" when nothing does).
func _block_tag(sim: MatchSim, player: PlayerState) -> String:
	if sim.casting_blocked(player):
		return CAPTURING_TAG if sim.layout.in_base(-sim.side_for_team(player.team), player.position) else RESCUING_TAG
	if player.effects.can_cast():
		return ""
	for type: int in CAST_BLOCK_TAGS:
		if player.effects.has(type as StatusEffects.Type):
			return CAST_BLOCK_TAGS[type] as String
	return ""


## Top row: the score card; the capture countdown; banners fade out.
func sync_score(sim: MatchSim, local_team: int, delta: float, local_id: int = -1) -> void:
	scoreboard.show_score(sim.score, local_team, sim.match_time)
	scoreboard.show_capture(sim, local_team)
	_sync_clock(sim, delta)
	_sync_capture(sim, local_team, delta)
	again_button.visible = sim.phase == MatchSim.Phase.MATCH_OVER
	point_result.sync(sim, local_team)
	results.set_over(sim.phase == MatchSim.Phase.MATCH_OVER, sim, local_id, local_team, results_mode)
	var covered: bool = sim.phase in [MatchSim.Phase.POINT_FREEZE, MatchSim.Phase.MATCH_OVER]
	if covered:
		chat.set_open(false)
		if menu.visible:
			menu.close()
	for item: CanvasItem in [header, top, scoreboard, marker, pulse, console, feedback]:
		item.visible = not covered
	if score_column != null:
		score_column.visible = not covered
	if street_game:
		console.visible = false
		# Patintero has a team score at the top centre like Agawan Base; Tumbang Preso's points are every kid's, in the column on the left sidewalk
		scoreboard.visible = not covered and not sim.mode.is_free_for_all()
	overhead.visible = not covered or overhead.tag_count() > 0
	if covered:
		for item: CanvasItem in [hand, street_pad, chat, tagged_panel, intro_bar, capture_label, capture_alert, banner, coach]:
			item.hide()
		clock_flash.cancel()
	if _covered != covered:
		_covered = covered
		set_controls_active(not covered and not menu.visible and not coach.running)
	if sim.phase != MatchSim.Phase.MATCH_OVER and _banner_left > 0.0:
		_banner_left -= delta
		if _banner_left <= 0.0:
			banner.visible = false


## The clock: what is left of this stage (it restarts at Extended and again at Overtime), or the match time with the stages off. A new
## stage flashes its name in the middle and flies it to the caption (`ClockStageFlash`).
func _sync_clock(sim: MatchSim, delta: float) -> void:
	var counting: bool = sim.counts_down()
	var stage: MatchSim.ClockStage = sim.clock_stage()
	clock_chip.show_time(sim.clock_left() if counting else sim.match_time, stage, counting)
	if not counting:
		clock_flash.cancel()
		return
	var caption_at: Vector2 = clock_flash.get_global_transform().affine_inverse() * (clock_chip.get_global_transform() * clock_chip.caption_center())
	var into: float = sim.round_time - sim.clock_stage_start(stage)
	clock_flash.sync(stage, into, 0.0 if sim_held else delta, caption_at, clock_chip.caption_color())
	clock_chip.hide_caption(clock_flash.flying())


## "HOLD THE BASE! 3" for us, in the middle of the screen; against us it is the warning triangle and its two lines at the top (`capture_alert`).
## A capture that stops for a moment (a kid steps out and back, two teams meet in a base) keeps its words and warning for
## `CAPTURE_LINGER` seconds, so a short gap does not make them blink; a change of who captures, or the end of the point, is shown at once.
func _sync_capture(sim: MatchSim, local_team: int, delta: float) -> void:
	var running: bool = sim.capture_team >= 0 and sim.phase == MatchSim.Phase.PLAYING
	var ours: bool = sim.capture_team == local_team
	var alert: bool = running and not ours and sim.capture_kind == MatchSim.CaptureKind.BASE
	if running:
		_capture_linger = CAPTURE_LINGER
		_capture_alerting = alert
	elif sim.phase == MatchSim.Phase.PLAYING and _capture_linger > 0.0:
		_capture_linger -= delta
		running = _capture_linger > 0.0
		alert = running and _capture_alerting
	else:
		_capture_linger = 0.0
	capture_alert.show_alert(alert)
	capture_label.visible = running and not alert
	if not capture_label.visible or sim.capture_team < 0:
		return
	var seconds: int = ceili(sim.capture_time_left())
	var how: String
	if sim.capture_kind == MatchSim.CaptureKind.ALL_TAGGED:
		how = "ALL TAGGED!" if ours else "ALL OF YOU TAGGED!"
	else:
		how = "HOLD THE BASE!"
	capture_label.text = "%s\n%d" % [how, seconds]
	capture_label.add_theme_color_override("font_color", CAPTURE_COLOR.lerp(TeamColors.feed_own() if ours else TeamColors.feed_enemy(), 0.35))


# ---- building ------------------------------------------------------------------

func _build() -> void:
	overhead = OverheadHud.new()
	overhead.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_place(overhead, Vector4(0, 0, 1, 1), Vector4.ZERO)
	add_child(overhead)
	marker = MoveMarker.new()
	marker.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(marker)
	move_child(marker, 0)  # under the bars over the kids
	pulse = HurtPulse.new()
	add_child(pulse)
	console = ConsolePanel.new()
	console.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(console)
	MatchHudTop.build(self)
	capture_alert = CaptureAlert.new()
	add_child(capture_alert)
	hand = CardHand.new()
	add_child(hand)
	street_pad = StreetGamePad.new()
	add_child(street_pad)
	chat = QuickChat.new()
	add_child(chat)
	chat.opened_changed.connect(func(opened: bool) -> void:
		set_controls_active(not opened and not menu.visible))
	street = StreetTouch.new()
	add_child(street)
	banner = Label.new()
	_place(banner, Vector4(0, 0.3, 1, 0.3), Vector4(0, -60, 0, 60))
	banner.visible = false
	UiKit.letter(banner, BANNER_FONT, RoyaleStyle.GOLD)
	banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	banner.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(banner)
	clock_flash = ClockStageFlash.new()
	add_child(clock_flash)
	capture_label = _pill_label("", UiMetrics.TITLE, Vector4(0, 0.4, 1, 0.4), Vector4(120, -56, -120, 56))
	capture_label.theme_type_variation = &"DisplayLabel"
	capture_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	capture_label.add_theme_color_override("font_outline_color", Color(0.08, 0.05, 0.1))
	capture_label.add_theme_constant_override("outline_size", 10)
	again_button = _button("Battle again!", UiMetrics.BODY, Vector4(0.5, 0.5, 0.5, 0.5), Vector4(-160, -30, 160, 60))
	KalyeahTheme.style_button(again_button, "orange")
	again_button.pressed.connect(again_pressed.emit)
	again_button.visible = false
	point_result = PointResult.new()
	add_child(point_result)
	results = MatchResults.new()
	add_child(results)
	results.adopt(again_button, true)
	tagged_panel = TaggedPanel.new()
	add_child(tagged_panel)
	swap_button = tagged_panel.swap_button
	tagged_panel.swap_pressed.connect(swap_pressed.emit)
	MatchHudTop.build_intro(self)
	coach = CoachMarks.new(self)
	add_child(coach)
	# last, so it draws over the hand
	menu = MatchMenu.new()
	add_child(menu)
	menu.exit_pressed.connect(exit_pressed.emit)
	menu.closed.connect(set_controls_active.bind(true))
	feedback = MatchFeedback.new()
	add_child(feedback)
	feedback.return_home.connect(exit_pressed.emit)


## Places the tray and the hand (see MatchHudLayout).
func layout_controls() -> void:
	MatchHudLayout.apply(self)


## Shows the opening show's banner (`title`, `subtitle`), or hides it with "".
func show_intro(title: String, subtitle: String = "") -> void:
	intro_bar.visible = not title.is_empty()
	intro_title.text = title
	intro_sub.text = subtitle


## Open or close the settings menu; the controls ignore touches while it is open.
func toggle_menu() -> void:
	if menu.visible:
		menu.close()
		return
	set_controls_active(false)
	menu.open()


## Anchors first, then offsets (setting an anchor can move offsets).
func _place(control: Control, anchors: Vector4, offsets: Vector4) -> void:
	control.anchor_left = anchors.x
	control.anchor_top = anchors.y
	control.anchor_right = anchors.z
	control.anchor_bottom = anchors.w
	control.offset_left = offsets.x
	control.offset_top = offsets.y
	control.offset_right = offsets.z
	control.offset_bottom = offsets.w


func _pill_label(text: String, font_size: int, anchors: Vector4, offsets: Vector4) -> Label:
	var label: Label = Label.new()
	_place(label, anchors, offsets)
	label.theme_type_variation = &"HudPill"
	label.text = text
	label.visible = false
	label.add_theme_font_size_override("font_size", font_size)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(label)
	return label


func _button(text: String, font_size: int, anchors: Vector4, offsets: Vector4, parent: Control = null) -> Button:
	var button: Button = Button.new()
	_place(button, anchors, offsets)
	button.text = text
	button.add_theme_font_size_override("font_size", font_size)
	(parent if parent != null else self).add_child(button)
	return button


func set_connection(text: String, terminal: bool = false) -> void:
	connection_blocked = not text.is_empty()
	feedback.show_connection(text, terminal)
	if connection_blocked:
		set_controls_active(false)


func _on_accessibility_changed() -> void:
	if Settings.reduced_motion:
		if is_instance_valid(_banner_tween):
			_banner_tween.kill()
		banner.scale = Vector2.ONE
		banner.modulate.a = 1.0
