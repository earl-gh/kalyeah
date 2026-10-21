class_name UiGallery
extends ScrollContainer
## Every piece of the interface in every state, on one page (Settings, UI gallery): buttons (resting,
## pressed look, disabled), chips with their contrast, switch, segmented control, field and slider, the panel
## kinds, toasts and status strips, the type scale and the faces. It is how the look is reviewed on a phone
## without playing a match, so a new component is not finished until it is here with all its states.

const VARIANTS: Array[String] = ["gold", "blue", "green", "red", "slate"]
## The contrast bar for the small white text on a chip.
const CONTRAST_BAR: float = 4.5

var sections: Array[String] = []
var chip_ratios: Dictionary[String, float] = {}

var _content: VBoxContainer


func _ready() -> void:
	name = "UiGallery"
	horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	follow_focus = true
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", UiMetrics.SECTION_GAP)
	add_child(_content)
	_content.add_child(UiKit.page_header("UI gallery", "Every control in every state."))
	_section("Buttons", _buttons())
	_section("Chips", _chips())
	_section("Controls", _controls())
	_section("Panels", _panels())
	_section("Feedback", _feedback())
	_section("Type", _type_scale())
	_section("Faces", _faces())


func _section(title: String, content: Control) -> void:
	sections.append(title)
	var box: VBoxContainer = VBoxContainer.new()
	box.name = "Section_%s" % title
	box.add_theme_constant_override("separation", UiMetrics.COMPACT_GAP)
	box.add_child(UiKit.section_label(title))
	box.add_child(UiKit.card(content, "glass", 20.0))
	_content.add_child(box)


## Each colour three ways: resting, the pressed look, disabled. The first column is live; the others are shown only.
func _buttons() -> Control:
	var grid: GridContainer = GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	grid.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	for variant: String in VARIANTS:
		var live: Button = MenuDialog.make_button(variant.capitalize(), variant, UiMetrics.DETAIL, Vector2(0.0, UiMetrics.TOUCH))
		live.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		grid.add_child(live)
		var pressed: Button = MenuDialog.make_button("%s pressed" % variant.capitalize(), variant, UiMetrics.DETAIL, Vector2(0.0, UiMetrics.TOUCH))
		pressed.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		pressed.focus_mode = Control.FOCUS_NONE
		pressed.mouse_filter = Control.MOUSE_FILTER_IGNORE
		pressed.add_theme_stylebox_override("normal", UiSkin.button(variant, "pressed"))
		grid.add_child(pressed)
	var off: Button = MenuDialog.make_button("Disabled", "grey", UiMetrics.DETAIL, Vector2(0.0, UiMetrics.TOUCH))
	off.disabled = true
	off.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_child(off)
	return grid


## The tags the game uses, each with its measured contrast so a weak colour shows up here.
func _chips() -> Control:
	var flow: HFlowContainer = HFlowContainer.new()
	flow.add_theme_constant_override("h_separation", UiMetrics.GAP)
	flow.add_theme_constant_override("v_separation", UiMetrics.GAP)
	var specs: Array[Dictionary] = [
		{"text": "Attack", "fill": UiKit.CHIP_RED}, {"text": "Control", "fill": UiKit.CHIP_PURPLE},
		{"text": "Block", "fill": UiKit.CHIP_BLUE}, {"text": "Heal", "fill": UiKit.CHIP_GREEN},
		{"text": "Victory", "fill": UiKit.CHIP_GREEN}, {"text": "Defeat", "fill": UiKit.CHIP_RED},
		{"text": "Bato-bato-pik", "fill": UiKit.CHIP_SLATE},
		{"text": "You", "fill": KalyeahTheme.TEAM_BLUE}, {"text": "Them", "fill": KalyeahTheme.TEAM_RED},
	]
	for spec: Dictionary in specs:
		var column: VBoxContainer = VBoxContainer.new()
		column.add_theme_constant_override("separation", 2)
		var chip: PanelContainer = UiKit.chip(String(spec["text"]), spec["fill"] as Color, Color.WHITE, 22)
		chip.name = "Chip_%s" % String(spec["text"])
		column.add_child(chip)
		var ratio: float = UiKit.contrast(spec["fill"] as Color, Color.WHITE)
		chip_ratios[String(spec["text"])] = ratio
		var good: bool = ratio >= CONTRAST_BAR
		var note: Label = UiKit.text("%.1f:1 %s" % [ratio, "ok" if good else "too low"], UiMetrics.MIN_TEXT,
			KalyeahTheme.SUCCESS if good else KalyeahTheme.ERROR, false)
		note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		column.add_child(note)
		flow.add_child(column)
	return flow


