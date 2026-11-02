class_name TumbangRoll
extends Control
## The roll that picks the first taya of Tumbang Preso, played in the opening show over the street (owner): the kid-pick carousel
## (`KidCarousel`, the kids who are in the match) spins round, each step slower than the one before, and stops on the taya's kid, then says
## who it is. The sim has already drawn the taya when the game starts (`TumbangPreso.setup`), so every phone plays the same roll to the same
## kid; `show_at` is driven by the opening show's clock and a late joiner sees the finished roll. With Reduce motion there is no spin: the
## carousel is already on the taya. View only.

## A card went by / the roll stopped on the taya: the match plays `STEP_SOUND` and `LANDED_SOUND` (the owner's own sound files, reused:
## the sound of picking a card, and the bright ding of a card that is ready).
signal stepped
signal landed

const STEP_SOUND: String = "card_pick"
const LANDED_SOUND: String = "card_ready"
const TITLE: String = "Mataya-taya!"
## The steps get slower as `elapsed / finish` to this power (above 1: each step takes longer than the one before).
const CURVE: float = 1.6
## Times round the ring before it stops.
const LAPS: int = 2
## The last step is at this share of the roll's length; the rest is the held result.
const FINISH_SHARE: float = 0.78
const PANEL_WIDTH: float = 660.0
const CAROUSEL_HEIGHT: float = 330.0
const RESULT_COLOR: Color = Color(1.0, 0.3, 0.26)

var carousel: KidCarousel
var title_label: Label
var name_label: Label
var _kids: Array[StringName] = []
var _owners: Dictionary[StringName, PackedStringArray] = {}
var _final: int = 0
## The card in the middle now (an index into `_kids`).
var _focus: int = 0
var _steps: Array[Dictionary] = []
var _done: int = 0
var _result: String = ""
var _landed: bool = false
var _roll_seconds: float = 0.0


## The steps of a roll over `count` cards that ends on card `final` at `finish` seconds: [{at, index}], starting from card 0, `LAPS` times
## round and on to `final`, one card a step, each step slower than the one before.
static func plan(count: int, final: int, finish: float) -> Array[Dictionary]:
	var steps: Array[Dictionary] = []
	if count < 2:
		return steps
	var total: int = LAPS * count + posmod(final, count)
	for k: int in range(1, total + 1):
		steps.append({"at": finish * pow(float(k) / float(total), CURVE), "index": k % count})
	return steps


## Builds the roll for the kids in `sim`: one card for each kid that plays, and the taya's kid to stop on. `names` says a player's name;
## the result reads "YOU'RE IT!" when the taya is `local_id`.
func setup(sim: MatchSim, taya_id: int, local_id: int, roll_seconds: float, names: Callable) -> void:
	name = "TumbangRoll"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_roll_seconds = roll_seconds
	var characters: Array[CharacterDef] = []
	for id: int in sim.players:
		var state: PlayerState = sim.players[id]
		var kid: CharacterDef = _character(sim, state.character_id)
		if not _kids.has(kid.id):
			_kids.append(kid.id)
			characters.append(kid)
		var who: PackedStringArray = _owners.get(kid.id, PackedStringArray()) as PackedStringArray
		who.append(String(names.call(state)))
		_owners[kid.id] = who
	var taya: PlayerState = sim.players[taya_id] as PlayerState
	var taya_kid: CharacterDef = _character(sim, taya.character_id)
	_final = maxi(_kids.find(taya_kid.id), 0)
	_result = "YOU'RE IT!" if taya_id == local_id else String(names.call(taya)).to_upper() + " IS IT!"
	var panel: PanelContainer = PanelContainer.new()
	panel.name = "Panel"
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_theme_stylebox_override("panel", UiSkin.plate("night", 18.0, UiSkin.RADIUS_XL))
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP, Control.PRESET_MODE_MINSIZE)
	panel.anchor_left = 0.5
	panel.anchor_right = 0.5
	panel.offset_left = -PANEL_WIDTH / 2.0
	panel.offset_right = PANEL_WIDTH / 2.0
	panel.offset_top = 150.0
	add_child(panel)
	var column: VBoxContainer = VBoxContainer.new()
	column.mouse_filter = Control.MOUSE_FILTER_IGNORE
	column.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	panel.add_child(column)
	title_label = Label.new()
	title_label.name = "Title"
	title_label.text = TITLE
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(title_label, UiMetrics.HEADLINE, RoyaleStyle.GOLD)
	column.add_child(title_label)
	carousel = KidCarousel.new()
	carousel.custom_minimum_size = Vector2(PANEL_WIDTH - 36.0, CAROUSEL_HEIGHT)
	carousel.size = carousel.custom_minimum_size
	column.add_child(carousel)
	carousel.setup_kids(characters, _kids[0] if not _kids.is_empty() else &"")
	carousel.bob = 0.0
	carousel.fit(carousel.custom_minimum_size)
	name_label = Label.new()
	name_label.name = "Who"
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	UiKit.letter(name_label, UiMetrics.DISPLAY, Color.WHITE)
	column.add_child(name_label)
	_steps = plan(_kids.size(), _final, _roll_seconds * FINISH_SHARE)
	_show_names()


## Shows the roll `elapsed` seconds into the opening show's roll.
func show_at(elapsed: float) -> void:
	visible = true
	if Settings.reduced_motion or (_done == 0 and elapsed >= _roll_seconds * FINISH_SHARE):
		# no spin (or the show is already over its spinning when this phone sees it): the cards are already on the taya
		if _done < _steps.size():
			_done = _steps.size()
			_focus = _final
			carousel.select(_final, false)
		_show_result()
		return
	var moved: bool = false
	while _done < _steps.size() and elapsed >= (_steps[_done]["at"] as float):
		var next_at: float = (_steps[_done + 1]["at"] as float) if _done + 1 < _steps.size() else _roll_seconds * FINISH_SHARE
		carousel.step_seconds = maxf(next_at - (_steps[_done]["at"] as float), 0.05)
		_focus = _steps[_done]["index"] as int
		carousel.select(_focus, true, 1)
		_done += 1
		moved = true
	if moved:
		stepped.emit()  # one sound a frame, however many steps the frame took
	if _done >= _steps.size() and elapsed >= _roll_seconds * FINISH_SHARE:
		_show_result()
	else:
		_show_names()


## True once the roll has stopped and says who is it.
func is_finished() -> bool:
	return name_label != null and name_label.text == _result and not _result.is_empty()


func _show_names() -> void:
	var who: PackedStringArray = _owners.get(_kids[_focus] if _focus < _kids.size() else &"", PackedStringArray()) as PackedStringArray
	name_label.text = " / ".join(who).to_upper()
	name_label.remove_theme_color_override("font_color")


func _show_result() -> void:
	name_label.text = _result
	name_label.add_theme_color_override("font_color", RESULT_COLOR)
	if not _landed:
		_landed = true
		landed.emit()


static func _character(sim: MatchSim, id: StringName) -> CharacterDef:
	for character: CharacterDef in sim.rules.characters:
		if character.id == id:
			return character
	return sim.rules.characters[0]
