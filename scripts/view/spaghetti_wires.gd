class_name SpaghettiWires
extends RefCounted
## Physically anchored Philippine Barangay overhead electric & telecom wire network,
## 3D galvanized side-wall weatherhead brackets + porcelain spool insulators, under-crossarm
## pole slack coils, and grounded courtyard *sampayan* helpers.
## Every wire strand terminates at BOTH ends on a real 3D pole crossarm insulator pin or a real
## 3D building side-wall ceramic weatherhead spool — zero floating wire endpoints anywhere in the arena.

## Exact binary-measured local-space porcelain insulator pin & secondary rack coordinates on
## `brgy_meralco_post.glb` (`height = 13.60 m`, pole trunk axis `X = 0.0, Z = -0.510`):
##   0..2 = upper crossarm porcelain insulator tips (`Y = 13.290, Z = -0.369`),
##   3..5 = lower crossarm porcelain insulator tips (`Y = 11.640, Z = -0.369`),
##   6    = secondary spool / bushing on front trunk face (`Y = 10.850, Z = -0.369`),
##   7    = telco messenger clamp on rear trunk face (`Y = 10.150, Z = -0.650`).
const LOCAL_POLE_PINS: Array[Vector3] = [
	Vector3(-0.735, 13.290, -0.369),
	Vector3(0.280, 13.290, -0.369),
	Vector3(0.735, 13.290, -0.369),
	Vector3(-0.735, 11.640, -0.369),
	Vector3(-0.280, 11.640, -0.369),
	Vector3(0.735, 11.640, -0.369),
	Vector3(0.0, 10.850, -0.369),
	Vector3(0.0, 10.150, -0.650),
]

## Exact post-centering Godot local `Vector3` coordinates of the built-in ceramic weatherhead spools
## baked on the side CHB firewalls (`±X`) of `@fix-3d-kit`'s architectural `.glb` models (`0.00 m` tie-in).
const KIT_WEATHERHEAD_SOCKETS: Dictionary[String, Array] = {
	"brgy_sari_sari": [Vector3(3.725, 3.420, 1.215)],
	"brgy_shophouse_row": [Vector3(7.410, 4.690, -1.320)],
	"brgy_tabi_cluster_a": [Vector3(-7.923, 6.240, -0.675), Vector3(8.157, 4.390, 1.525)],
	"brgy_tabi_cluster_b": [Vector3(-6.860, 6.490, 1.715)],
	"brgy_house_two_storey": [Vector3(3.905, 6.220, 1.310)],
	"brgy_hall_plaza": [Vector3(7.740, 7.090, 1.742)],
	"brgy_kapilya": [Vector3(3.040, 4.190, 1.320)],
	"brgy_bakery_oven": [Vector3(-2.635, 3.220, 0.735)],
}

## Exact binary-measured exterior side-firewall `X` faces (`Vector2(neg_wall_x, pos_wall_x)`) for asymmetric
## two-unit clusters & houses so fallback 3D weatherheads mount 0.00 m flush on the true outer CHB wall!
const KIT_SIDE_WALL_X: Dictionary[String, Vector2] = {
	"brgy_sari_sari": Vector2(-3.815, 3.705),
	"brgy_shophouse_row": Vector2(-7.430, 7.390),
	"brgy_tabi_cluster_a": Vector2(-7.893, 8.137),
	"brgy_tabi_cluster_b": Vector2(-6.840, 7.115),
	"brgy_house_two_storey": Vector2(-4.035, 3.885),
	"brgy_hall_plaza": Vector2(-7.720, 7.720),
	"brgy_kapilya": Vector2(-3.020, 3.020),
	"brgy_bakery_oven": Vector2(-2.615, 2.555),
}


## Computes the 8 world-space crossarm & secondary-rack pin positions for a placed pole `Node3D`.
static func compute_pole_pins(pole: Node3D) -> PackedVector3Array:
	var pins: PackedVector3Array = PackedVector3Array()
	var xf: Transform3D = pole.transform
	for local_p: Vector3 in LOCAL_POLE_PINS:
		pins.append(xf * local_p)
	return pins


