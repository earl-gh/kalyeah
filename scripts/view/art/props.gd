class_name Props
extends RefCounted
## Low-poly street props built from LowPoly pieces (one material, palette colours).
## Inspired by Filipino streets: sari-sari store, houses with tin roofs, laundry
## lines, an electric post covered in flyers, and the cover kids hide behind in the
## street game: the houses, store, trees and the like. The cover pieces, benches, poles and A-frames are 3D models (PropModel).
## Meshes are cached per key, so the same prop is built once.

static var _cache: Dictionary[String, ArrayMesh] = {}


static func cached(key: String, build: Callable) -> ArrayMesh:
	if not _cache.has(key):
		_cache[key] = build.call() as ArrayMesh
	return _cache[key]


## A two-storey house with a balcony, grilled windows and a water tank on the roof.
static func house_tall(variant: int) -> ArrayMesh:
	return cached("house_tall%d" % variant, func() -> ArrayMesh:
		var walls: Array[Color] = [Palette.WALL_PEACH, Palette.WALL_SKY, Palette.WALL_MINT, Palette.WALL_PINK]
		var wall: Color = walls[variant % walls.size()]
		var upper: Color = wall.lightened(0.12)
		var width: float = 5.6
		var depth: float = 4.4
		var kit: LowPoly = LowPoly.new()
		# concrete ground floor, painted wooden upper floor
		kit.box(Vector3(0.0, 1.5, 0.0), Vector3(depth, 3.0, width), Palette.CONCRETE, Palette.CONCRETE)
		kit.box(Vector3(0.1, 4.3, 0.0), Vector3(depth + 0.2, 2.6, width + 0.2), upper)
		kit.box(Vector3(depth / 2.0 + 0.02, 1.0, -1.2), Vector3(0.08, 2.0, 1.0), Palette.DOOR)
		kit.box(Vector3(depth / 2.0 + 0.02, 1.7, 1.2), Vector3(0.08, 1.0, 1.6), Palette.WINDOW)
		for i: int in 5:
			kit.box(Vector3(depth / 2.0 + 0.07, 1.7, 0.5 + float(i) * 0.35), Vector3(0.04, 1.0, 0.04), Palette.WHITE)
		# balcony with railing and plants
		kit.box(Vector3(depth / 2.0 + 0.7, 3.05, 0.0), Vector3(1.4, 0.15, width * 0.8), Palette.CONCRETE)
		for i: int in 9:
			kit.box(Vector3(depth / 2.0 + 1.35, 3.45, -width * 0.38 + float(i) * width * 0.095), Vector3(0.05, 0.7, 0.05), Palette.WIRE)
		kit.box(Vector3(depth / 2.0 + 1.35, 3.8, 0.0), Vector3(0.08, 0.06, width * 0.8), Palette.WIRE)
		for z: float in [-1.6, 1.6]:
			kit.cylinder(Vector3(depth / 2.0 + 0.9, 3.12, z), 0.18, 0.14, 0.3, 6, Palette.POT)
			kit.sphere(Vector3(depth / 2.0 + 0.9, 3.6, z), 0.28, Palette.LEAF)
		kit.box(Vector3(depth / 2.0 + 0.14, 4.4, 0.0), Vector3(0.08, 1.2, 2.4), Palette.WINDOW)
		kit.box(Vector3(depth / 2.0 + 0.19, 4.4, 0.0), Vector3(0.04, 1.3, 0.08), Palette.WHITE)
		kit.set_transform(Transform3D(Basis(Vector3.UP, PI / 2.0), Vector3.ZERO))
		kit.gable(Vector3(0.0, 5.6, 0.0), width + 0.2, depth + 0.2, 1.4, Palette.ROOF_TIN if variant % 2 == 0 else Palette.ROOF_RED, 0.4)
		kit.reset_transform()
		# blue water tank on a stand at the back
		for x: float in [-0.5, 0.5]:
			for z: float in [-0.5, 0.5]:
				kit.box(Vector3(-depth / 2.0 + 0.6 + x, 6.6, 1.5 + z), Vector3(0.08, 1.2, 0.08), Palette.WIRE)
		kit.cylinder(Vector3(-depth / 2.0 + 0.6, 7.2, 1.5), 0.65, 0.65, 1.1, 8, Palette.JEEP_BLUE, Palette.JEEP_BLUE.lightened(0.2))
		return kit.commit())