func _controls() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	var switches: HBoxContainer = HBoxContainer.new()
	switches.add_theme_constant_override("separation", UiMetrics.GAP)
	var off: UiToggle = UiToggle.new()
	off.name = "SwitchOff"
	off.accessibility_name = "Example switch, off"
	switches.add_child(off)
	switches.add_child(UiKit.text("Off", UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED, false))
	var on: UiToggle = UiToggle.new()
	on.name = "SwitchOn"
	on.accessibility_name = "Example switch, on"
	on.set_state(true)
	switches.add_child(on)
	switches.add_child(UiKit.text("On", UiMetrics.DETAIL, KalyeahTheme.TEXT_MUTED, false))
	column.add_child(switches)
	var segmented: UiSegmented = UiSegmented.new([{"id": &"a", "label": "One"}, {"id": &"b", "label": "Two"}], UiMetrics.TOUCH)
	segmented.select(&"a", false)
	column.add_child(segmented)
	for sample: Dictionary in [{"label": "Room code boxes: empty, typing, wrong, shown", "text": ""}]:
		column.add_child(UiKit.text(String(sample["label"]), UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED, false))
	var empty: UiCodeBoxes = UiCodeBoxes.new(8, "ABCDEFGHJKMNPQRSTUVWXYZ23456789")
	empty.name = "CodeEmpty"
	column.add_child(empty)
	var typing: UiCodeBoxes = UiCodeBoxes.new(8, "ABCDEFGHJKMNPQRSTUVWXYZ23456789")
	typing.name = "CodeTyping"
	typing.set_text("ABC23")
	column.add_child(typing)
	var wrong: UiCodeBoxes = UiCodeBoxes.new(8, "ABCDEFGHJKMNPQRSTUVWXYZ23456789")
	wrong.name = "CodeWrong"
	wrong.set_text("ABC234X")
	wrong.set_error(true)
	column.add_child(wrong)
	var shown: UiCodeBoxes = UiCodeBoxes.new(8, "", false)
	shown.name = "CodeShown"
	shown.set_text("K7M2Q9XA")
	column.add_child(shown)
	var field: LineEdit = LineEdit.new()
	field.placeholder_text = "A text field"
	field.custom_minimum_size = Vector2(0.0, UiMetrics.TOUCH)
	column.add_child(field)
	var slider: HSlider = HSlider.new()
	slider.accessibility_name = "Example slider"
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.05
	slider.value = 0.6
	slider.custom_minimum_size = Vector2(0.0, 48.0)
	column.add_child(slider)
	return column


func _panels() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	for kind: String in UiSkin.PLATES:
		var plate: PanelContainer = UiKit.card(UiKit.text("%s panel" % kind.capitalize(), UiMetrics.DETAIL), kind, 22.0)
		plate.name = "Plate_%s" % kind
		column.add_child(plate)
	return column


## Buttons that fire a toast, and the three looks of a status strip.
func _feedback() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", UiMetrics.GAP)
	var row: GridContainer = GridContainer.new()
	row.columns = 2
	row.add_theme_constant_override("h_separation", UiMetrics.COMPACT_GAP)
	row.add_theme_constant_override("v_separation", UiMetrics.COMPACT_GAP)
	for tone: String in UiToast.KINDS:
		var button: Button = MenuDialog.make_button("Toast: %s" % tone, "grey", UiMetrics.DETAIL, Vector2(0.0, UiMetrics.TOUCH))
		button.name = "Toast_%s" % tone
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(func() -> void: UiToast.say(self, "This is a %s message" % tone, tone))
		row.add_child(button)
	column.add_child(row)
	column.add_child(UiStatusStrip.strip("Connecting…", "wait"))
	column.add_child(UiStatusStrip.strip("Connection lost. Try again.", "error"))
	column.add_child(UiStatusStrip.strip("This is a preview: a real room needs the Android app.", "info"))
	return column


func _type_scale() -> Control:
	var column: VBoxContainer = VBoxContainer.new()
	column.add_theme_constant_override("separation", 6)
	var display: Label = UiKit.text("DISPLAY", UiMetrics.TITLE + 16, KalyeahTheme.CREAM, false)
	display.theme_type_variation = &"DisplayLabel"
	column.add_child(display)
	column.add_child(UiKit.text("Body %d: the main reading size" % UiMetrics.BODY, UiMetrics.BODY, KalyeahTheme.TEXT, false))
	column.add_child(UiKit.text("Detail %d: sentences under a title" % UiMetrics.DETAIL, UiMetrics.DETAIL, KalyeahTheme.TEXT, false))
	column.add_child(UiKit.text("Caption %d: the smallest text" % UiMetrics.CAPTION, UiMetrics.CAPTION, KalyeahTheme.TEXT_MUTED, false))
	return column


func _faces() -> Control:
	var flow: HFlowContainer = HFlowContainer.new()
	flow.add_theme_constant_override("h_separation", UiMetrics.GAP)
	flow.add_theme_constant_override("v_separation", UiMetrics.GAP)
	for id: String in Session.AVATARS:
		flow.add_child(UiKit.avatar(id, 88.0))
	return flow