## Computes the exact side-firewall weatherhead socket facing toward `pole_xz` for a placed building record.
## Prefers the building's built-in `.glb` ceramic weatherhead spool when that side firewall faces `pole_xz`
## cleanly (`dot >= 0.42`, clearing overhanging roof eaves); falls back to mounting a physical 3D pole-facing
## corner firewall weatherhead bracket below the eave when the nearest pole stands at a steeper/opposite angle.
static func compute_side_weatherhead(pole_xz: Vector2, bldg: Dictionary, allow_builtin: bool = true) -> Dictionary:
	var kit_id: String = String(bldg.get("kit_id", ""))
	var canonical_kit: String = "brgy_hall_plaza" if kit_id == "brgy_barangay_hall" else kit_id
	# Open-air waiting sheds, market stalls & planter walls have no solid side firewall for Meralco risers!
	if kit_id != "" and not KIT_WEATHERHEAD_SOCKETS.has(canonical_kit):
		return {}

	if allow_builtin and bldg.has("transform") and KIT_WEATHERHEAD_SOCKETS.has(canonical_kit):
		var xf: Transform3D = bldg["transform"] as Transform3D
		var sockets: Array = KIT_WEATHERHEAD_SOCKETS[canonical_kit]
		var best_builtin_dist: float = 999.0
		var best_builtin: Dictionary = {}
		for item: Variant in sockets:
			var local_sock: Vector3 = item as Vector3
			var world_sock: Vector3 = xf * local_sock
			var local_n: Vector3 = Vector3(1.0 if local_sock.x >= 0.0 else -1.0, 0.0, 0.0)
			var world_n3: Vector3 = (xf.basis * local_n).normalized()
			var out_n_xz: Vector2 = Vector2(world_n3.x, world_n3.z)
			if out_n_xz.length_squared() > 0.0001:
				out_n_xz = out_n_xz.normalized()
			var sock_xz: Vector2 = Vector2(world_sock.x, world_sock.z)
			var to_pole: Vector2 = pole_xz - sock_xz
			var d_sock: float = to_pole.length()
			if d_sock >= 1.2 and to_pole.normalized().dot(out_n_xz) >= 0.42 and d_sock < best_builtin_dist:
				best_builtin_dist = d_sock
				best_builtin = {
					"wall_pt": world_sock,
					"normal_xz": out_n_xz,
					"dist": d_sock,
					"builtin": true,
				}
		if not best_builtin.is_empty():
			return best_builtin

	var center: Vector2 = bldg["center"] as Vector2
	var half: Vector2 = bldg["half"] as Vector2
	var yaw: float = float(bldg["yaw"])
	var b_h: float = float(bldg["height"])
	var ux: Vector2 = Vector2(cos(yaw), -sin(yaw))
	var uz: Vector2 = Vector2(sin(yaw), cos(yaw))
	var rel: Vector2 = pole_xz - center
	var side_sign: float = 1.0 if rel.dot(ux) >= 0.0 else -1.0

	var local_x: float = side_sign * maxf(half.x - 0.34, 0.5)
	if bldg.has("transform") and KIT_SIDE_WALL_X.has(canonical_kit):
		var xf_s: Transform3D = bldg["transform"] as Transform3D
		var wall_pair: Vector2 = KIT_SIDE_WALL_X[canonical_kit]
		var sx_scale: float = xf_s.basis.get_scale().x
		local_x = (wall_pair.y if side_sign > 0.0 else wall_pair.x) * sx_scale

	var local_z: float = clampf(rel.dot(uz), -half.y * 0.62, half.y * 0.66)
	var wall_xz: Vector2 = center + ux * local_x + uz * local_z
	var normal_xz: Vector2 = (ux * side_sign).normalized()
	var to_pole_fb: Vector2 = pole_xz - wall_xz
	if to_pole_fb.length() < 1.2 or to_pole_fb.normalized().dot(normal_xz) < 0.42:
		return {}
	var attach_y: float = clampf(b_h * 0.56, 2.95, 4.55)
	return {
		"wall_pt": Vector3(wall_xz.x, attach_y, wall_xz.y),
		"normal_xz": normal_xz,
		"dist": to_pole_fb.length(),
		"builtin": false,
	}