## A fiesta tarpaulin banner (fictional, no real names), facing +X.
static func tarpaulin(variant: int) -> ArrayMesh:
	return cached("tarp%d" % variant, func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		var colors: Array[Color] = [Palette.SARI_YELLOW, Palette.WALL_SKY, Palette.WALL_PINK]
		var base: Color = colors[variant % colors.size()]
		kit.box(Vector3.ZERO, Vector3(0.04, 1.0, 2.2), Palette.WHITE)
		kit.box(Vector3(0.03, 0.25, 0.0), Vector3(0.02, 0.4, 2.0), base)
		kit.box(Vector3(0.03, -0.22, -0.55), Vector3(0.02, 0.35, 0.7), Palette.JEEP_RED)
		kit.box(Vector3(0.03, -0.22, 0.45), Vector3(0.02, 0.12, 0.9), Palette.BLACK)
		kit.box(Vector3(0.03, -0.36, 0.45), Vector3(0.02, 0.08, 0.7), Palette.BLACK)
		return kit.commit())


## Chalk piko (hopscotch) drawn on the asphalt, long axis along Z.
static func hopscotch() -> ArrayMesh:
	return cached("piko", func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		var chalk: Color = Color(0.97, 0.97, 0.92)
		var squares: Array[Vector2] = [Vector2(0, 0), Vector2(0, 1), Vector2(-0.5, 2), Vector2(0.5, 2), Vector2(0, 3), Vector2(-0.5, 4), Vector2(0.5, 4), Vector2(0, 5)]
		for cell: Vector2 in squares:
			var c: Vector3 = Vector3(cell.x * 1.0, 0.012, -cell.y * 0.9)
			for edge: int in 4:
				var horizontal: bool = edge < 2
				var offset: Vector3 = Vector3(0.0, 0.0, 0.42 if edge == 0 else -0.42) if horizontal else Vector3(0.47 if edge == 2 else -0.47, 0.0, 0.0)
				var size: Vector3 = Vector3(0.94, 0.01, 0.06) if horizontal else Vector3(0.06, 0.01, 0.84)
				kit.box(c + offset, size, chalk)
		kit.box(Vector3(0.0, 0.012, -5.35), Vector3(0.3, 0.01, 0.3), Palette.SARI_YELLOW)
		return kit.commit())


static func manhole() -> ArrayMesh:
	return cached("manhole", func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		kit.cylinder(Vector3(0.0, 0.0, 0.0), 0.55, 0.55, 0.02, 10, Palette.ASPHALT_DARK.darkened(0.3), Palette.ASPHALT_DARK.darkened(0.25))
		for i: int in 3:
			kit.box(Vector3(0.0, 0.025, -0.25 + float(i) * 0.25), Vector3(0.7, 0.01, 0.05), Palette.ASPHALT_DARK.darkened(0.45))
		return kit.commit())


## The concrete base pad around a post: painted rings, team colour applied separately.
static func base_pad(radius: float) -> ArrayMesh:
	return cached("pad%.1f" % radius, func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		kit.cylinder(Vector3(0.0, -0.02, 0.0), radius + 0.35, radius + 0.25, 0.1, 16, Palette.CURB, Palette.CONCRETE)
		for i: int in 8:
			var angle: float = TAU * float(i) / 8.0
			kit.box(Vector3(cos(angle), 0.0, sin(angle)) * (radius + 0.05) + Vector3(0.0, 0.1, 0.0), Vector3(0.35, 0.06, 0.35), Palette.SARI_YELLOW)
		return kit.commit())


