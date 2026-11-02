class_name QrReader
extends RefCounted
## Reads a QR code (versions 1 to 6, the ones `QrCode` makes) from a camera picture, in plain GDScript. Steps: each pixel dark or
## light against the brightness around it; the three finder squares by their 1:1:3:1:1 stripes; the grid between them (with the
## alignment square, a perspective fit); the format bits; the data, Reed-Solomon-corrected. A mirrored picture (a front camera)
## is read too. Returns the text, or "" when there is no code it can read.

## The picture is scaled down to this side at most before reading (a phone camera's frame is far bigger than a room code needs).
const MAX_SIDE: int = 360
## The brightness around a pixel is the picture's average over blocks of about 1/AREA_BLOCKS of its side (to 1/2 that): wider than
## a finder square of a code that fills the picture, so its dark centre never counts as light.
const AREA_BLOCKS: int = 5
## How much darker than its surroundings a pixel must be to count as dark (0..255): keeps flat paper and a blank screen light.
const DARK_MARGIN: int = 6
## How many modules either way the grid's fourth corner is looked for when the finders alone do not frame it.
const SEARCH_MODULES: int = 2
## Where in a module (0..1 each way) it is sampled.
const SAMPLE_POINTS: Array[Vector2] = [Vector2(0.5, 0.5), Vector2(0.3, 0.3), Vector2(0.7, 0.3), Vector2(0.3, 0.7), Vector2(0.7, 0.7)]
## The middle alone: the corner search tries many framings, so each is read quickly.
const CENTRE_POINT: Array[Vector2] = [Vector2(0.5, 0.5)]
## Rows a finder candidate must be found on before it is tried.
const MIN_CONFIRMATIONS: int = 1
## A read gives up after this long (a busy picture has many square-looking spots), so the camera preview never stalls for long.
const BUDGET_MSEC: int = 150

var _width: int = 0
var _height: int = 0
## 1 = dark, row by row.
var _bits: PackedByteArray
var _deadline: int = 0


## The text of the QR code in `picture`, or "".
static func read(picture: Image) -> String:
	if picture == null or picture.is_empty():
		return ""
	var reader: QrReader = QrReader.new()
	reader._binarize(picture)
	return reader._read()


## Tests: read an already dark/light grid.
static func read_bits(bits: PackedByteArray, width: int, height: int) -> String:
	var reader: QrReader = QrReader.new()
	reader._bits = bits
	reader._width = width
	reader._height = height
	return reader._read()


func _binarize(picture: Image) -> void:
	var image: Image = picture.duplicate() as Image
	if image.is_compressed():
		image.decompress()
	var longest: int = maxi(image.get_width(), image.get_height())
	if longest > MAX_SIDE:
		var factor: float = float(MAX_SIDE) / float(longest)
		image.resize(maxi(1, roundi(image.get_width() * factor)), maxi(1, roundi(image.get_height() * factor)), Image.INTERPOLATE_BILINEAR)
	image.convert(Image.FORMAT_L8)
	_width = image.get_width()
	_height = image.get_height()
	# the surroundings: the mipmap whose side is still at least AREA_BLOCKS (mipmaps average whole blocks), stretched back smoothly
	var averages: Image = image.duplicate() as Image
	averages.generate_mipmaps()
	var level: int = 0
	var block_width: int = _width
	var block_height: int = _height
	while mini(block_width, block_height) / 2 >= AREA_BLOCKS and level < averages.get_mipmap_count():
		block_width = maxi(1, block_width / 2)
		block_height = maxi(1, block_height / 2)
		level += 1
	var offset: int = averages.get_mipmap_offset(level)
	var area: Image = Image.create_from_data(block_width, block_height, false, Image.FORMAT_L8,
		averages.get_data().slice(offset, offset + block_width * block_height))
	area.resize(_width, _height, Image.INTERPOLATE_BILINEAR)
	var luma: PackedByteArray = image.get_data()
	var around: PackedByteArray = area.get_data()
	_bits = PackedByteArray()
	_bits.resize(luma.size())
	for index: int in luma.size():
		if luma[index] + DARK_MARGIN < around[index]:
			_bits[index] = 1


