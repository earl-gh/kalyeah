class_name HowToPlayPage
extends ScrollContainer
## How to play, in the same four short steps as the first-run walkthrough (Settings, How to play): a card for
## each, and a gold button that starts a practice match with the walkthrough over it. The numbers come from the
## rules data (capture time, points to win), never from this file.

signal walkthrough_requested
## The Back button (How to play is a sub-page of Settings).
signal back_pressed

const RULES_PATH: String = "res://data/rules/game_rules.tres"

var cards: Array[Control] = []
var walkthrough_button: Button
var back_button: Button


func _ready() -> void:
	name = "HowToPlayPage"
	horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	follow_focus = true
	var content: VBoxContainer = VBoxContainer.new()
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", UiMetrics.GAP)
	add_child(content)
	back_button = MenuDialog.make_button("Back", "grey", UiMetrics.ACTION, Vector2(160.0, UiMetrics.TOUCH))
	back_button.name = "BackButton"
	back_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	back_button.pressed.connect(back_pressed.emit)
	content.add_child(back_button)
	content.add_child(UiKit.page_header("How to play", "Four things to know. Then try them."))
	var rules: GameRules = load(RULES_PATH) as GameRules
	var steps: Array[Dictionary] = CoachMarks.steps_for(rules.capture_time, rules.first_to)
	for i: int in steps.size():
		var card: Control = _card(i + 1, steps[i])
		content.add_child(card)
		cards.append(card)
	walkthrough_button = MenuDialog.make_button("Practice with the walkthrough", "orange", UiMetrics.ACTION, Vector2(0.0, 100.0))
	walkthrough_button.name = "WalkthroughButton"
	walkthrough_button.pressed.connect(walkthrough_requested.emit)
	content.add_child(walkthrough_button)


func _card(number: int, step: Dictionary) -> Control:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", UiMetrics.GAP)
	var badge: PanelContainer = UiKit.chip(str(number), Color("ffc233"), Color("3a1c00"), UiMetrics.ACTION)
	badge.name = "Number"
	badge.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	row.add_child(badge)
	var words: VBoxContainer = VBoxContainer.new()
	words.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	words.add_theme_constant_override("separation", 6)
	var title: Label = Label.new()
	title.name = "Title"
	title.theme_type_variation = &"DisplayLabel"
	title.text = (step["title"] as String).to_upper()
	title.add_theme_font_size_override("font_size", UiMetrics.TITLE)
	title.add_theme_constant_override("outline_size", 6)
	words.add_child(title)
	var text: Label = UiKit.text(step["text"] as String, UiMetrics.DETAIL, KalyeahTheme.TEXT_BODY)
	text.name = "Words"
	words.add_child(text)
	row.add_child(words)
	return UiKit.card(row, "glass", 22.0)
