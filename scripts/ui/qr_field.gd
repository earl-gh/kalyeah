class_name QrField
extends RefCounted
## QR codes' Reed-Solomon arithmetic in GF(256) (polynomial 0x11D, generator roots 2^0, 2^1, ...): the error codewords
## `QrCode` writes and the corrections `QrReader` makes. Polynomials are byte arrays, highest power first, unless said otherwise.

static var _exp: PackedByteArray
static var _log: PackedByteArray


static func _tables() -> void:
	if not _exp.is_empty():
		return
	_exp.resize(512)
	_log.resize(256)
	var value: int = 1
	for index: int in 255:
		_exp[index] = value
		_log[value] = index
		value <<= 1
		if value & 0x100:
			value ^= 0x11D
	for index: int in range(255, 512):
		_exp[index] = _exp[index - 255]


static func multiply(a: int, b: int) -> int:
	if a == 0 or b == 0:
		return 0
	_tables()
	return _exp[_log[a] + _log[b]]


static func divide(a: int, b: int) -> int:
	if a == 0:
		return 0
	_tables()
	return _exp[(_log[a] + 255 - _log[b]) % 255]


## 2 to the power `n`.
static func power(n: int) -> int:
	_tables()
	return _exp[posmod(n, 255)]


static func inverse(a: int) -> int:
	return divide(1, a)


## The `degree` error codewords for `data`.
static func remainder(data: PackedByteArray, degree: int) -> PackedByteArray:
	# the generator (x - 2^0)(x - 2^1)..., highest power left out (it is 1)
	var divisor: PackedByteArray = PackedByteArray()
	divisor.resize(degree)
	divisor[degree - 1] = 1
	var root: int = 1
	for _i: int in degree:
		for j: int in degree:
			divisor[j] = multiply(divisor[j], root)
			if j + 1 < degree:
				divisor[j] ^= divisor[j + 1]
		root = multiply(root, 2)
	var result: PackedByteArray = PackedByteArray()
	result.resize(degree)
	for byte: int in data:
		var factor: int = byte ^ result[0]
		for j: int in degree - 1:
			result[j] = result[j + 1]
		result[degree - 1] = 0
		for j: int in degree:
			result[j] ^= multiply(divisor[j], factor)
	return result


## Fixes up to `ecc_count / 2` wrong bytes in `block` (data then its `ecc_count` error codewords) in place. False when there are
## too many to fix.
static func correct(block: PackedByteArray, ecc_count: int) -> bool:
	var n: int = block.size()
	var syndromes: PackedByteArray = PackedByteArray()
	syndromes.resize(ecc_count)
	var clean: bool = true
	for i: int in ecc_count:
		var x: int = power(i)
		var value: int = 0
		for byte: int in block:
			value = multiply(value, x) ^ byte
		syndromes[i] = value
		if value != 0:
			clean = false
	if clean:
		return true
	# Berlekamp-Massey: the error locator, lowest power first
	var locator: PackedByteArray = PackedByteArray([1])
	var previous: PackedByteArray = PackedByteArray([1])
	var length: int = 0
	var shift: int = 1
	var last: int = 1
	for step: int in ecc_count:
		var delta: int = syndromes[step]
		for i: int in range(1, length + 1):
			if i < locator.size():
				delta ^= multiply(locator[i], syndromes[step - i])
		if delta == 0:
			shift += 1
			continue
		var scale: int = divide(delta, last)
		var next: PackedByteArray = locator.duplicate()
		if next.size() < previous.size() + shift:
			next.resize(previous.size() + shift)
		for i: int in previous.size():
			next[i + shift] ^= multiply(scale, previous[i])
		if 2 * length <= step:
			previous = locator
			length = step + 1 - length
			last = delta
			shift = 1
		else:
			shift += 1
		locator = next
	if length * 2 > ecc_count:
		return false
	# the evaluator: syndromes x locator, mod x^ecc_count (lowest power first)
	var evaluator: PackedByteArray = PackedByteArray()
	evaluator.resize(ecc_count)
	for i: int in ecc_count:
		for j: int in locator.size():
			if i + j < ecc_count:
				evaluator[i + j] ^= multiply(syndromes[i], locator[j])
	var found: int = 0
	for index: int in n:
		# the byte at `index` is the coefficient of x^(n - 1 - index)
		var place: int = power(n - 1 - index)
		var place_inverse: int = inverse(place)
		if _evaluate_low(locator, place_inverse) != 0:
			continue
		var derivative: int = 0
		for i: int in range(1, locator.size(), 2):
			derivative ^= multiply(locator[i], power_of(place_inverse, i - 1))
		if derivative == 0:
			return false
		block[index] ^= multiply(place, divide(_evaluate_low(evaluator, place_inverse), derivative))
		found += 1
	return found == length


static func power_of(a: int, n: int) -> int:
	var result: int = 1
	for _i: int in n:
		result = multiply(result, a)
	return result


static func _evaluate_low(poly: PackedByteArray, x: int) -> int:
	var value: int = 0
	for i: int in range(poly.size() - 1, -1, -1):
		value = multiply(value, x) ^ poly[i]
	return value