## Tests whether a sagging 3D catenary wire between `a` and `b` clips any building wall, stepped parapet,
## or overhanging corrugated *yero* roof eave (`24` interior samples, `0` grazing wires).
static func _catenary_hits_building_obb(
	a: Vector3,
	b: Vector3,
	sag: float,
	buildings: Array[Dictionary],
	ignore_wall_idx: int
) -> bool:
	for s_step: int in range(1, 24):
		var t: float = float(s_step) / 24.0
		var p: Vector3 = a.lerp(b, t) - Vector3(0.0, 4.0 * sag * t * (1.0 - t), 0.0)
		for j: int in buildings.size():
			var b_rec: Dictionary = buildings[j]
			var bc: Vector2 = b_rec["center"] as Vector2
			var bh: Vector2 = b_rec["half"] as Vector2
			var bheight: float = float(b_rec["height"])
			if p.y > bheight + 0.35 or p.y < -0.10:
				continue
			var dx: float = p.x - bc.x
			var dz: float = p.z - bc.y
			var max_r: float = bh.x + bh.y + 1.2
			if dx * dx + dz * dz > max_r * max_r:
				continue
			var byaw: float = float(b_rec["yaw"])
			var ux: Vector2 = Vector2(cos(byaw), -sin(byaw))
			var uz: Vector2 = Vector2(sin(byaw), cos(byaw))
			var rel: Vector2 = Vector2(dx, dz)
			var signed_lx: float = rel.dot(ux)
			var lx: float = absf(signed_lx)
			var lz: float = absf(rel.dot(uz))
			if j != ignore_wall_idx:
				if lx <= bh.x + 0.35 and lz <= bh.y + 0.35:
					return true
			else:
				if p.y >= bheight * 0.78 and lx <= bh.x + 0.12 and lz <= bh.y + 0.12:
					return true
				if s_step <= 22:
					var k_id: String = String(b_rec.get("kit_id", ""))
					var can_k: String = "brgy_hall_plaza" if k_id == "brgy_barangay_hall" else k_id
					if b_rec.has("transform") and KIT_SIDE_WALL_X.has(can_k):
						var b_xf: Transform3D = b_rec["transform"] as Transform3D
						var sx_w: float = b_xf.basis.get_scale().x
						var wp: Vector2 = KIT_SIDE_WALL_X[can_k]
						if signed_lx >= wp.x * sx_w - 0.04 and signed_lx <= wp.y * sx_w + 0.04 and lz <= maxf(bh.y - 0.10, 0.3):
							return true
					elif lx <= maxf(bh.x - 0.10, 0.3) and lz <= maxf(bh.y - 0.10, 0.3):
						return true
	return false


