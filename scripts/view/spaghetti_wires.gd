class_name SpaghettiWires
extends RefCounted
## Batched 90s Philippine Provincial Overhead Electric-Pole Spaghetti Wiring (`Y >= 4.55 m`),
## 3D Pole Election & Siphoning (*Malabanan*) Notice Boards, and True 3D Volumetric
## *Sampayan* Hanging Clothes (extruded 3D tubular sleeves, 3D collar rings, puffed 3D
## chest prisms, 3D tubular *puruntong* shorts legs, and folded 3D *tuwalya* — zero flat quads).

const MIN_WIRE_Y: float = 4.55


## Builds the single-draw-call chaotic overhead electric-pole wiring ArrayMesh (`Y >= 4.55 m`).
static func build_wire_network_mesh(
	pole_tops: Array[Vector3],
	spans: Array[Vector2i],
	alley_corridors: Array[Vector4],
	obs_rects: Array[Rect2],
	ring_r: float
) -> ArrayMesh:
	var st: SurfaceTool = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	# 1. Pole-to-pole trunk & pathway/alley overhead catenary bundles
	for s_idx: int in spans.size():
		var pair: Vector2i = spans[s_idx]
		if pair.x < 0 or pair.x >= pole_tops.size() or pair.y < 0 or pair.y >= pole_tops.size():
			continue
		var p_a: Vector3 = pole_tops[pair.x]
		var p_b: Vector3 = pole_tops[pair.y]
		var horiz: Vector3 = Vector3(p_b.x - p_a.x, 0.0, p_b.z - p_a.z)
		var dist: float = horiz.length()
		if dist < 1.0:
			continue
		var side_dir: Vector3 = Vector3(-horiz.z, 0.0, horiz.x) / dist
		var is_alley_span: bool = pair.x >= 24 or pair.y >= 24
		var strand_count: int = 6 if is_alley_span else 5
		for strand: int in strand_count:
			var f_st: float = float(strand)
			var spread: float = (f_st - 2.2) * 0.22 + 0.08 * sin(float(s_idx * 5 + strand) * 1.9)
			var y_drop: float = -0.12 * f_st + 0.05 * cos(float(s_idx + strand) * 2.3)
			var start_p: Vector3 = p_a + side_dir * spread + Vector3(0.0, y_drop, 0.0)
			var end_p: Vector3 = p_b + side_dir * (spread * 0.82) + Vector3(0.0, y_drop - 0.07 * sin(f_st), 0.0)
			start_p.y = maxf(start_p.y, MIN_WIRE_Y + 0.35)
			end_p.y = maxf(end_p.y, MIN_WIRE_Y + 0.35)
			var sag_cap: float = 0.72 if is_alley_span else 1.15
			var sag: float = clampf(dist * (0.015 + 0.004 * f_st) + 0.12 * absf(sin(float(s_idx * 3 + strand))), 0.26, sag_cap)
			var radius_w: float = 0.034 if strand == 0 else 0.024
			_append_catenary_wire(st, start_p, end_p, sag, radius_w, 7)
			if is_alley_span and strand == 1 and s_idx % 2 == 0:
				var mid_sag_pt: Vector3 = start_p.lerp(end_p, 0.5) - Vector3(0.0, sag + 0.06, 0.0)
				mid_sag_pt.y = maxf(mid_sag_pt.y, MIN_WIRE_Y + 0.36)
				_append_wire_coil(st, mid_sag_pt, 0.22, 0.28, atan2(horiz.x, horiz.z))

	# 2. Overhead cross-pathway & alley utility spans (`Y >= 4.85 m`) between opposing shophouse eaves
	for side_z: float in [1.0, -1.0]:
		for c_idx: int in alley_corridors.size():
			var corr: Vector4 = alley_corridors[c_idx]
			var x_l: float = corr.x * side_z
			var x_r: float = corr.y * side_z
			var stations: int = 4
			for st_i: int in stations:
				var frac: float = (float(st_i) + 0.45) / float(stations)
				var z_base: float = lerpf(corr.z, corr.w, frac) * side_z
				var z_diag: float = lerpf(corr.z, corr.w, clampf(frac + 0.26, 0.05, 0.98)) * side_z
				var y_l: float = 5.65 + 0.95 * absf(sin(float(c_idx * 7 + st_i * 3) + side_z * 0.5))
				var y_r: float = 5.70 + 0.90 * absf(cos(float(c_idx * 5 + st_i * 4) - side_z * 0.3))
				var anchor_l: Vector3 = Vector3(x_l, y_l, z_base)
				var anchor_r: Vector3 = Vector3(x_r, y_r, z_base + 0.7 * side_z)
				var anchor_r_diag: Vector3 = Vector3(x_r, clampf(y_r - 0.22, 5.45, 6.8), z_diag)
				var anchor_l_diag: Vector3 = Vector3(x_l, clampf(y_l - 0.20, 5.45, 6.8), z_diag)
				_append_wall_bracket(st, anchor_l, signf(x_r - x_l) * 0.24)
				_append_wall_bracket(st, anchor_r, signf(x_l - x_r) * 0.24)
				for w_k: int in 3:
					var wy: float = -0.12 * float(w_k)
					var wz: float = (float(w_k) - 1.0) * 0.22 * side_z
					var sag_c: float = 0.28 + 0.09 * float(w_k) + 0.06 * absf(sin(float(st_i + w_k)))
					_append_catenary_wire(
						st,
						anchor_l + Vector3(0.0, wy, wz),
						anchor_r + Vector3(0.0, wy - 0.05, -wz),
						sag_c,
						0.025 if w_k == 0 else 0.019,
						6
					)
				_append_catenary_wire(st, anchor_l, anchor_r_diag, 0.42, 0.021, 6)
				_append_catenary_wire(st, anchor_r, anchor_l_diag, 0.44, 0.021, 6)
				if (st_i + c_idx) % 2 == 0:
					var loop_pt: Vector3 = anchor_l.lerp(anchor_r, 0.36) - Vector3(0.0, 0.32, 0.0)
					loop_pt.y = maxf(loop_pt.y, MIN_WIRE_Y + 0.38)
					_append_wire_coil(st, loop_pt, 0.21, 0.28, float(st_i) * 0.9 + 0.4)

	# 3. Tangled telecom slack coils ("ikot ng kable") mounted high on every pole (`Y >= 4.85 m`)
	for p_i: int in pole_tops.size():
		var top_p: Vector3 = pole_tops[p_i]
		var coil_drop: float = 0.55 if p_i >= 24 else 0.95
		var coil_center: Vector3 = top_p - Vector3(0.0, coil_drop, 0.0)
		coil_center.y = maxf(coil_center.y, MIN_WIRE_Y + 0.68)
		for loop_k: int in 4:
			var loop_yaw: float = float(p_i) * 1.1 + float(loop_k) * 0.85
			var loop_r_x: float = 0.26 + 0.05 * float(loop_k)
			var loop_r_y: float = 0.30 + 0.06 * float(loop_k)
			var lc: Vector3 = coil_center + Vector3(0.0, -0.07 * float(loop_k), 0.0)
			lc.y = maxf(lc.y, MIN_WIRE_Y + loop_r_y + 0.05)
			_append_wire_coil(st, lc, loop_r_x, loop_r_y, loop_yaw)

	# 4. Diagonal service drop-wires fanning from poles into nearby interior & perimeter house eaves
	for p_i: int in pole_tops.size():
		var junction_p: Vector3 = pole_tops[p_i] - Vector3(0.0, 0.42 if p_i >= 24 else 0.85, 0.0)
		junction_p.y = maxf(junction_p.y, MIN_WIRE_Y + 0.55)
		var max_drops: int = 5 if p_i >= 24 else 3
		var drops_added: int = 0
		if not obs_rects.is_empty():
			for o_i: int in obs_rects.size():
				if drops_added >= max_drops:
					break
				var r_obs: Rect2 = obs_rects[(o_i + p_i * 3) % obs_rects.size()]
				var corner_2d: Vector2 = Vector2(
					clampf(junction_p.x, r_obs.position.x + 0.35, r_obs.end.x - 0.35),
					clampf(junction_p.z, r_obs.position.y + 0.35, r_obs.end.y - 0.35)
				)
				var d2: float = Vector2(junction_p.x, junction_p.z).distance_to(corner_2d)
				if d2 > 1.2 and d2 < 26.0:
					var eave_h: float = 5.20 + 0.80 * absf(sin(float(o_i + p_i) * 1.7))
					var eave_pt: Vector3 = Vector3(corner_2d.x, eave_h, corner_2d.y)
					_append_catenary_wire(st, junction_p, eave_pt, clampf(0.22 + d2 * 0.011, 0.24, 0.50), 0.020, 5)
					drops_added += 1
		if p_i >= 12 and p_i < 24:
			var rad_dir: Vector2 = Vector2(junction_p.x, junction_p.z).normalized()
			for branch: float in [-3.8, 3.8]:
				var tang: Vector2 = Vector2(-rad_dir.y, rad_dir.x) * branch
				var eave_out: Vector2 = rad_dir * (ring_r + 5.8) + tang
				_append_catenary_wire(st, junction_p, Vector3(eave_out.x, 5.30, eave_out.y), 0.30, 0.019, 4)

	st.generate_normals()
	var wire_mesh: ArrayMesh = st.commit()
	var wire_mat: StandardMaterial3D = StandardMaterial3D.new()
	wire_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	wire_mat.albedo_color = Color(0.11, 0.09, 0.12)
	wire_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	wire_mesh.surface_set_material(0, wire_mat)
	return wire_mesh