## A house front, facing +X (the lane is on its +X side). Footprint w (along Z) x d.
static func house(variant: int) -> ArrayMesh:
	return cached("house%d" % variant, func() -> ArrayMesh:
		var walls: Array[Color] = [Palette.WALL_MINT, Palette.WALL_PINK, Palette.WALL_SKY, Palette.WALL_LEMON, Palette.WALL_PEACH]
		var roofs: Array[Color] = [Palette.ROOF_TIN, Palette.ROOF_RUST, Palette.ROOF_RED]
		var wall: Color = walls[variant % walls.size()]
		var roof: Color = roofs[variant % roofs.size()]
		var width: float = 5.0 + float(variant % 3) * 0.8
		var depth: float = 4.0
		var height: float = 3.0 + float(variant % 2) * 0.6
		var kit: LowPoly = LowPoly.new()
		kit.box(Vector3(0.0, height / 2.0, 0.0), Vector3(depth, height, width), wall)
		kit.box(Vector3(depth / 2.0 + 0.02, 0.95, -width * 0.22), Vector3(0.08, 1.9, 0.9), Palette.DOOR)
		kit.box(Vector3(depth / 2.0 + 0.02, 1.6, width * 0.2), Vector3(0.08, 0.9, 1.2), Palette.WINDOW)
		kit.box(Vector3(depth / 2.0 + 0.06, 1.12, width * 0.2), Vector3(0.12, 0.08, 1.4), Palette.WHITE)
		# awning over the door
		kit.box(Vector3(depth / 2.0 + 0.45, 2.15, -width * 0.22), Vector3(0.9, 0.08, 1.3), roof.lightened(0.15))
		kit.set_transform(Transform3D(Basis(Vector3.UP, PI / 2.0), Vector3.ZERO))
		kit.gable(Vector3(0.0, height, 0.0), width, depth, 1.3, roof, 0.35)
		kit.reset_transform()
		if variant % 2 == 0:
			kit.cylinder(Vector3(depth / 2.0 + 0.5, 0.0, width * 0.42), 0.28, 0.22, 0.45, 6, Palette.POT)
			kit.sphere(Vector3(depth / 2.0 + 0.5, 0.75, width * 0.42), 0.38, Palette.LEAF)
		return kit.commit())


## Sari-sari store front, facing +X: wide grilled window, yellow-red sign, tin roof.
static func sari_sari() -> ArrayMesh:
	return cached("sari", func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		kit.box(Vector3(0.0, 1.5, 0.0), Vector3(3.5, 3.0, 5.0), Palette.WALL_LEMON)
		kit.box(Vector3(1.77, 1.45, 0.0), Vector3(0.06, 1.2, 3.6), Palette.WINDOW)
		for i: int in 9:
			kit.box(Vector3(1.82, 1.45, -1.6 + float(i) * 0.4), Vector3(0.05, 1.2, 0.05), Palette.WHITE)
		kit.box(Vector3(2.0, 0.85, 0.0), Vector3(0.5, 0.1, 3.8), Palette.WOOD)
		# little goods on the counter
		for i: int in 6:
			kit.box(Vector3(2.05, 0.98, -1.5 + float(i) * 0.6), Vector3(0.18, 0.18, 0.18), Palette.FLYERS[i % Palette.FLYERS.size()])
		kit.box(Vector3(1.95, 2.55, 0.0), Vector3(0.12, 0.7, 4.4), Palette.SARI_RED)
		kit.box(Vector3(2.02, 2.55, 0.0), Vector3(0.04, 0.45, 3.8), Palette.SARI_YELLOW)
		kit.set_transform(Transform3D(Basis(Vector3.FORWARD, -0.18), Vector3(0.3, 3.0, 0.0)))
		kit.box(Vector3.ZERO, Vector3(4.6, 0.1, 5.6), Palette.ROOF_TIN)
		kit.reset_transform()
		return kit.commit())