## Builds the single-draw-call overhead wire ArrayMesh AND appends matching 3D weatherhead brackets,
## porcelain spool clevises, and pole lashing straps into `hardware_kit` so every wire is 100% anchored.
static func build_anchored_wire_network(
	pole_pin_sets: Array[PackedVector3Array],
	pole_shaft_tops: Array[Vector3],
	spans: Array[Vector2i],
	buildings: Array[Dictionary],
	hardware_kit: LowPoly
) -> ArrayMesh:
	var st: SurfaceTool = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	# 1. Pole-to-pole primary & secondary catenary bundles between real crossarm insulator pins
	for s_idx: int in spans.size():
		var pair: Vector2i = spans[s_idx]
		if pair.x < 0 or pair.x >= pole_pin_sets.size() or pair.y < 0 or pair.y >= pole_pin_sets.size():
			continue
		var pins_a: PackedVector3Array = pole_pin_sets[pair.x]
		var pins_b: PackedVector3Array = pole_pin_sets[pair.y]
		if pins_a.size() < 8 or pins_b.size() < 8:
			continue
		var mid_a: Vector3 = pins_a[1]
		var mid_b: Vector3 = pins_b[1]
		var dist: float = Vector2(mid_b.x - mid_a.x, mid_b.z - mid_a.z).length()
		if dist < 1.2 or dist > 48.0:
			continue

		var arm_a: Vector2 = Vector2(pins_a[2].x - pins_a[0].x, pins_a[2].z - pins_a[0].z)
		var arm_b: Vector2 = Vector2(pins_b[2].x - pins_b[0].x, pins_b[2].z - pins_b[0].z)
		var flip_arm: bool = arm_a.dot(arm_b) < 0.0

		var upper_map: PackedInt32Array = PackedInt32Array([0, 1, 2])
		var lower_map: PackedInt32Array = PackedInt32Array([3, 4, 5])
		if flip_arm:
			upper_map = PackedInt32Array([2, 1, 0])
			lower_map = PackedInt32Array([5, 4, 3])

		# 3 upper primary conductors (pins 0, 1, 2)
		for k: int in 3:
			var pa: Vector3 = pins_a[k]
			var pb: Vector3 = pins_b[upper_map[k]]
			var sag_u: float = clampf(dist * 0.015 + 0.05 * float(k % 2), 0.16, 0.72)
			if not _catenary_hits_building_obb(pa, pb, sag_u, buildings, -1):
				_append_catenary_wire(st, pa, pb, sag_u, 0.024, 5)

		# 2 lower secondary conductors + 1 telco messenger cable (pins 3, 5, and 6)
		for k2: int in [0, 2]:
			var la: Vector3 = pins_a[3 + k2]
			var lb: Vector3 = pins_b[lower_map[k2]]
			var sag_l: float = clampf(dist * 0.018 + 0.07, 0.20, 0.78)
			if not _catenary_hits_building_obb(la, lb, sag_l, buildings, -1):
				_append_catenary_wire(st, la, lb, sag_l, 0.021, 5)

		var sec_a: Vector3 = pins_a[6]
		var sec_b: Vector3 = pins_b[6]
		var sag_sec: float = clampf(dist * 0.020 + 0.09, 0.22, 0.84)
		if not _catenary_hits_building_obb(sec_a, sec_b, sag_sec, buildings, -1):
			_append_catenary_wire(st, sec_a, sec_b, sag_sec, 0.026, 5)

	# 2. Telecom dead-coils ("ikot ng kable") hung UNDERNEATH the telco clamp flush on the pole trunk
	var strap_steel: Color = Color(0.36, 0.38, 0.41)
	for p_i: int in pole_pin_sets.size():
		var pset: PackedVector3Array = pole_pin_sets[p_i]
		if pset.size() < 8:
			continue
		var clamp_pt: Vector3 = pset[7]
		var shaft_pt: Vector3 = pole_shaft_tops[p_i] if p_i < pole_shaft_tops.size() else clamp_pt
		var out_dir: Vector2 = Vector2(clamp_pt.x - shaft_pt.x, clamp_pt.z - shaft_pt.z)
		if out_dir.length_squared() < 0.0004:
			out_dir = Vector2(1.0, 0.0)
		else:
			out_dir = out_dir.normalized()
		var coil_yaw: float = atan2(out_dir.x, out_dir.y) + PI * 0.5
		var top_lashing: Vector3 = Vector3(shaft_pt.x + out_dir.x * 0.16, clamp_pt.y, shaft_pt.z + out_dir.y * 0.16)
		if hardware_kit != null:
			hardware_kit.cylinder(
				Vector3(shaft_pt.x, clamp_pt.y - 0.02, shaft_pt.z),
				0.155,
				0.155,
				0.055,
				8,
				strap_steel,
				strap_steel.darkened(0.2)
			)
		for loop_k: int in 2:
			var rx: float = 0.17 + 0.03 * float(loop_k)
			var ry: float = 0.20 + 0.03 * float(loop_k)
			# Hung strictly BELOW `top_lashing` so top of loop touches the lashing clamp!
			var lc: Vector3 = top_lashing - Vector3(0.0, ry, 0.0)
			_append_wire_coil(st, lc, rx, ry, coil_yaw + float(loop_k) * 0.16)

	# 3. Physically terminated service drop-wires: each placed building connects from an unobstructed pole
	#    within 28.5 m directly to its built-in `.glb` ceramic spool or pole-facing corner weatherhead (`0` roof clips)!
	for b_i: int in buildings.size():
		var b_rec: Dictionary = buildings[b_i]
		var b_center: Vector2 = b_rec["center"] as Vector2
		var pin_idx: int = 6 if (b_i % 2 == 0) else 7
		var best_pole: int = -1
		var best_score: float = 999.0
		var best_dist: float = 28.5
		var best_wh: Dictionary = {}

		for p_i: int in pole_pin_sets.size():
			var pset_p: PackedVector3Array = pole_pin_sets[p_i]
			if pset_p.size() < 8:
				continue
			var pin_candidate: Vector3 = pset_p[pin_idx]
			var pxz: Vector2 = Vector2(pin_candidate.x, pin_candidate.z)
			if pxz.distance_to(b_center) > 34.0:
				continue
			for mode_k: int in 2:
				var allow_b: bool = mode_k == 0
				var wh: Dictionary = compute_side_weatherhead(pxz, b_rec, allow_b)
				if wh.is_empty():
					break
				if mode_k == 1 and bool(wh.get("builtin", false)):
					continue
				var d_wh: float = float(wh["dist"])
				if d_wh < 1.5 or d_wh > 28.5:
					continue
				var cand_wall: Vector3 = wh["wall_pt"] as Vector3
				var cand_norm: Vector2 = wh["normal_xz"] as Vector2
				var cand_tip: Vector3 = cand_wall
				if not bool(wh.get("builtin", false)):
					var n_unit: Vector2 = cand_norm.normalized() if cand_norm.length_squared() > 0.0001 else Vector2(1.0, 0.0)
					cand_tip = cand_wall + Vector3(n_unit.x * 0.22, 0.0, n_unit.y * 0.22)
				var cand_sag: float = clampf(0.14 + d_wh * 0.015, 0.16, 0.52)
				if _catenary_hits_building_obb(pin_candidate, cand_tip, cand_sag, buildings, b_i):
					continue
				var score: float = d_wh - (3.5 if bool(wh.get("builtin", false)) else 0.0)
				if score < best_score:
					best_score = score
					best_dist = d_wh
					best_pole = p_i
					best_wh = wh
				if bool(wh.get("builtin", false)):
					break

		if best_pole >= 0:
			var pin_start: Vector3 = pole_pin_sets[best_pole][pin_idx]
			var wall_pt: Vector3 = best_wh["wall_pt"] as Vector3
			var norm_xz: Vector2 = best_wh["normal_xz"] as Vector2
			var spool_tip: Vector3 = wall_pt
			if not bool(best_wh.get("builtin", false)):
				spool_tip = append_weatherhead_fixture(hardware_kit, wall_pt, norm_xz)
			var drop_sag: float = clampf(0.14 + best_dist * 0.015, 0.16, 0.52)
			_append_catenary_wire(st, pin_start, spool_tip, drop_sag, 0.019, 5)

	st.generate_normals()
	var wire_mesh: ArrayMesh = st.commit()
	var wire_mat: StandardMaterial3D = StandardMaterial3D.new()
	wire_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	wire_mat.albedo_color = Color(0.13, 0.11, 0.13)
	wire_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wire_mesh.surface_set_material(0, wire_mat)
	return wire_mesh


