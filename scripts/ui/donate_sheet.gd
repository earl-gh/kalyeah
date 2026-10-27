class_name DonateSheet
extends RefCounted
## The Donate sheet (the envelope icon on the main screen): thanks for wanting to help, a note for grown-ups, and a button to the donation
## page when the owner has set one (`DonateInfo.url`). Donating is optional, gives nothing in the game, and opens the phone's browser.

const TITLE: String = "Donate"
const ICON: Texture2D = preload("res://assets/ui/mail_icon.png")
const INFO_PATH: String = "res://data/rules/donate_info.tres"
const WIDTH: float = MenuDialog.WIDTH_SHEET

## Called with the address when the Donate button is pressed (the default opens the browser); tests replace it.
static var open_url: Callable = func(url: String) -> void: OS.shell_open(url)


static func build(info: DonateInfo = null) -> MenuDialog:
	if info == null:
		info = load(INFO_PATH) as DonateInfo
	var card: MenuDialog = MenuDialog.create(TITLE, WIDTH, ICON)
	card.name = "DonateSheet"
	var heart: Label = card.add_label("♥", UiMetrics.DISPLAY, Color("d6302c"), HORIZONTAL_ALIGNMENT_CENTER)
	heart.name = "Heart"
	var thanks: Label = card.add_label(info.thanks, UiMetrics.BODY, MenuDialog.TEXT_COLOR, HORIZONTAL_ALIGNMENT_CENTER)
	thanks.name = "Thanks"
	card.add_hint(info.grown_ups).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var close: Button = MenuDialog.make_button("Close", "grey", UiMetrics.BODY)
	close.name = "Close"
	close.pressed.connect(card.close)
	if info.has_link():
		var donate: Button = MenuDialog.make_button("Donate", "orange", UiMetrics.BODY)
		donate.name = "DonateButton"
		donate.pressed.connect(func() -> void: open_url.call(info.url))
		card.add_buttons([close, donate])
	else:
		var soon: Label = card.add_label("Coming soon!", UiMetrics.DETAIL, KalyeahTheme.MODAL_TEXT_MUTED, HORIZONTAL_ALIGNMENT_CENTER)
		soon.name = "Soon"
		card.add_buttons([close])
	return card