static func basketball_ring() -> ArrayMesh:
	return cached("hoop", func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		kit.box(Vector3(0.0, 1.5, 0.0), Vector3(0.14, 3.0, 0.14), Palette.JEEP_BLUE)
		kit.box(Vector3(0.35, 3.0, 0.0), Vector3(0.7, 0.1, 0.1), Palette.JEEP_BLUE)
		kit.box(Vector3(0.72, 3.25, 0.0), Vector3(0.06, 0.8, 1.1), Palette.WHITE)
		kit.box(Vector3(0.74, 3.15, 0.0), Vector3(0.04, 0.3, 0.4), Palette.SARI_RED)
		for i: int in 8:
			var angle: float = TAU * float(i) / 8.0
			kit.box(Vector3(1.0 + cos(angle) * 0.24, 2.95, sin(angle) * 0.24), Vector3(0.09, 0.04, 0.09), Palette.RING_ORANGE)
		return kit.commit())


## Jeepney stop sign on a pole.
static func stop_sign() -> ArrayMesh:
	return cached("stop", func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		kit.box(Vector3(0.0, 1.2, 0.0), Vector3(0.1, 2.4, 0.1), Palette.POST_GREY)
		kit.box(Vector3(0.0, 2.45, 0.0), Vector3(0.08, 0.6, 1.0), Palette.JEEP_BLUE)
		kit.box(Vector3(0.05, 2.45, 0.0), Vector3(0.02, 0.2, 0.7), Palette.WHITE)
		return kit.commit())


## The thrown object for a weapon (by WeaponDef id); sized by its radius.
static func weapon_object(id: StringName, radius: float) -> ArrayMesh:
	return cached("weapon_%s_%.2f" % [id, radius], func() -> ArrayMesh:
		var kit: LowPoly = LowPoly.new()
		var r: float = maxf(radius, 0.15)
		match id:
			&"bato_light", &"bato_heavy":
				kit.sphere(Vector3.ZERO, r, Palette.CONCRETE.darkened(0.15), 5, 3)
			&"papel_trap":
				kit.sphere(Vector3.ZERO, r, Palette.WHITE, 5, 3)
			&"tsinelas_light", &"tsinelas_heavy":
				kit.box(Vector3.ZERO, Vector3(r * 1.1, r * 0.3, r * 2.4), Palette.JEEP_BLUE, Palette.SARI_YELLOW)
				kit.box(Vector3(0.0, r * 0.3, -r * 0.5), Vector3(r * 1.0, r * 0.2, r * 0.2), Palette.SARI_RED)
			&"lata":
				kit.cylinder(Vector3(0.0, -r, 0.0), r * 0.7, r * 0.7, r * 2.0, 8, Palette.JEEP_CHROME)
				kit.cylinder(Vector3(0.0, -r * 0.4, 0.0), r * 0.72, r * 0.72, r * 0.8, 8, Palette.SARI_RED)
			&"jacks":
				for axis: Vector3 in [Vector3.RIGHT, Vector3.UP, Vector3.BACK]:
					kit.box(Vector3.ZERO, Vector3.ONE * r * 0.25 + axis * r * 1.6, Palette.JEEP_CHROME)
			&"bola":
				kit.sphere(Vector3.ZERO, r, Palette.SARI_RED, 7, 4)
			&"trumpo":
				kit.cylinder(Vector3(0.0, -r * 0.7, 0.0), 0.02, r, r * 0.9, 8, Palette.WOOD)
				kit.cylinder(Vector3(0.0, r * 0.2, 0.0), r, r * 0.3, r * 0.4, 8, Palette.SARI_RED)
			_:
				kit.sphere(Vector3.ZERO, r, Palette.WHITE)
		return kit.commit())


## The polymorphed player: a big can (tumbang preso lata).