## Appends a 3D galvanized service-entrance weatherhead conduit + Meralco meter riser + porcelain spool
## clevis flush on a building side firewall when the nearest pole is on the side opposite the built-in meter,
## returning the exact 3D spool pin tip where the wire lands.
static func append_weatherhead_fixture(kit: LowPoly, wall_pt: Vector3, outward_xz: Vector2) -> Vector3:
	var n2: Vector2 = outward_xz.normalized() if outward_xz.length_squared() > 0.0001 else Vector2(1.0, 0.0)
	var tip: Vector3 = wall_pt + Vector3(n2.x * 0.22, 0.0, n2.y * 0.22)
	if kit == null:
		return tip
	var yaw: float = atan2(n2.x, n2.y)
	var b: Basis = Basis(Vector3.UP, yaw)
	var galv_col: Color = Color(0.50, 0.52, 0.54)
	var galv_dark: Color = Color(0.34, 0.36, 0.38)
	var ceramic_col: Color = Color(0.84, 0.82, 0.77)
	var meter_glass: Color = Color(0.58, 0.68, 0.72)

	var pipe_bottom_y: float = maxf(1.65 - wall_pt.y, -wall_pt.y + 0.15)
	var pipe_len: float = maxf(0.30 - pipe_bottom_y, 0.80)

	kit.set_transform(Transform3D(b, wall_pt))
	# Steel wall cleat & extended standoff clevis arm (`+0.22 m` clear of eave drip)
	kit.box(Vector3(0.0, 0.0, 0.018), Vector3(0.16, 0.22, 0.036), galv_dark, galv_col)
	kit.box(Vector3(0.0, 0.0, 0.115), Vector3(0.052, 0.052, 0.20), galv_col, galv_dark)
	# Vertical PVC/RSC conduit riser pipe descending down the firewall to a Meralco kWh meter box!
	kit.cylinder(Vector3(0.09, pipe_bottom_y, 0.042), 0.024, 0.024, pipe_len, 6, galv_col, galv_dark)
	kit.box(Vector3(0.09, 0.32, 0.070), Vector3(0.082, 0.095, 0.11), galv_dark, galv_col)
	kit.box(Vector3(0.09, pipe_bottom_y - 0.12, 0.075), Vector3(0.22, 0.26, 0.13), galv_dark, galv_col)
	kit.cylinder(Vector3(0.09, pipe_bottom_y - 0.12, 0.14), 0.078, 0.078, 0.06, 8, meter_glass, galv_dark)
	# Porcelain strain spool insulator at exact wire termination point `(0, 0, 0.22)`
	kit.cylinder(Vector3(0.0, -0.042, 0.22), 0.040, 0.040, 0.085, 8, ceramic_col, ceramic_col.darkened(0.16))
	kit.reset_transform()
	return tip