func _dark(x: int, y: int) -> bool:
	return x >= 0 and y >= 0 and x < _width and y < _height and _bits[y * _width + x] == 1


func _read() -> String:
	_deadline = Time.get_ticks_msec() + BUDGET_MSEC
	var centres: Array[Vector3] = _finders()
	if centres.size() < 3:
		return ""
	# the most-confirmed candidates first, then every three of the best few
	centres.sort_custom(func(a: Vector3, b: Vector3) -> bool: return a.z > b.z)
	var best: Array[Vector3] = centres.slice(0, mini(centres.size(), 6))
	for i: int in best.size():
		for j: int in range(i + 1, best.size()):
			for k: int in range(j + 1, best.size()):
				var text: String = _read_three(best[i], best[j], best[k])
				if not text.is_empty():
					return text
	return ""


## Finder candidates as (x, y, rows found on * 1000 + module size).
func _finders() -> Array[Vector3]:
	var found: Array[Vector3] = []
	var counts: Array[int] = []
	var step: int = 1 if _height < 200 else 2
	for y: int in range(0, _height, step):
		var runs: PackedInt32Array = PackedInt32Array([0, 0, 0, 0, 0])
		var state: int = 0
		for x: int in _width + 1:
			var dark: bool = x < _width and _bits[y * _width + x] == 1
			if dark == (state % 2 == 0):
				runs[state] += 1
				continue
			if state < 4:
				if state == 0 and runs[0] == 0:
					# waiting for the first dark run
					continue
				state += 1
				runs[state] = 1
				continue
			# five runs done: dark light dark light dark
			if _ratio_ok(runs):
				var centre_x: float = x - runs[4] - runs[3] - runs[2] / 2.0
				var centre: Vector3 = _confirm(centre_x, float(y) + 0.5, runs)
				if centre.z > 0.0:
					_add(found, counts, centre)
			# slide on by two runs (the last dark run starts the next try)
			runs[0] = runs[2]
			runs[1] = runs[3]
			runs[2] = runs[4]
			runs[3] = 1
			runs[4] = 0
			state = 3
	# (x, y, confirmations * 1000 + module size): _read tries the most-confirmed first
	var by_count: Array[Vector3] = []
	for index: int in found.size():
		if counts[index] >= MIN_CONFIRMATIONS:
			by_count.append(Vector3(found[index].x, found[index].y, float(counts[index]) * 1000.0 + found[index].z))
	return by_count


static func _ratio_ok(runs: PackedInt32Array) -> bool:
	var total: int = 0
	for run: int in runs:
		if run == 0:
			return false
		total += run
	if total < 7:
		return false
	var module: float = total / 7.0
	var slack: float = module / 1.5
	return absf(module - runs[0]) < slack and absf(module - runs[1]) < slack and absf(3.0 * module - runs[2]) < 3.0 * slack \
		and absf(module - runs[3]) < slack and absf(module - runs[4]) < slack


## The stripes cross-checked down the column, then along the row again: the centre and the module size, or z = 0.
func _confirm(centre_x: float, row: float, runs: PackedInt32Array) -> Vector3:
	var total: int = 0
	for run: int in runs:
		total += run
	var y: float = _cross(floori(centre_x), floori(row), Vector2i(0, 1), total)
	if y < 0.0:
		return Vector3.ZERO
	var x: float = _cross(floori(centre_x), floori(y), Vector2i(1, 0), total)
	if x < 0.0:
		return Vector3.ZERO
	var y2: float = _cross(floori(x), floori(y), Vector2i(0, 1), total)
	if y2 < 0.0:
		return Vector3.ZERO
	return Vector3(x, y2, total / 7.0)


