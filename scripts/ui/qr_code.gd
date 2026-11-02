class_name QrCode
extends RefCounted
## A QR code (ISO/IEC 18004), made in plain GDScript: no plugin. Versions 1 to 6 (21 to 41 modules a side), which is plenty
## for a room code and keeps the code easy for a phone camera to read. `QrReader` reads them back. The tables, the Reed-Solomon
## field and the layout live here so both sides share them.

enum Ecc { L, M, Q, H }

const MAX_VERSION: int = 6
const MODE_NUMERIC: int = 1
const MODE_ALPHANUMERIC: int = 2
const MODE_BYTE: int = 4
const ALPHANUMERIC: String = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:"
## The two bits each error-correction level writes into the format information.
const ECC_BITS: Array[int] = [1, 0, 3, 2]
## Per version (index 1..6) and level (L, M, Q, H): [error codewords per block, data codewords of each block].
const BLOCKS: Array = [
	[],
	[[7, [19]], [10, [16]], [13, [13]], [17, [9]]],
	[[10, [34]], [16, [28]], [22, [22]], [28, [16]]],
	[[15, [55]], [26, [44]], [18, [17, 17]], [22, [13, 13]]],
	[[20, [80]], [18, [32, 32]], [26, [24, 24]], [16, [9, 9, 9, 9]]],
	[[26, [108]], [24, [43, 43]], [18, [15, 15, 16, 16]], [22, [11, 11, 12, 12]]],
	[[18, [68, 68]], [16, [27, 27, 27, 27]], [24, [19, 19, 19, 19]], [28, [15, 15, 15, 15]]],
]

## The side, in modules (21 + 4 per version).
var size: int = 0
var version: int = 0
var ecc: Ecc = Ecc.M
var mask: int = 0
## One byte per module, row by row: 1 = dark.
var modules: PackedByteArray


## The QR code for `text` (the smallest version that holds it), or null when it is too long for version 6.
static func encode(text: String, level: Ecc = Ecc.M) -> QrCode:
	var mode: int = MODE_ALPHANUMERIC if _is_alphanumeric(text) else MODE_BYTE
	var payload: PackedByteArray = text.to_utf8_buffer()
	var count: int = text.length() if mode == MODE_ALPHANUMERIC else payload.size()
	for v: int in range(1, MAX_VERSION + 1):
		var capacity: int = data_codewords(v, level) * 8
		var bits: Array[int] = []
		_put(bits, mode, 4)
		_put(bits, count, count_bits(mode, v))
		if mode == MODE_ALPHANUMERIC:
			var index: int = 0
			while index + 1 < text.length():
				_put(bits, ALPHANUMERIC.find(text[index]) * 45 + ALPHANUMERIC.find(text[index + 1]), 11)
				index += 2
			if index < text.length():
				_put(bits, ALPHANUMERIC.find(text[index]), 6)
		else:
			for byte: int in payload:
				_put(bits, byte, 8)
		if bits.size() > capacity:
			continue
		_put(bits, 0, mini(4, capacity - bits.size()))
		while bits.size() % 8 != 0:
			bits.append(0)
		var data: PackedByteArray = PackedByteArray()
		for start: int in range(0, bits.size(), 8):
			var byte: int = 0
			for bit: int in 8:
				byte = (byte << 1) | bits[start + bit]
			data.append(byte)
		var pad: int = 0
		while data.size() < capacity / 8:
			data.append(0xEC if pad % 2 == 0 else 0x11)
			pad += 1
		return _from_data(v, level, data)
	return null


func is_dark(x: int, y: int) -> bool:
	return modules[y * size + x] == 1


static func side(v: int) -> int:
	return 17 + v * 4


static func data_codewords(v: int, level: Ecc) -> int:
	var total: int = 0
	for count: int in (BLOCKS[v][level] as Array)[1] as Array:
		total += count
	return total


static func count_bits(mode: int, _v: int) -> int:
	# versions 1 to 9
	match mode:
		MODE_NUMERIC:
			return 10
		MODE_ALPHANUMERIC:
			return 9
	return 8