## Appends a grounded 3D courtyard clothesline with 2 grounded galvanized T-posts (`Y = 0..2.24 m`)
## when `brgy_sampayan_3d.glb` is unavailable.
static func append_grounded_sampayan_with_posts(
	kit: LowPoly,
	a_ground: Vector3,
	b_ground: Vector3,
	cloth_palette: Array[Color],
	trim_palette: Array[Color],
	cord_col: Color,
	pin_col: Color,
	span_seed: int
) -> void:
	var post_h: float = 2.24
	var pipe_col: Color = Color(0.48, 0.50, 0.52)
	var footing_col: Color = Color(0.54, 0.52, 0.48)
	var span_vec: Vector3 = b_ground - a_ground
	var span_len: float = Vector2(span_vec.x, span_vec.z).length()
	if span_len < 1.2:
		return
	var yaw_s: float = atan2(span_vec.x, span_vec.z)
	var b_post: Basis = Basis(Vector3.UP, yaw_s)
	for end_pt: Vector3 in [a_ground, b_ground]:
		var base_p: Vector3 = Vector3(end_pt.x, 0.0, end_pt.z)
		kit.cylinder(base_p, 0.18, 0.15, 0.14, 8, footing_col, footing_col.darkened(0.16))
		kit.cylinder(base_p + Vector3(0.0, 0.12, 0.0), 0.042, 0.038, post_h, 8, pipe_col, pipe_col.darkened(0.18))
		kit.set_transform(Transform3D(b_post, base_p + Vector3(0.0, post_h + 0.10, 0.0)))
		kit.box(Vector3.ZERO, Vector3(0.42, 0.048, 0.048), pipe_col.lightened(0.08), pipe_col)
		kit.reset_transform()

	var rope_a: Vector3 = Vector3(a_ground.x, post_h + 0.08, a_ground.z)
	var rope_b: Vector3 = Vector3(b_ground.x, post_h + 0.08, b_ground.z)
	var sag_mid: float = clampf(span_len * 0.04, 0.09, 0.22)
	var cord_segs: int = 5
	for seg_i: int in cord_segs:
		var t0: float = float(seg_i) / float(cord_segs)
		var t1: float = float(seg_i + 1) / float(cord_segs)
		var p0: Vector3 = rope_a.lerp(rope_b, t0) - Vector3(0.0, 4.0 * sag_mid * t0 * (1.0 - t0), 0.0)
		var p1: Vector3 = rope_a.lerp(rope_b, t1) - Vector3(0.0, 4.0 * sag_mid * t1 * (1.0 - t1), 0.0)
		_append_3d_tube_segment(kit, p0, p1, 0.020, 6, cord_col)

	var pieces: int = 4
	for p_i: int in pieces:
		var t: float = (float(p_i) + 0.65) / float(pieces + 0.30)
		var hang_top: Vector3 = rope_a.lerp(rope_b, t) - Vector3(0.0, 4.0 * sag_mid * t * (1.0 - t), 0.0)
		var c_col: Color = cloth_palette[(span_seed * 3 + p_i) % cloth_palette.size()]
		var tr_col: Color = trim_palette[(span_seed + p_i * 2) % trim_palette.size()]
		var kind: int = (p_i + span_seed) % 4
		var sway_rad: float = deg_to_rad(4.5 * sin(float(span_seed * 5 + p_i) * 2.1 + 0.4))
		append_volumetric_garment(kit, yaw_s, hang_top, c_col, tr_col, pin_col, kind, sway_rad)