## Walks out both ways from a dark centre along `axis`: the 1:1:3:1:1 stripes there, of about `expected` pixels in all, give
## the centre along that axis (pixel edges count from 0), or -1.
func _cross(x: int, y: int, axis: Vector2i, expected: int) -> float:
	if not _dark(x, y):
		return -1.0
	var limit: int = expected * 2
	var centre: Vector2i = Vector2i(x, y)
	var back_middle: int = _run(centre, -axis, true, limit)
	var at: Vector2i = centre - axis * back_middle
	var back_light: int = _run(at, -axis, false, limit)
	at -= axis * back_light
	var back_dark: int = _run(at, -axis, true, limit)
	var forward_middle: int = _run(centre + axis, axis, true, limit)
	at = centre + axis * (1 + forward_middle)
	var forward_light: int = _run(at, axis, false, limit)
	at += axis * forward_light
	var forward_dark: int = _run(at, axis, true, limit)
	var runs: PackedInt32Array = PackedInt32Array([back_dark, back_light, back_middle + forward_middle, forward_light, forward_dark])
	var total: int = 0
	for run: int in runs:
		total += run
	if absi(total - expected) * 5 >= expected * 2 or not _ratio_ok(runs):
		return -1.0
	var along: int = x if axis.x != 0 else y
	return along + 1.0 + (forward_middle - back_middle) / 2.0


## How many pixels from `from` on, a step of `step` each, are dark (`want`) or light, up to `limit`.
func _run(from: Vector2i, step: Vector2i, want: bool, limit: int) -> int:
	var count: int = 0
	var at: Vector2i = from
	while count < limit and _inside(at) and (_bits[at.y * _width + at.x] == 1) == want:
		count += 1
		at += step
	return count


func _inside(at: Vector2i) -> bool:
	return at.x >= 0 and at.y >= 0 and at.x < _width and at.y < _height


func _add(found: Array[Vector3], counts: Array[int], centre: Vector3) -> void:
	for index: int in found.size():
		var known: Vector3 = found[index]
		if absf(known.x - centre.x) <= centre.z * 2.0 and absf(known.y - centre.y) <= centre.z * 2.0 \
				and absf(known.z - centre.z) <= maxf(1.0, known.z * 0.5):
			var n: float = float(counts[index])
			found[index] = Vector3((known.x * n + centre.x) / (n + 1.0), (known.y * n + centre.y) / (n + 1.0), (known.z * n + centre.z) / (n + 1.0))
			counts[index] += 1
			return
	found.append(centre)
	counts.append(1)


## Three finder candidates (x, y, count * 1000 + module size): tries to read the code they frame, plain and mirrored.
func _read_three(a: Vector3, b: Vector3, c: Vector3) -> String:
	var module_a: float = fmod(a.z, 1000.0)
	var module_b: float = fmod(b.z, 1000.0)
	var module_c: float = fmod(c.z, 1000.0)
	var smallest: float = minf(module_a, minf(module_b, module_c))
	if maxf(module_a, maxf(module_b, module_c)) > smallest * 2.0:
		return ""
	var pa: Vector2 = Vector2(a.x, a.y)
	var pb: Vector2 = Vector2(b.x, b.y)
	var pc: Vector2 = Vector2(c.x, c.y)
	# the top-left finder is the one across from the longest side
	var ab: float = pa.distance_to(pb)
	var bc: float = pb.distance_to(pc)
	var ac: float = pa.distance_to(pc)
	var top_left: Vector2 = pc
	var one: Vector2 = pa
	var two: Vector2 = pb
	if bc >= ab and bc >= ac:
		top_left = pa
		one = pb
		two = pc
	elif ac >= ab and ac >= bc:
		top_left = pb
		one = pa
		two = pc
	# the two legs should be near-equal and near-square to each other
	var leg_one: Vector2 = one - top_left
	var leg_two: Vector2 = two - top_left
	if leg_one.length() < 1.0 or leg_two.length() < 1.0:
		return ""
	var legs: float = leg_one.length() / leg_two.length()
	if legs < 0.6 or legs > 1.67 or absf(leg_one.normalized().dot(leg_two.normalized())) > 0.5:
		return ""
	var top_right: Vector2 = one
	var bottom_left: Vector2 = two
	if leg_one.cross(leg_two) < 0.0:
		top_right = two
		bottom_left = one
	# the module size measured along the code's own edges (a turned code's stripes are longer along a pixel row)
	var module: float = (_module_along(top_left, top_right) + _module_along(top_right, top_left)
		+ _module_along(top_left, bottom_left) + _module_along(bottom_left, top_left)) / 4.0
	if module <= 0.0:
		return ""
	var across: float = (top_left.distance_to(top_right) + top_left.distance_to(bottom_left)) / 2.0 / module
	var side: int = roundi(across) + 7
	match side % 4:
		0:
			side += 1
		2:
			side -= 1
		3:
			side -= 2
	var v: int = (side - 17) / 4
	if v < 1 or v > QrCode.MAX_VERSION:
		return ""
	for flip: int in 2:
		var right: Vector2 = top_right if flip == 0 else bottom_left
		var down: Vector2 = bottom_left if flip == 0 else top_right
		var text: String = _read_framed(top_left, right, down, v, module)
		if not text.is_empty():
			return text
	return ""


