class_name TaggedPanel
extends Control
## What you see while tagged and waiting in the box: over the bottom of the screen, with no sheet
## behind it (the street stays visible), a row of up to five 2:3 player portraits (everyone
## else in the match: teammates first, names in their team's colour) and a Change weapons button. Tap a card and
## the camera follows that player; the watched card is lit. Built in code;
## MatchHud feeds it with `sync()`.

signal swap_pressed
## The player tapped the card of player `id` (the camera should follow them).
signal watch_requested(id: int)

const BAND_HEIGHT: float = 360.0
const INK: Color = Color(0.1, 0.06, 0.06)
const TITLE_COLOR: Color = Color(1.0, 0.4, 0.34)
const MAX_CARDS: int = 5
## Cards are 2:3 (width:height).
const CARD_SIZE: Vector2 = Vector2(96.0, 144.0)
const CARD_GAP: float = 12.0
const OWN: Color = Color(0.3, 0.6, 1.0)
const ENEMY: Color = Color(1.0, 0.35, 0.35)
const WATCHED: Color = Color(1.0, 0.86, 0.3)
const WAIT_TEXT: String = "Tagged · Wait for a rescue"
const HINT_TEXT: String = "Teammates can run into your box to set you free."

## How to get out (kept as a label for screen readers and tests).
var wait_label: Label
## One line on how it ends (the box, the teammate), under the title.
var hint_label: Label
var swap_button: Button
## Player ids in card order (left to right).
var card_ids: Array[int] = []

var _cards: Array[Button] = []
var _portraits: Array[TextureRect] = []
var _names: Array[Label] = []
var _states: Array[Label] = []


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	var band: PanelContainer = PanelContainer.new()
	var bare: StyleBoxEmpty = StyleBoxEmpty.new()
	bare.set_content_margin_all(UiMetrics.PADDING)
	band.add_theme_stylebox_override("panel", bare)
	band.mouse_filter = Control.MOUSE_FILTER_IGNORE
	band.anchor_left = 0.0
	band.anchor_right = 1.0
	band.anchor_top = 1.0
	band.anchor_bottom = 1.0
	band.offset_left = UiMetrics.GAP
	band.offset_right = -UiMetrics.GAP
	band.offset_top = -BAND_HEIGHT - UiMetrics.GAP
	band.offset_bottom = -UiMetrics.GAP
	add_child(band)
	var column: VBoxContainer = VBoxContainer.new()
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	band.add_child(column)
	wait_label = _label(WAIT_TEXT, 26, TITLE_COLOR)
	wait_label.theme_type_variation = &"DisplayLabel"
	column.add_child(wait_label)
	hint_label = _label(HINT_TEXT, UiMetrics.CAPTION, Color("c9d6f2"))
	hint_label.name = "Hint"
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(hint_label)
	var row: HBoxContainer = HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", int(CARD_GAP))
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_child(row)
	for i: int in MAX_CARDS:
		_build_card(row, i)
	swap_button = Button.new()
	swap_button.text = "Change weapons"
	swap_button.custom_minimum_size = Vector2(400.0, UiMetrics.TOUCH)
	swap_button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	swap_button.add_theme_font_size_override("font_size", UiMetrics.ACTION)
	KalyeahTheme.style_button(swap_button, "orange")
	swap_button.pressed.connect(swap_pressed.emit)
	column.add_child(swap_button)


## Shown while `player` is tagged. `watched` is the id the camera follows (lit).
func sync(sim: MatchSim, player: PlayerState, watched: int, pick_open: bool) -> void:
	visible = player.tagged
	swap_button.visible = visible and not pick_open
	if not visible:
		return
	card_ids = cards_for(sim, player)
	for i: int in MAX_CARDS:
		var card: Button = _cards[i]
		card.visible = i < card_ids.size()
		if not card.visible:
			continue
		var other: PlayerState = sim.players[card_ids[i]]
		var ally: bool = other.team == player.team
		_portraits[i].texture = KidModel.portrait(other.character_id)
		_names[i].text = _display_name(sim, other)
		_states[i].text = "Tagged" if other.tagged else ""
		_names[i].add_theme_color_override("font_color", OWN.lightened(0.3) if ally else ENEMY.lightened(0.2))
		_portraits[i].modulate = Color(0.55, 0.55, 0.6) if other.tagged else Color.WHITE
		card.add_theme_stylebox_override("normal", _card_box(OWN if ally else ENEMY, card_ids[i] == watched))
		card.add_theme_stylebox_override("hover", _card_box(OWN if ally else ENEMY, card_ids[i] == watched))
		card.add_theme_stylebox_override("pressed", _card_box(OWN if ally else ENEMY, true))


## Everyone but `player`, teammates first, at most MAX_CARDS (a full 3v3 has five others).
static func cards_for(sim: MatchSim, player: PlayerState) -> Array[int]:
	var allies: Array[int] = []
	var enemies: Array[int] = []
	for id: int in sim.players:
		if id == player.id:
			continue
		if sim.players[id].team == player.team:
			allies.append(id)
		else:
			enemies.append(id)
	allies.append_array(enemies)
	return allies.slice(0, MAX_CARDS)


## Taps a card (tests and the touch handler).
func press_card(index: int) -> void:
	if index < card_ids.size():
		watch_requested.emit(card_ids[index])


func _display_name(sim: MatchSim, state: PlayerState) -> String:
	for def: CharacterDef in sim.rules.characters:
		if def.id == state.character_id:
			return def.display_name
	return "Player %d" % state.id


func _build_card(row: HBoxContainer, index: int) -> void:
	var card: Button = Button.new()
	card.custom_minimum_size = CARD_SIZE
	card.pressed.connect(press_card.bind(index))
	row.add_child(card)
	var portrait: TextureRect = TextureRect.new()
	portrait.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	portrait.offset_left = 6.0
	portrait.offset_right = -6.0
	portrait.offset_top = 6.0
	portrait.offset_bottom = -34.0
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(portrait)
	var name_label: Label = _label("", UiMetrics.CAPTION, Color.WHITE)
	name_label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	name_label.offset_top = -34.0
	name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(name_label)
	var state_label: Label = _label("", UiMetrics.CAPTION, TITLE_COLOR)
	state_label.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	state_label.offset_bottom = 40.0
	state_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(state_label)
	_cards.append(card)
	_portraits.append(portrait)
	_names.append(name_label)
	_states.append(state_label)
	card.add_theme_stylebox_override("focus", KalyeahTheme.focus_ring(16))


## No box behind a portrait: only the one being watched gets a gold ring.
func _card_box(_team: Color, lit: bool) -> StyleBoxFlat:
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(0.0, 0.0, 0.0, 0.0)
	box.set_corner_radius_all(14)
	box.border_color = WATCHED
	box.set_border_width_all(4 if lit else 0)
	return box


func _label(text: String, font_size: int, color: Color) -> Label:
	var label: Label = Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", INK)
	label.add_theme_constant_override("outline_size", 6)
	return label
