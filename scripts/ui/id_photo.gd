class_name IdPhoto
extends RefCounted
## The player's ID photo on the name tag (owner): a square picture taken with the phone's camera (`IdCameraSheet`). It is kept only
## on this phone, in its own file, and never sent anywhere: it is not in the profile file, not online and not over the hotspot.

const PATH: String = "user://id_photo.png"
## The saved picture's side in pixels (a square, cropped from the middle of what the camera saw).
const SIZE: int = 256

## Where the photo is kept (tests point it elsewhere).
static var path: String = PATH


## The saved photo, or null when there is none.
static func texture() -> Texture2D:
	if not FileAccess.file_exists(path):
		return null
	var image: Image = Image.load_from_file(path)
	if image == null or image.is_empty():
		return null
	return ImageTexture.create_from_image(image)


static func exists() -> bool:
	return FileAccess.file_exists(path)


## Keeps the middle square of `image`, `SIZE` pixels a side, as the photo.
static func save(image: Image) -> Error:
	if image == null or image.is_empty():
		return ERR_INVALID_PARAMETER
	var picture: Image = image.duplicate() as Image
	picture.convert(Image.FORMAT_RGB8)
	var side: int = mini(picture.get_width(), picture.get_height())
	picture = picture.get_region(Rect2i((picture.get_width() - side) / 2, (picture.get_height() - side) / 2, side, side))
	picture.resize(SIZE, SIZE, Image.INTERPOLATE_BILINEAR)
	return picture.save_png(path)


static func remove() -> void:
	if exists():
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path))


## Whether this kind of device can have a camera Godot reads (`CameraServer`: Android, macOS, Linux, Windows). A phone may still have
## none, or the player may refuse it: the camera sheet says so.
static func camera_possible() -> bool:
	return OS.get_name() in ["Android", "macOS", "Linux", "Windows"]