## A finder's module size along the line from its centre `from` towards `to`: its centre square, light ring and dark ring end
## 3.5 modules out, both ways (7 in all). 0 when the stripes are not there.
func _module_along(from: Vector2, to: Vector2) -> float:
	var direction: Vector2 = (to - from).normalized()
	var total: float = 0.0
	for sign: float in [1.0, -1.0]:
		var step: Vector2 = direction * sign
		var at: Vector2 = from
		var travelled: float = 0.0
		var limit: float = from.distance_to(to) / 2.0
		for want: bool in [true, false, true]:
			# a one-pixel speck of the other shade (camera grain) does not end the stripe
			while travelled < limit and (_dark(floori(at.x), floori(at.y)) == want or _dark(floori(at.x + step.x), floori(at.y + step.y)) == want):
				at += step
				travelled += 1.0
		if travelled >= limit:
			return 0.0
		# the walk starts mid-pixel and stops on the first pixel past the ring: half a pixel over each way
		total += travelled - 0.5
	return total / 7.0


## The bottom-right point of the grid: the alignment square (versions 2 and up) or, without one, where the finders put it. The
## guess of version 1 (and of a missed alignment square) is off on a tilted picture, so the points around it are tried too,
## nearest first, half a module apart.
func _read_framed(top_left: Vector2, top_right: Vector2, bottom_left: Vector2, v: int, module: float) -> String:
	var n: int = QrCode.side(v)
	var far: float = n - 3.5
	var corner_at: float = far if v == 1 else n - 6.5
	var guess: Vector2 = top_left + (top_right - top_left + bottom_left - top_left) * ((corner_at - 3.5) / (far - 3.5))
	if v > 1:
		var found: Vector2 = _alignment(guess, module)
		if found != guess:
			var text: String = _decode_framed(_frame(top_left, top_right, bottom_left, found, corner_at, v), v)
			if not text.is_empty():
				return text
	var across: Vector2 = (top_right - top_left).normalized() * module
	var down: Vector2 = (bottom_left - top_left).normalized() * module
	for offset: Vector2 in _search_offsets():
		var corner: Vector2 = guess + across * offset.x + down * offset.y
		var text: String = _decode_framed(_frame(top_left, top_right, bottom_left, corner, corner_at, v), v, CENTRE_POINT)
		if not text.is_empty():
			return text
	return ""


## Half-module steps around the corner guess, nearest first (the guess itself is first).
static func _search_offsets() -> Array[Vector2]:
	var offsets: Array[Vector2] = []
	for dy: int in range(-SEARCH_MODULES * 2, SEARCH_MODULES * 2 + 1):
		for dx: int in range(-SEARCH_MODULES * 2, SEARCH_MODULES * 2 + 1):
			offsets.append(Vector2(dx, dy) / 2.0)
	offsets.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.length_squared() < b.length_squared())
	return offsets