static func append_volumetric_garment(
	kit: LowPoly,
	yaw_s: float,
	hang_top: Vector3,
	c_col: Color,
	trim_col: Color,
	pin_col: Color,
	kind: int,
	sway_rad: float
) -> void:
	var g_basis: Basis = Basis(Vector3.UP, yaw_s) * Basis(Vector3.FORWARD, sway_rad)
	var lit_c: Color = c_col.lightened(0.08)
	var shade_c: Color = c_col.darkened(0.14)
	for pin_sign: float in [-1.0, 1.0]:
		var pin_pos: Vector3 = hang_top + g_basis * Vector3(0.0, 0.025, pin_sign * 0.14)
		kit.set_transform(Transform3D(g_basis, pin_pos))
		kit.box(Vector3.ZERO, Vector3(0.048, 0.105, 0.044), pin_col, pin_col.lightened(0.12))
		kit.reset_transform()

	if kind == 0:
		var shoulder_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.10, 0.0)
		kit.set_transform(Transform3D(g_basis, shoulder_pos))
		kit.rounded_slab(Vector3.ZERO, Vector3(0.145, 0.16, 0.44), 0.055, c_col, lit_c, 4)
		kit.cylinder(Vector3(0.0, 0.045, 0.0), 0.088, 0.082, 0.065, 8, trim_col, trim_col.darkened(0.25))
		kit.reset_transform()

		var torso_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.36, 0.0)
		kit.set_transform(Transform3D(g_basis, torso_pos))
		kit.rounded_slab(Vector3.ZERO, Vector3(0.135, 0.42, 0.40), 0.052, shade_c, c_col, 4)
		kit.box(Vector3(0.0, -0.19, 0.0), Vector3(0.142, 0.065, 0.41), trim_col, lit_c)
		kit.reset_transform()

		for side_s: float in [-1.0, 1.0]:
			var slv_start: Vector3 = hang_top + g_basis * Vector3(0.0, -0.09, side_s * 0.19)
			var slv_end: Vector3 = hang_top + g_basis * Vector3(0.0, -0.25, side_s * 0.36)
			var cuff_end: Vector3 = hang_top + g_basis * Vector3(0.0, -0.29, side_s * 0.40)
			_append_3d_tube_segment(kit, slv_start, slv_end, 0.074, 7, c_col)
			_append_3d_tube_segment(kit, slv_end, cuff_end, 0.078, 7, trim_col)
	elif kind == 1:
		var waist_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.075, 0.0)
		kit.set_transform(Transform3D(g_basis, waist_pos))
		kit.rounded_slab(Vector3.ZERO, Vector3(0.155, 0.12, 0.45), 0.06, trim_col, trim_col.lightened(0.10), 4)
		kit.rounded_slab(Vector3(0.0, -0.08, 0.0), Vector3(0.15, 0.10, 0.43), 0.055, c_col, lit_c, 4)
		kit.reset_transform()

		for leg_s: float in [-1.0, 1.0]:
			var leg_top: Vector3 = hang_top + g_basis * Vector3(0.0, -0.15, leg_s * 0.125)
			var leg_bot: Vector3 = hang_top + g_basis * Vector3(0.0, -0.52, leg_s * 0.148)
			var hem_bot: Vector3 = hang_top + g_basis * Vector3(0.0, -0.575, leg_s * 0.152)
			append_3d_tapered_tube(kit, leg_top, leg_bot, 0.092, 0.104, 8, c_col)
			append_3d_tapered_tube(kit, leg_bot, hem_bot, 0.106, 0.108, 8, trim_col)
	elif kind == 2:
		for strap_s: float in [-1.0, 1.0]:
			var strap_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.10, strap_s * 0.135)
			kit.set_transform(Transform3D(g_basis, strap_pos))
			kit.box(Vector3.ZERO, Vector3(0.095, 0.19, 0.088), c_col, lit_c)
			kit.reset_transform()
		var chest_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.39, 0.0)
		kit.set_transform(Transform3D(g_basis, chest_pos))
		kit.rounded_slab(Vector3.ZERO, Vector3(0.135, 0.45, 0.40), 0.052, c_col, lit_c, 4)
		kit.rounded_slab(Vector3(0.0, 0.06, 0.0), Vector3(0.148, 0.11, 0.41), 0.054, trim_col, trim_col.lightened(0.1), 4)
		kit.reset_transform()
	else:
		var fold_pos: Vector3 = hang_top + g_basis * Vector3(0.0, -0.02, 0.0)
		kit.set_transform(Transform3D(g_basis * Basis(Vector3.RIGHT, PI * 0.5), fold_pos))
		kit.cylinder(Vector3(0.0, -0.21, 0.0), 0.072, 0.072, 0.42, 8, lit_c, c_col)
		kit.reset_transform()
		for flap_s: float in [-1.0, 1.0]:
			var flap_basis: Basis = g_basis * Basis(Vector3.FORWARD, flap_s * 0.05)
			var flap_pos: Vector3 = hang_top + g_basis * Vector3(flap_s * 0.046, -0.28, 0.0)
			kit.set_transform(Transform3D(flap_basis, flap_pos))
			kit.box(Vector3.ZERO, Vector3(0.046, 0.50, 0.42), c_col if flap_s > 0.0 else shade_c, lit_c)
			kit.box(Vector3(0.0, -0.20, 0.0), Vector3(0.050, 0.055, 0.425), trim_col, trim_col)
			kit.reset_transform()


