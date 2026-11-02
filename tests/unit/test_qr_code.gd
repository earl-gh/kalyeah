extends GutTest
## The room QR code: QrCode makes it, QrReader reads it back from a camera-like picture (scaled, turned, tilted, unevenly lit,
## mirrored), QrField fixes damaged bytes.

const QR_PICTURE: GDScript = preload("res://tests/fixtures/qr_picture.gd")


func _picture(code: QrCode, side: int, fill: float, degrees: float = 0.0, tilt: float = 0.0, gradient: float = 0.0, mirror: bool = false) -> Image:
	return QR_PICTURE.draw(code, side, fill, degrees, tilt, gradient, mirror)


func test_format_bits_match_the_standard_table() -> void:
	assert_eq(QrCode.format_bits(QrCode.Ecc.L, 0), 0b111011111000100)
	assert_eq(QrCode.format_bits(QrCode.Ecc.M, 0), 0b101010000010010)
	assert_eq(QrCode.format_bits(QrCode.Ecc.H, 7), 0b000100000111011)


func test_hello_world_1q_codewords_match_the_worked_example() -> void:
	var data: PackedByteArray = PackedByteArray([32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236])
	assert_eq(QrField.remainder(data, 13), PackedByteArray([168, 72, 22, 82, 217, 54, 156, 0, 46, 15, 180, 122, 16]))
	var code: QrCode = QrCode.encode("HELLO WORLD", QrCode.Ecc.Q)
	assert_eq(code.version, 1)
	assert_eq(code.size, 21)


func test_a_room_code_fits_the_smallest_code() -> void:
	var code: QrCode = QrCode.encode(RoomQr.payload("ABCD2345"))
	assert_not_null(code)
	assert_eq(code.version, 1, "21 x 21 modules: the easiest for a camera")
	assert_eq(code.ecc, QrCode.Ecc.M)


func test_too_long_text_gives_no_code() -> void:
	assert_null(QrCode.encode("x".repeat(200)))


func test_reed_solomon_fixes_up_to_half_its_error_bytes() -> void:
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = 7
	for trial: int in 20:
		var data: PackedByteArray = PackedByteArray()
		for _i: int in 20:
			data.append(rng.randi_range(0, 255))
		var block: PackedByteArray = data.duplicate()
		block.append_array(QrField.remainder(data, 10))
		var broken: PackedByteArray = block.duplicate()
		var hits: int = trial % 6
		var places: Array[int] = []
		while places.size() < hits:
			var at: int = rng.randi_range(0, block.size() - 1)
			if not places.has(at):
				places.append(at)
				broken[at] ^= rng.randi_range(1, 255)
		var fixed: bool = QrField.correct(broken, 10)
		if hits <= 5:
			assert_true(fixed, "%d errors fixed" % hits)
			assert_eq(broken, block)


func test_every_version_and_level_reads_back_from_a_clean_grid() -> void:
	for level: int in 4:
		for length: int in [1, 8, 20, 40, 70]:
			var text: String = "Kalyeah laro tayo! 0123456789".repeat(4).substr(0, length)
			var code: QrCode = QrCode.encode(text, level as QrCode.Ecc)
			if code == null:
				continue
			var n: int = code.size + 8
			var bits: PackedByteArray = PackedByteArray()
			bits.resize(n * n * 9)
			for y: int in n * 3:
				for x: int in n * 3:
					var mx: int = x / 3 - 4
					var my: int = y / 3 - 4
					if mx >= 0 and my >= 0 and mx < code.size and my < code.size and code.is_dark(mx, my):
						bits[y * n * 3 + x] = 1
			assert_eq(QrReader.read_bits(bits, n * 3, n * 3), text, "v%d level %d, %d chars" % [code.version, level, length])