## The perspective that puts the finder centres and `corner` (grid point (`corner_at`, `corner_at`)) where they are in the picture.
static func _frame(top_left: Vector2, top_right: Vector2, bottom_left: Vector2, corner: Vector2, corner_at: float, v: int) -> PackedFloat64Array:
	var far: float = QrCode.side(v) - 3.5
	var grid_points: Array[Vector2] = [Vector2(3.5, 3.5), Vector2(far, 3.5), Vector2(corner_at, corner_at), Vector2(3.5, far)]
	var picture_points: Array[Vector2] = [top_left, top_right, corner, bottom_left]
	return _quad_to_quad(grid_points, picture_points)


## Whether module (x, y) is dark: most of five points around its middle, so grain or an edge pixel does not flip it.
func _module_dark(transform: PackedFloat64Array, x: int, y: int, points: Array[Vector2]) -> bool:
	var votes: int = 0
	for offset: Vector2 in points:
		var point: Vector2 = _apply(transform, Vector2(x, y) + offset)
		if _dark(floori(point.x), floori(point.y)):
			votes += 1
	return votes * 2 > points.size()


## The text in the grid `transform` frames, or "". The 30 format modules are read first: most wrong framings stop there.
func _decode_framed(transform: PackedFloat64Array, v: int, points: Array[Vector2] = SAMPLE_POINTS) -> String:
	if Time.get_ticks_msec() > _deadline:
		return ""
	var n: int = QrCode.side(v)
	var grid: PackedByteArray = PackedByteArray()
	grid.resize(n * n)
	for copy: Array in QrCode.format_positions(n):
		for at: Vector2i in copy:
			grid[at.y * n + at.x] = 1 if _module_dark(transform, at.x, at.y, points) else 0
	if _format(grid, n).x < 0:
		return ""
	for y: int in n:
		for x: int in n:
			grid[y * n + x] = 1 if _module_dark(transform, x, y, points) else 0
	return _decode_grid(grid, v)


## The alignment square near `guess`: a dark centre ringed light then dark. The guess itself when none is found.
func _alignment(guess: Vector2, module: float) -> Vector2:
	var reach: int = ceili(module * 4.0)
	var best: Vector2 = guess
	var best_distance: float = INF
	var step: int = maxi(1, floori(module / 2.0))
	for dy: int in range(-reach, reach + 1, step):
		for dx: int in range(-reach, reach + 1, step):
			var x: int = roundi(guess.x) + dx
			var y: int = roundi(guess.y) + dy
			if not _dark(x, y):
				continue
			var cx: float = _small_cross(x, y, Vector2i(1, 0), module)
			if cx < 0.0:
				continue
			var cy: float = _small_cross(roundi(cx), y, Vector2i(0, 1), module)
			if cy < 0.0:
				continue
			var point: Vector2 = Vector2(cx, cy)
			var distance: float = point.distance_squared_to(guess)
			if distance < best_distance:
				best_distance = distance
				best = point
	return best


## The centre along `axis` of a one-module dark run between one-module light runs, or -1.
func _small_cross(x: int, y: int, axis: Vector2i, module: float) -> float:
	var limit: int = ceili(module * 2.0)
	var start: Vector2i = Vector2i(x, y)
	var low: int = 0
	while _dark(start.x - axis.x, start.y - axis.y) and low < limit:
		start -= axis
		low += 1
	var high: Vector2i = Vector2i(x, y)
	var count: int = low
	while _dark(high.x + axis.x, high.y + axis.y) and count < limit:
		high += axis
		count += 1
	var dark_run: int = count + 1
	if absf(dark_run - module) > module * 0.6:
		return -1.0
	for direction: int in [-1, 1]:
		var edge: Vector2i = (start if direction < 0 else high) + axis * direction
		var light: int = 0
		while not _dark(edge.x, edge.y) and _inside(edge) and light < limit:
			edge += axis * direction
			light += 1
		if absf(light - module) > module * 0.6 or not _dark(edge.x, edge.y):
			return -1.0
	var a: int = start.x if axis.x != 0 else start.y
	var b: int = high.x if axis.x != 0 else high.y
	return (a + b) / 2.0 + 0.5