## Appends a sagging 3D clothesline cord + 3D volumetric extruded garments (T-shirts, shorts, sandos, towels).
## Every garment uses closed 3D cylinders, rounded slabs, and beveled 3D boxes — ZERO flat quads!
static func append_volumetric_sampayan_span(
	kit: LowPoly,
	a_pt: Vector3,
	b_pt: Vector3,
	cloth_palette: Array[Color],
	trim_palette: Array[Color],
	cord_col: Color,
	pin_col: Color,
	span_seed: int
) -> void:
	var safe_a: Vector3 = Vector3(a_pt.x, maxf(a_pt.y, 4.85), a_pt.z)
	var safe_b: Vector3 = Vector3(b_pt.x, maxf(b_pt.y, 4.85), b_pt.z)
	var span_vec: Vector3 = safe_b - safe_a
	var span_len: float = span_vec.length()
	if span_len < 0.6:
		return
	var sag_mid: float = clampf(span_len * 0.045, 0.12, 0.32)
	var cord_segs: int = 5
	for seg_i: int in cord_segs:
		var t0: float = float(seg_i) / float(cord_segs)
		var t1: float = float(seg_i + 1) / float(cord_segs)
		var p0: Vector3 = safe_a.lerp(safe_b, t0) - Vector3(0.0, 4.0 * sag_mid * t0 * (1.0 - t0), 0.0)
		var p1: Vector3 = safe_a.lerp(safe_b, t1) - Vector3(0.0, 4.0 * sag_mid * t1 * (1.0 - t1), 0.0)
		_append_3d_tube_segment(kit, p0, p1, 0.024, 6, cord_col)

	var dir_s: Vector3 = span_vec / span_len
	var yaw_s: float = atan2(dir_s.x, dir_s.z)
	var pieces: int = 5
	for p_i: int in pieces:
		var t: float = (float(p_i) + 0.62) / float(pieces + 0.24)
		var hang_top: Vector3 = safe_a.lerp(safe_b, t) - Vector3(0.0, 4.0 * sag_mid * t * (1.0 - t), 0.0)
		var c_col: Color = cloth_palette[(span_seed * 3 + p_i) % cloth_palette.size()]
		var tr_col: Color = trim_palette[(span_seed + p_i * 2) % trim_palette.size()]
		var kind: int = (p_i + span_seed) % 4
		var sway_rad: float = deg_to_rad(6.0 * sin(float(span_seed * 5 + p_i) * 2.1 + 0.4))
		append_volumetric_garment(kit, yaw_s, hang_top, c_col, tr_col, pin_col, kind, sway_rad)