static func _is_alphanumeric(text: String) -> bool:
	for character: String in text:
		if ALPHANUMERIC.find(character) < 0:
			return false
	return true


static func _put(bits: Array[int], value: int, length: int) -> void:
	for shift: int in range(length - 1, -1, -1):
		bits.append((value >> shift) & 1)


static func _from_data(v: int, level: Ecc, data: PackedByteArray) -> QrCode:
	var code: QrCode = QrCode.new()
	code.version = v
	code.ecc = level
	code.size = side(v)
	var codewords: PackedByteArray = interleave(v, level, data)
	var function: PackedByteArray = function_map(v)
	var base: PackedByteArray = PackedByteArray()
	base.resize(code.size * code.size)
	_draw_function(base, v)
	var order: PackedInt32Array = data_order(v)
	for index: int in order.size():
		if index < codewords.size() * 8:
			base[order[index]] = (codewords[index >> 3] >> (7 - (index & 7))) & 1
	var best: int = -1
	var best_score: int = 0
	for candidate: int in 8:
		var trial: PackedByteArray = base.duplicate()
		apply_mask(trial, function, code.size, candidate)
		draw_format(trial, code.size, level, candidate)
		var score: int = _penalty(trial, code.size)
		if best < 0 or score < best_score:
			best = candidate
			best_score = score
			code.modules = trial
	code.mask = best
	return code


## The data cut into blocks, each with its error codewords, interleaved as the code stores them.
static func interleave(v: int, level: Ecc, data: PackedByteArray) -> PackedByteArray:
	var entry: Array = BLOCKS[v][level] as Array
	var ecc_count: int = entry[0] as int
	var counts: Array = entry[1] as Array
	var blocks: Array[PackedByteArray] = []
	var eccs: Array[PackedByteArray] = []
	var offset: int = 0
	for count: int in counts:
		var block: PackedByteArray = data.slice(offset, offset + count)
		offset += count
		blocks.append(block)
		eccs.append(QrField.remainder(block, ecc_count))
	var out: PackedByteArray = PackedByteArray()
	var longest: int = counts[counts.size() - 1] as int
	for index: int in longest:
		for block: PackedByteArray in blocks:
			if index < block.size():
				out.append(block[index])
	for index: int in ecc_count:
		for block: PackedByteArray in eccs:
			out.append(block[index])
	return out


## Where the alignment patterns' centres sit (versions 2 to 6 have one row and column of them besides the finders).
static func alignment_positions(v: int) -> PackedInt32Array:
	if v == 1:
		return PackedInt32Array()
	return PackedInt32Array([6, side(v) - 7])


## 1 for every module that is a pattern or format information, not data.
static func function_map(v: int) -> PackedByteArray:
	var n: int = side(v)
	var map: PackedByteArray = PackedByteArray()
	map.resize(n * n)
	for y: int in n:
		for x: int in n:
			var used: bool = x == 6 or y == 6
			used = used or (x < 9 and y < 9) or (x >= n - 8 and y < 9) or (x < 9 and y >= n - 8)
			if v > 1 and x >= n - 9 and x <= n - 5 and y >= n - 9 and y <= n - 5:
				used = true
			if used:
				map[y * n + x] = 1
	return map


static func _draw_function(grid: PackedByteArray, v: int) -> void:
	var n: int = side(v)
	for index: int in n:
		grid[6 * n + index] = 1 if index % 2 == 0 else 0
		grid[index * n + 6] = 1 if index % 2 == 0 else 0
	for corner: Vector2i in [Vector2i(3, 3), Vector2i(n - 4, 3), Vector2i(3, n - 4)]:
		for dy: int in range(-4, 5):
			for dx: int in range(-4, 5):
				var x: int = corner.x + dx
				var y: int = corner.y + dy
				if x < 0 or y < 0 or x >= n or y >= n:
					continue
				var ring: int = maxi(absi(dx), absi(dy))
				grid[y * n + x] = 1 if ring != 2 and ring != 4 else 0
	if v > 1:
		var c: int = n - 7
		for dy: int in range(-2, 3):
			for dx: int in range(-2, 3):
				grid[(c + dy) * n + c + dx] = 1 if maxi(absi(dx), absi(dy)) != 1 else 0


