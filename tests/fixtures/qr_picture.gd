extends RefCounted
## Test pictures of a QR code as a phone camera would see it (QrReader's tests and the scan sheet's fake camera).


## `code` drawn into a `side` x `side` picture: the code (with its 4-module light margin) fills `fill` of the side, turned by
## `degrees`, its far edge pulled in by `tilt` (a phone held at an angle), lit brighter from the left by `gradient`.
static func draw(code: QrCode, side: int, fill: float, degrees: float = 0.0, tilt: float = 0.0, gradient: float = 0.0, mirror: bool = false) -> Image:
	var n: int = code.size + 8
	var half: float = side * fill / 2.0
	var centre: Vector2 = Vector2(side, side) / 2.0
	var corners: Array[Vector2] = [Vector2(-half, -half), Vector2(half, -half), Vector2(half * (1.0 - tilt), half), Vector2(-half * (1.0 - tilt), half)]
	var placed: Array[Vector2] = []
	for corner: Vector2 in corners:
		placed.append(centre + corner.rotated(deg_to_rad(degrees)))
	var square: Array[Vector2] = [Vector2(0, 0), Vector2(n, 0), Vector2(n, n), Vector2(0, n)]
	var to_grid: PackedFloat64Array = QrReader._quad_to_quad(placed, square)
	var data: PackedByteArray = PackedByteArray()
	data.resize(side * side)
	for y: int in side:
		for x: int in side:
			var at: Vector2 = QrReader._apply(to_grid, Vector2(x + 0.5, y + 0.5))
			var light: float = 200.0 - gradient * 120.0 * x / side
			var mx: int = floori(at.x) - 4
			var my: int = floori(at.y) - 4
			if mirror:
				mx = code.size - 1 - mx
			var dark: bool = mx >= 0 and my >= 0 and mx < code.size and my < code.size and code.is_dark(mx, my)
			data[y * side + x] = clampi(roundi(light * (0.2 if dark else 1.0)) + 30, 0, 255)
	return Image.create_from_data(side, side, false, Image.FORMAT_L8, data)