## Appends a single closed-volume 3D hanging garment (`kind` 0..3) at `hang_top`.
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
	# Two 3D wooden sipit clothespins gripping the clothesline
	for pin_sign: float in [-1.0, 1.0]:
		var pin_pos: Vector3 = hang_top + g_basis * Vector3(0.0, 0.025, pin_sign * 0.14)
		kit.set_transform(Transform3D(g_basis, pin_pos))
		kit.box(Vector3.ZERO, Vector3(0.048, 0.105, 0.044), pin_col, pin_col.lightened(0.12))
		kit.reset_transform()

	if kind == 0:
		# 1. Volumetric 3D Pinoy T-Shirt (Kamiseta): puffed 3D chest + 3D neck ring + 2 cylindrical 3D sleeves!
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
		# 2. Volumetric 3D Puruntong Basketball Shorts: 3D gathered waistband + 2 flared 3D tubular legs!
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
		# 3. Volumetric 3D Barrio Basketball Sando: sculpted 3D chest + 2 thick 3D shoulder straps + raised stripe!
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
		# 4. Volumetric 3D Draped Bath Towel (Tuwalya): rounded U-fold over cord + 2 separated 3D hanging panels!
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


## Appends 3D wooden/tin Election ("VOTE!") & Siphoning Service ("MALABANAN") notice boards around electric poles.
static func append_pole_posters(kit: LowPoly, pole_spots: Array[Vector3]) -> void:
	var wire_tie_col: Color = Color(0.22, 0.21, 0.23)
	var malabanan_yellow: Color = Color(0.84, 0.68, 0.18)
	var malabanan_blue: Color = Color(0.14, 0.30, 0.58)
	var vote_paper: Color = Color(0.86, 0.82, 0.72)
	var vote_red: Color = Color(0.72, 0.20, 0.18)
	var vote_blue: Color = Color(0.18, 0.36, 0.64)
	var wood_back: Color = Color(0.42, 0.31, 0.20)

	for idx: int in pole_spots.size():
		var spot: Vector3 = pole_spots[idx]
		var face_yaw: float = atan2(-spot.x, -spot.z) + float(idx % 3 - 1) * 0.35
		var p_basis: Basis = Basis(Vector3.UP, face_yaw)

		# Tie bands around pole at Y = 2.05m and Y = 2.95m
		kit.cylinder(spot + Vector3(0.0, 2.02, 0.0), 0.185, 0.185, 0.04, 8, wire_tie_col, wire_tie_col)
		kit.cylinder(spot + Vector3(0.0, 2.92, 0.0), 0.175, 0.175, 0.04, 8, wire_tie_col, wire_tie_col)

		# Lower tin notice: "MALABANAN SIPSIP POSO NEGRO" (yellow tin plate + blue header bar)
		var tin_pos: Vector3 = spot + Vector3(0.0, 2.12, 0.0) + p_basis * Vector3(0.0, 0.0, 0.19)
		kit.set_transform(Transform3D(p_basis * Basis(Vector3.FORWARD, deg_to_rad(2.5 * sin(float(idx)))), tin_pos))
		kit.box(Vector3.ZERO, Vector3(0.54, 0.38, 0.036), malabanan_yellow, wood_back)
		kit.box(Vector3(0.0, 0.11, 0.014), Vector3(0.50, 0.10, 0.024), malabanan_blue, malabanan_blue)
		kit.box(Vector3(0.0, -0.06, 0.014), Vector3(0.44, 0.055, 0.022), vote_red, vote_red)
		kit.reset_transform()

		# Upper election poster board: tilted wooden frame + campaign tarp header/stripes
		var elec_yaw: float = face_yaw + PI * 0.55
		var e_basis: Basis = Basis(Vector3.UP, elec_yaw)
		var elec_pos: Vector3 = spot + Vector3(0.0, 2.86, 0.0) + e_basis * Vector3(0.0, 0.0, 0.18)
		kit.set_transform(Transform3D(e_basis * Basis(Vector3.FORWARD, deg_to_rad(-3.2 * cos(float(idx)))), elec_pos))
		kit.box(Vector3.ZERO, Vector3(0.58, 0.72, 0.042), wood_back, wood_back)
		kit.box(Vector3(0.0, 0.0, 0.018), Vector3(0.54, 0.68, 0.024), vote_paper, vote_paper)
		var banner_c: Color = vote_red if idx % 2 == 0 else vote_blue
		kit.box(Vector3(0.0, 0.21, 0.028), Vector3(0.50, 0.16, 0.020), banner_c, banner_c)
		kit.box(Vector3(0.0, -0.20, 0.028), Vector3(0.48, 0.11, 0.020), malabanan_yellow, malabanan_yellow)
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


