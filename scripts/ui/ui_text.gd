class_name UiText
extends RefCounted
## Words that are drawn rather than set on a Label or a Button (a weapon's tag, the status over a kid, an arrow's label).
## Godot translates a Label's or Button's text by itself when it is a key of a loaded translation; text that is drawn
## goes through `t()`. The keys are the English strings (data/i18n/strings.csv, built by tools/i18n/extract.py).


## `key` in the language the player chose (the English itself when there is no translation).
static func t(key: String) -> String:
	return TranslationServer.translate(key)


## A sentence with places in it ("Best of %d"): the translated sentence with `args` put in.
static func f(key: String, args: Array) -> String:
	return t(key) % args