## The module indices that carry data bits, in the order the bits are written (the two-column zigzag from the bottom right).
static func data_order(v: int) -> PackedInt32Array:
	var n: int = side(v)
	var function: PackedByteArray = function_map(v)
	var order: PackedInt32Array = PackedInt32Array()
	var right: int = n - 1
	while right >= 1:
		if right == 6:
			right = 5
		var upward: bool = ((right + 1) & 2) == 0
		for vert: int in n:
			var y: int = n - 1 - vert if upward else vert
			for j: int in 2:
				var x: int = right - j
				if function[y * n + x] == 0:
					order.append(y * n + x)
		right -= 2
	return order


static func mask_bit(which: int, x: int, y: int) -> bool:
	match which:
		0:
			return (x + y) % 2 == 0
		1:
			return y % 2 == 0
		2:
			return x % 3 == 0
		3:
			return (x + y) % 3 == 0
		4:
			return (y / 2 + x / 3) % 2 == 0
		5:
			return (x * y) % 2 + (x * y) % 3 == 0
		6:
			return ((x * y) % 2 + (x * y) % 3) % 2 == 0
	return ((x + y) % 2 + (x * y) % 3) % 2 == 0


## Flips the data modules where mask `which` says so (doing it twice undoes it).
static func apply_mask(grid: PackedByteArray, function: PackedByteArray, n: int, which: int) -> void:
	for y: int in n:
		for x: int in n:
			if function[y * n + x] == 0 and mask_bit(which, x, y):
				grid[y * n + x] ^= 1


## The 15 format bits for a level and mask (BCH-protected, then masked with 0x5412).
static func format_bits(level: Ecc, which: int) -> int:
	var data: int = (ECC_BITS[level] << 3) | which
	var remainder: int = data
	for _i: int in 10:
		remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537)
	return ((data << 10) | (remainder & 0x3FF)) ^ 0x5412


## The module positions of the two copies of the format bits, bit 0 first.
static func format_positions(n: int) -> Array[Array]:
	var first: Array[Vector2i] = []
	var second: Array[Vector2i] = []
	for i: int in 6:
		first.append(Vector2i(8, i))
	first.append(Vector2i(8, 7))
	first.append(Vector2i(8, 8))
	first.append(Vector2i(7, 8))
	for i: int in range(9, 15):
		first.append(Vector2i(14 - i, 8))
	for i: int in 8:
		second.append(Vector2i(n - 1 - i, 8))
	for i: int in range(8, 15):
		second.append(Vector2i(8, n - 15 + i))
	return [first, second]


static func draw_format(grid: PackedByteArray, n: int, level: Ecc, which: int) -> void:
	var bits: int = format_bits(level, which)
	for copy: Array in format_positions(n):
		for i: int in 15:
			var at: Vector2i = copy[i] as Vector2i
			grid[at.y * n + at.x] = (bits >> i) & 1
	# the dark module
	grid[(n - 8) * n + 8] = 1


static func _penalty(grid: PackedByteArray, n: int) -> int:
	var score: int = 0
	var dark: int = 0
	for horizontal: int in 2:
		for a: int in n:
			var run: int = 0
			var last: int = -1
			var window: int = 0
			for b: int in n:
				var value: int = grid[a * n + b] if horizontal == 1 else grid[b * n + a]
				if value == last:
					run += 1
					if run == 5:
						score += 3
					elif run > 5:
						score += 1
				else:
					run = 1
					last = value
				# finder-like 1011101 with four light modules on a side
				window = ((window << 1) | value) & 0x7FF
				if b >= 10 and (window == 0x5D0 or window == 0x05D):
					score += 40
	for y: int in n - 1:
		for x: int in n - 1:
			var value: int = grid[y * n + x]
			if value == grid[y * n + x + 1] and value == grid[(y + 1) * n + x] and value == grid[(y + 1) * n + x + 1]:
				score += 3
	for value: int in grid:
		dark += value
	var percent: int = dark * 100 / (n * n)
	score += absi(percent - 50) / 5 * 10
	return score