static func _append_wall_bracket(st: SurfaceTool, wall_pt: Vector3, out_x: float) -> void:
	var safe_wall: Vector3 = Vector3(wall_pt.x, maxf(wall_pt.y, MIN_WIRE_Y + 0.30), wall_pt.z)
	var tip: Vector3 = safe_wall + Vector3(out_x, 0.0, 0.0)
	var brace_low: Vector3 = safe_wall + Vector3(0.0, -0.20, 0.0)
	var bar_z: Vector3 = Vector3(0.0, 0.0, 0.22)
	var thick: Vector3 = Vector3(0.0, 0.032, 0.0)
	_append_quad(st, safe_wall - thick, safe_wall + thick, tip + thick, tip - thick)
	_append_quad(st, brace_low - thick, brace_low + thick, tip + thick, tip - thick)
	_append_quad(st, tip - bar_z - thick, tip - bar_z + thick, tip + bar_z + thick, tip + bar_z - thick)


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
		p0.y = maxf(p0.y, MIN_WIRE_Y + radius * 1.2)
		p1.y = maxf(p1.y, MIN_WIRE_Y + radius * 1.2)
		_append_quad(st, p0 - up, p0 + up, p1 + up, p1 - up)
		_append_quad(st, p0 - side, p0 + side, p1 + side, p1 - side)