func test_reads_a_room_code_from_a_camera_like_picture() -> void:
	var payload: String = RoomQr.payload("HJK23MNP")
	var code: QrCode = QrCode.encode(payload)
	assert_eq(QrReader.read(_picture(code, 320, 0.6)), payload, "straight on")
	assert_eq(QrReader.read(_picture(code, 320, 0.5, 23.0)), payload, "turned")
	assert_eq(QrReader.read(_picture(code, 320, 0.7, 90.0)), payload, "sideways")
	assert_eq(QrReader.read(_picture(code, 320, 0.6, 180.0, 0.0, 0.6)), payload, "upside down, lit from one side")
	assert_eq(QrReader.read(_picture(code, 320, 0.6, -12.0, 0.15)), payload, "held at an angle")
	assert_eq(QrReader.read(_picture(code, 320, 0.45, 0.0, 0.0, 0.0, true)), payload, "mirrored, like a front camera")


func test_reads_a_bigger_code_with_its_alignment_square() -> void:
	var text: String = "https://kalyeah-promo.vercel.app/?room=HJK23MNP"
	var code: QrCode = QrCode.encode(text)
	assert_gt(code.version, 1)
	assert_eq(QrReader.read(_picture(code, 360, 0.8, 8.0, 0.1)), text)


func test_reads_from_a_big_colour_frame() -> void:
	var payload: String = RoomQr.payload("ABCD2345")
	var grey: Image = _picture(QrCode.encode(payload), 300, 0.6)
	grey.convert(Image.FORMAT_RGBA8)
	grey.resize(900, 900, Image.INTERPOLATE_NEAREST)
	assert_eq(QrReader.read(grey), payload, "shrunk to MAX_SIDE first")


func test_nothing_is_read_from_a_blank_or_noisy_picture() -> void:
	var blank: Image = Image.create(200, 200, false, Image.FORMAT_L8)
	blank.fill(Color(0.8, 0.8, 0.8))
	assert_eq(QrReader.read(blank), "")
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = 3
	var noise: PackedByteArray = PackedByteArray()
	for _i: int in 200 * 200:
		noise.append(rng.randi_range(0, 255))
	assert_eq(QrReader.read(Image.create_from_data(200, 200, false, Image.FORMAT_L8, noise)), "")
	assert_eq(QrReader.read(null), "")


func test_room_payload_round_trip_and_what_is_refused() -> void:
	assert_eq(RoomQr.payload("ABCD2345"), "KALYEAH:ABCD2345")
	assert_eq(RoomQr.code_from("KALYEAH:ABCD2345"), "ABCD2345")
	assert_eq(RoomQr.code_from(" kalyeah:abcd2345 "), "ABCD2345", "a hand-made code in small letters still works")
	assert_eq(RoomQr.code_from("ABCD2345"), "", "only Kalyeah's own codes")
	assert_eq(RoomQr.code_from("KALYEAH:ABC"), "", "too short")
	assert_eq(RoomQr.code_from("KALYEAH:ABCD234I"), "", "I is not in the code alphabet")
	assert_eq(RoomQr.code_from("WIFI:S:x;T:WPA;P:y;;"), "")


func test_a_room_code_reads_through_grain_blur_tilt_and_uneven_light() -> void:
	var payload: String = RoomQr.payload("QRST6789")
	var code: QrCode = QrCode.encode(payload)
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = 21
	var read: int = 0
	for trial: int in 30:
		var picture: Image = _picture(code, 320, rng.randf_range(0.4, 0.9), rng.randf_range(0.0, 360.0), rng.randf_range(0.0, 0.12),
			rng.randf_range(0.0, 0.5), trial % 6 == 0)
		picture.resize(220, 220, Image.INTERPOLATE_BILINEAR)
		picture.resize(320, 320, Image.INTERPOLATE_BILINEAR)
		var data: PackedByteArray = picture.get_data()
		for index: int in data.size():
			data[index] = clampi(data[index] + rng.randi_range(-15, 15), 0, 255)
		if QrReader.read(Image.create_from_data(320, 320, false, Image.FORMAT_L8, data)) == payload:
			read += 1
	# the scanner reads about three frames a second, so a frame now and then missed is not felt
	assert_gte(read, 27, "%d of 30 frames read" % read)