## The perspective that sends the four `from` points to the four `to` points (as a 3x3, row by row).
static func _quad_to_quad(from: Array[Vector2], to: Array[Vector2]) -> PackedFloat64Array:
	var square_to_from: PackedFloat64Array = _square_to_quad(from)
	var square_to_to: PackedFloat64Array = _square_to_quad(to)
	return _times(square_to_to, _adjoint(square_to_from))


static func _square_to_quad(q: Array[Vector2]) -> PackedFloat64Array:
	var dx3: float = q[0].x - q[1].x + q[2].x - q[3].x
	var dy3: float = q[0].y - q[1].y + q[2].y - q[3].y
	var dx1: float = q[1].x - q[2].x
	var dx2: float = q[3].x - q[2].x
	var dy1: float = q[1].y - q[2].y
	var dy2: float = q[3].y - q[2].y
	var denominator: float = dx1 * dy2 - dx2 * dy1
	var g: float = (dx3 * dy2 - dx2 * dy3) / denominator
	var h: float = (dx1 * dy3 - dx3 * dy1) / denominator
	return PackedFloat64Array([q[1].x - q[0].x + g * q[1].x, q[3].x - q[0].x + h * q[3].x, q[0].x,
		q[1].y - q[0].y + g * q[1].y, q[3].y - q[0].y + h * q[3].y, q[0].y, g, h, 1.0])


static func _adjoint(m: PackedFloat64Array) -> PackedFloat64Array:
	return PackedFloat64Array([
		m[4] * m[8] - m[5] * m[7], m[2] * m[7] - m[1] * m[8], m[1] * m[5] - m[2] * m[4],
		m[5] * m[6] - m[3] * m[8], m[0] * m[8] - m[2] * m[6], m[2] * m[3] - m[0] * m[5],
		m[3] * m[7] - m[4] * m[6], m[1] * m[6] - m[0] * m[7], m[0] * m[4] - m[1] * m[3]])


static func _times(a: PackedFloat64Array, b: PackedFloat64Array) -> PackedFloat64Array:
	var out: PackedFloat64Array = PackedFloat64Array()
	out.resize(9)
	for row: int in 3:
		for column: int in 3:
			var sum: float = 0.0
			for k: int in 3:
				sum += a[row * 3 + k] * b[k * 3 + column]
			out[row * 3 + column] = sum
	return out


static func _apply(m: PackedFloat64Array, p: Vector2) -> Vector2:
	var w: float = m[6] * p.x + m[7] * p.y + m[8]
	return Vector2((m[0] * p.x + m[1] * p.y + m[2]) / w, (m[3] * p.x + m[4] * p.y + m[5]) / w)


## The text in a sampled grid of version `v`, or "".
static func _decode_grid(grid: PackedByteArray, v: int) -> String:
	var n: int = QrCode.side(v)
	var format: Vector2i = _format(grid, n)
	if format.x < 0:
		return ""
	var level: QrCode.Ecc = format.x as QrCode.Ecc
	var unmasked: PackedByteArray = grid.duplicate()
	QrCode.apply_mask(unmasked, QrCode.function_map(v), n, format.y)
	var order: PackedInt32Array = QrCode.data_order(v)
	var entry: Array = QrCode.BLOCKS[v][level] as Array
	var ecc_count: int = entry[0] as int
	var counts: Array = entry[1] as Array
	var total: int = 0
	for count: int in counts:
		total += (count as int) + ecc_count
	var codewords: PackedByteArray = PackedByteArray()
	codewords.resize(total)
	for index: int in total * 8:
		codewords[index >> 3] |= unmasked[order[index]] << (7 - (index & 7))
	# undo the interleaving: data columns, then error columns
	var blocks: Array[PackedByteArray] = []
	for count: int in counts:
		var block: PackedByteArray = PackedByteArray()
		block.resize((count as int) + ecc_count)
		blocks.append(block)
	var at: int = 0
	var longest: int = counts[counts.size() - 1] as int
	for index: int in longest:
		for b: int in blocks.size():
			if index < (counts[b] as int):
				blocks[b][index] = codewords[at]
				at += 1
	for index: int in ecc_count:
		for b: int in blocks.size():
			blocks[b][(counts[b] as int) + index] = codewords[at]
			at += 1
	var data: PackedByteArray = PackedByteArray()
	for b: int in blocks.size():
		if not QrField.correct(blocks[b], ecc_count):
			return ""
		data.append_array(blocks[b].slice(0, counts[b] as int))
	return _parse(data, v)