static func _append_wire_coil(st: SurfaceTool, center: Vector3, rx: float, ry: float, yaw: float) -> void:
	var steps: int = 10
	var basis: Basis = Basis(Vector3.UP, yaw)
	var thick: Vector3 = Vector3(0.0, 0.030, 0.0)
	var wide: Vector3 = basis * Vector3(0.026, 0.0, 0.0)
	for i: int in steps:
		var a0: float = TAU * float(i) / float(steps)
		var a1: float = TAU * float(i + 1) / float(steps)
		var p0: Vector3 = center + basis * Vector3(cos(a0) * rx, sin(a0) * ry, 0.06 * sin(a0 * 2.0))
		var p1: Vector3 = center + basis * Vector3(cos(a1) * rx, sin(a1) * ry, 0.06 * sin(a1 * 2.0))
		p0.y = maxf(p0.y, MIN_WIRE_Y + 0.04)
		p1.y = maxf(p1.y, MIN_WIRE_Y + 0.04)
		_append_quad(st, p0 - thick, p0 + thick, p1 + thick, p1 - thick)
		_append_quad(st, p0 - wide, p0 + wide, p1 + wide, p1 - wide)


static func _append_quad(st: SurfaceTool, v0: Vector3, v1: Vector3, v2: Vector3, v3: Vector3) -> void:
	var c0: Vector3 = Vector3(v0.x, maxf(v0.y, MIN_WIRE_Y), v0.z)
	var c1: Vector3 = Vector3(v1.x, maxf(v1.y, MIN_WIRE_Y), v1.z)
	var c2: Vector3 = Vector3(v2.x, maxf(v2.y, MIN_WIRE_Y), v2.z)
	var c3: Vector3 = Vector3(v3.x, maxf(v3.y, MIN_WIRE_Y), v3.z)
	st.add_vertex(c0)
	st.add_vertex(c1)
	st.add_vertex(c2)
	st.add_vertex(c0)
	st.add_vertex(c2)
	st.add_vertex(c3)
