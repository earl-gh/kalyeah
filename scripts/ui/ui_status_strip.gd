class_name UiStatusStrip
extends RefCounted
## How a screen's status line looks: a tinted strip, amber while waiting ("Connecting…", "Looking for the
## room…"), red for a problem ("Connection lost. Try again."), blue for a plain note. The line itself stays
## the screen's own Label (so what it says and when it shows are unchanged); this only dresses it.

const KINDS: Dictionary[String, Color] = {
	"wait": Color("ffc233"),
	"error": Color("ff7a70"),
	"info": Color("4aa0ff"),
}
## Words that mark a problem rather than something in progress.
const PROBLEM_WORDS: PackedStringArray = ["can't", "cannot", "lost", "no room", "ended", "full", "already", "isn't", "failed", "enter ", "need", "turn on"]


## The kind of a status text: `wait` for a line that ends in an ellipsis, `error` for a problem, else `info`.
static func kind_of(message: String) -> String:
	var lower: String = message.to_lower()
	if message.ends_with("...") or message.ends_with("…"):
		return "wait"
	if lower.contains("preview"):
		return "info"
	for word: String in PROBLEM_WORDS:
		if lower.contains(word):
			return "error"
	return "info"


## Dresses `label` as a strip of `tone` (wait, error or info).
static func style(label: Label, tone: String) -> void:
	var color: Color = KINDS.get(tone, KINDS["info"]) as Color
	var box: StyleBoxFlat = StyleBoxFlat.new()
	box.bg_color = Color(color, 0.16)
	box.border_color = Color(color, 0.85)
	box.set_border_width_all(2)
	box.set_corner_radius_all(UiSkin.RADIUS_M)
	box.anti_aliasing = true
	box.content_margin_left = 18.0
	box.content_margin_right = 18.0
	box.content_margin_top = 10.0
	box.content_margin_bottom = 10.0
	label.add_theme_stylebox_override("normal", box)
	label.add_theme_color_override("font_color", KalyeahTheme.TEXT)
	label.add_theme_constant_override("outline_size", 0)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


## A new strip label showing `message` (the kind comes from the text unless `tone` is given).
static func strip(message: String, tone: String = "") -> Label:
	var label: Label = Label.new()
	label.name = "Strip"
	label.text = message
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", UiMetrics.DETAIL)
	style(label, tone if not tone.is_empty() else kind_of(message))
	return label