## (level, mask) from the nearer of the two format copies, or (-1, -1).
static func _format(grid: PackedByteArray, n: int) -> Vector2i:
	var best: Vector2i = Vector2i(-1, -1)
	var best_distance: int = 4
	for copy: Array in QrCode.format_positions(n):
		var bits: int = 0
		for i: int in 15:
			var at: Vector2i = copy[i] as Vector2i
			bits |= grid[at.y * n + at.x] << i
		for level: int in 4:
			for which: int in 8:
				var distance: int = _ones(bits ^ QrCode.format_bits(level as QrCode.Ecc, which))
				if distance < best_distance:
					best_distance = distance
					best = Vector2i(level, which)
	return best


static func _ones(value: int) -> int:
	var count: int = 0
	while value != 0:
		count += value & 1
		value >>= 1
	return count


static func _parse(data: PackedByteArray, v: int) -> String:
	var reader: Array[int] = [0]
	var bits: int = data.size() * 8
	var text: String = ""
	var bytes: PackedByteArray = PackedByteArray()
	while bits - reader[0] >= 4:
		var mode: int = _take(data, reader, 4)
		if mode == 0:
			break
		if mode == 7:
			# ECI: a character set we read as UTF-8 anyway
			_take(data, reader, 8)
			continue
		if mode != QrCode.MODE_NUMERIC and mode != QrCode.MODE_ALPHANUMERIC and mode != QrCode.MODE_BYTE:
			return ""
		var length_bits: int = QrCode.count_bits(mode, v)
		if bits - reader[0] < length_bits:
			return ""
		var count: int = _take(data, reader, length_bits)
		if mode == QrCode.MODE_BYTE:
			if bits - reader[0] < count * 8:
				return ""
			for _i: int in count:
				bytes.append(_take(data, reader, 8))
			continue
		text += bytes.get_string_from_utf8()
		bytes = PackedByteArray()
		if mode == QrCode.MODE_ALPHANUMERIC:
			var left: int = count
			while left >= 2:
				if bits - reader[0] < 11:
					return ""
				var pair: int = _take(data, reader, 11)
				if pair >= 45 * 45:
					return ""
				text += QrCode.ALPHANUMERIC[pair / 45] + QrCode.ALPHANUMERIC[pair % 45]
				left -= 2
			if left == 1:
				if bits - reader[0] < 6:
					return ""
				var single: int = _take(data, reader, 6)
				if single >= 45:
					return ""
				text += QrCode.ALPHANUMERIC[single]
		else:
			var left: int = count
			while left > 0:
				var digits: int = mini(left, 3)
				var width: int = [0, 4, 7, 10][digits] as int
				if bits - reader[0] < width:
					return ""
				text += str(_take(data, reader, width)).pad_zeros(digits)
				left -= digits
	return text + bytes.get_string_from_utf8()


static func _take(data: PackedByteArray, reader: Array[int], count: int) -> int:
	var value: int = 0
	for _i: int in count:
		var at: int = reader[0]
		value = (value << 1) | ((data[at >> 3] >> (7 - (at & 7))) & 1)
		reader[0] += 1
	return value