static func _append_3d_tube_segment(
	kit: LowPoly,
	a: Vector3,
	b: Vector3,
	radius: float,
	sides: int,
	color: Color
) -> void:
	append_3d_tapered_tube(kit, a, b, radius, radius, sides, color)


static func append_3d_tapered_tube(
	kit: LowPoly,
	a: Vector3,
	b: Vector3,
	r_start: float,
	r_end: float,
	sides: int,
	color: Color
) -> void:
	var seg: Vector3 = b - a
	var len_seg: float = seg.length()
	if len_seg < 0.005:
		return
	var y_axis: Vector3 = seg / len_seg
	var ref: Vector3 = Vector3.FORWARD if absf(y_axis.y) > 0.92 else Vector3.UP
	var x_axis: Vector3 = ref.cross(y_axis).normalized()
	var z_axis: Vector3 = y_axis.cross(x_axis).normalized()
	kit.set_transform(Transform3D(Basis(x_axis, y_axis, z_axis), a))
	kit.cylinder(Vector3.ZERO, r_start, r_end, len_seg, sides, color, color.darkened(0.18))
	kit.reset_transform()


static func _append_catenary_wire(
	st: SurfaceTool,
	a: Vector3,
	b: Vector3,
	sag: float,
	radius: float,
	segments: int
) -> void:
	var chord: Vector3 = b - a
	var horiz: Vector3 = Vector3(chord.x, 0.0, chord.z)
	var h_len: float = horiz.length()
	var side: Vector3 = Vector3(-horiz.z / h_len, 0.0, horiz.x / h_len) * radius if h_len > 0.001 else Vector3(radius, 0.0, 0.0)
	var up: Vector3 = Vector3(0.0, radius * 1.15, 0.0)
	for i: int in segments:
		var t0: float = float(i) / float(segments)
		var t1: float = float(i + 1) / float(segments)
		var p0: Vector3 = a.lerp(b, t0) - Vector3(0.0, 4.0 * sag * t0 * (1.0 - t0), 0.0)
		var p1: Vector3 = a.lerp(b, t1) - Vector3(0.0, 4.0 * sag * t1 * (1.0 - t1), 0.0)
		_append_quad(st, p0 - up, p0 + up, p1 + up, p1 - up)
		_append_quad(st, p0 - side, p0 + side, p1 + side, p1 - side)


static func _append_wire_coil(st: SurfaceTool, center: Vector3, rx: float, ry: float, yaw: float) -> void:
	var steps: int = 8
	var basis: Basis = Basis(Vector3.UP, yaw)
	var thick: Vector3 = Vector3(0.0, 0.026, 0.0)
	var wide: Vector3 = basis * Vector3(0.022, 0.0, 0.0)
	for i: int in steps:
		var a0: float = TAU * float(i) / float(steps)
		var a1: float = TAU * float(i + 1) / float(steps)
		var p0: Vector3 = center + basis * Vector3(cos(a0) * rx, sin(a0) * ry, 0.04 * sin(a0 * 2.0))
		var p1: Vector3 = center + basis * Vector3(cos(a1) * rx, sin(a1) * ry, 0.04 * sin(a1 * 2.0))
		_append_quad(st, p0 - thick, p0 + thick, p1 + thick, p1 - thick)
		_append_quad(st, p0 - wide, p0 + wide, p1 + wide, p1 - wide)


static func _append_quad(st: SurfaceTool, v0: Vector3, v1: Vector3, v2: Vector3, v3: Vector3) -> void:
	st.add_vertex(v0)
	st.add_vertex(v1)
	st.add_vertex(v2)
	st.add_vertex(v0)
	st.add_vertex(v2)
	st.add_vertex(v3)
