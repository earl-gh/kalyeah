class_name IdPhotoPicker
extends RefCounted
## "Pick from gallery" for the ID photo (owner, 2026-11-03): the phone's own picture picker (Android's document picker, through Godot's
## native file dialog, so the game needs no permission and no plugin) hands back a picture; `keep` reads it (PNG, JPEG or WebP), turns a
## phone photo the right way up (its EXIF orientation) and keeps the middle square as the ID photo (`IdPhoto`), on this phone only.

const TITLE: String = "Choose a photo"
## The picker lists pictures only.
const FILTERS: PackedStringArray = ["image/*"]
## EXIF orientation values (what a viewer must do to show the picture upright).
const NORMAL: int = 1


## Tests and previews say a gallery is there (true) or not (false) whatever the machine has; null leaves it to the machine.
static var available_override: Variant = null


## Whether this device has a native file picker to open the gallery with (Android, macOS, Windows, Linux with a portal).
static func available() -> bool:
	if available_override is bool:
		return available_override as bool
	return DisplayServer.has_feature(DisplayServer.FEATURE_NATIVE_DIALOG_FILE)


## Opens the gallery. `on_done(result)` runs once with OK (the photo is kept), ERR_SKIP (the player closed the picker) or the error that
## kept the picture from being read (`ERR_FILE_CANT_READ`, `ERR_FILE_UNRECOGNIZED`). Returns the error of showing the picker itself.
static func pick(on_done: Callable) -> Error:
	var answer: Callable = func(ok: bool, paths: PackedStringArray, _filter: int) -> void:
		if not ok or paths.is_empty():
			on_done.call(ERR_SKIP)
			return
		on_done.call(keep(paths[0]))
	return DisplayServer.file_dialog_show(TITLE, "", "", false, DisplayServer.FILE_DIALOG_MODE_OPEN_FILE, FILTERS, answer)


## Reads the picture at `path` (a file or an Android `content://` address) and keeps it as the ID photo.
static func keep(path: String) -> Error:
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(path)
	if bytes.is_empty():
		return ERR_FILE_CANT_READ
	var image: Image = decode(bytes)
	if image == null:
		return ERR_FILE_UNRECOGNIZED
	return IdPhoto.save(image)


## The picture in `bytes`, upright, or null when it is not a PNG, JPEG or WebP (or cannot be decoded). The format is read from the first
## bytes, not from a name: a `content://` address has no extension.
static func decode(bytes: PackedByteArray) -> Image:
	var image: Image = Image.new()
	var result: Error = ERR_FILE_UNRECOGNIZED
	var jpeg: bool = false
	if bytes.size() > 8 and bytes[0] == 0x89 and bytes[1] == 0x50 and bytes[2] == 0x4E and bytes[3] == 0x47:
		result = image.load_png_from_buffer(bytes)
	elif bytes.size() > 3 and bytes[0] == 0xFF and bytes[1] == 0xD8:
		jpeg = true
		result = image.load_jpg_from_buffer(bytes)
	elif bytes.size() > 12 and bytes.slice(0, 4).get_string_from_ascii() == "RIFF" and bytes.slice(8, 12).get_string_from_ascii() == "WEBP":
		result = image.load_webp_from_buffer(bytes)
	if result != OK or image.is_empty():
		return null
	if jpeg:
		upright(image, exif_orientation(bytes))
	return image


## Turns `image` (in place) the way an EXIF `orientation` says: 3 upside down, 6 and 8 a quarter turn, 2, 4, 5 and 7 mirrored.
static func upright(image: Image, orientation: int) -> void:
	match orientation:
		2:
			image.flip_x()
		3:
			image.rotate_180()
		4:
			image.flip_y()
		5:
			image.rotate_90(CLOCKWISE)
			image.flip_x()
		6:
			image.rotate_90(CLOCKWISE)
		7:
			image.rotate_90(COUNTERCLOCKWISE)
			image.flip_x()
		8:
			image.rotate_90(COUNTERCLOCKWISE)


## The orientation (1 to 8) a JPEG's EXIF data asks for; 1 (as stored) when there is none or the data is not understood.
static func exif_orientation(bytes: PackedByteArray) -> int:
	var at: int = 2
	while at + 4 <= bytes.size():
		if bytes[at] != 0xFF:
			return NORMAL
		var marker: int = bytes[at + 1]
		if marker == 0xFF:
			at += 1  # a fill byte
			continue
		if marker == 0xD9 or marker == 0xDA:
			return NORMAL  # the end, or the picture's own data: no EXIF after this
		var length: int = (bytes[at + 2] << 8) | bytes[at + 3]
		if length < 2:
			return NORMAL
		if marker == 0xE1 and at + 10 <= bytes.size() and bytes.slice(at + 4, at + 10).get_string_from_ascii() == "Exif":
			return _tiff_orientation(bytes, at + 10, at + 2 + length)
		at += 2 + length
	return NORMAL


## The orientation tag (0x0112) in the first directory of the TIFF block that starts at `start`, `end` being where the block ends.
static func _tiff_orientation(bytes: PackedByteArray, start: int, end: int) -> int:
	end = mini(end, bytes.size())
	if start + 8 > end:
		return NORMAL
	var little: bool
	if bytes[start] == 0x49 and bytes[start + 1] == 0x49:
		little = true
	elif bytes[start] == 0x4D and bytes[start + 1] == 0x4D:
		little = false
	else:
		return NORMAL
	var directory: int = start + _u32(bytes, start + 4, little)
	if directory < start or directory + 2 > end:
		return NORMAL
	var count: int = _u16(bytes, directory, little)
	for i: int in count:
		var entry: int = directory + 2 + i * 12
		if entry + 12 > end:
			return NORMAL
		if _u16(bytes, entry, little) == 0x0112:
			var value: int = _u16(bytes, entry + 8, little)
			return value if value >= 1 and value <= 8 else NORMAL
	return NORMAL


static func _u16(bytes: PackedByteArray, at: int, little: bool) -> int:
	return (bytes[at] | (bytes[at + 1] << 8)) if little else ((bytes[at] << 8) | bytes[at + 1])


static func _u32(bytes: PackedByteArray, at: int, little: bool) -> int:
	if little:
		return bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24)
	return (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3]
