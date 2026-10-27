class_name DonateInfo
extends Resource
## What the Donate sheet shows (the envelope icon on the main screen): the thank-you words and where a donation goes. The page address
## is the owner's to fill in (`data/rules/donate_info.tres`); while it is empty the sheet says donations open soon and offers no link.

@export_multiline var thanks: String = ""
@export_multiline var grown_ups: String = ""
## The web address a donation goes to (opened in the phone's browser). Empty until the owner sets it.
@export var url: String = ""


func has_link() -> bool:
	return url.begins_with("https://")
