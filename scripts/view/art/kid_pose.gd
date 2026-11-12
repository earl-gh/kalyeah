class_name KidPose
extends SkeletonModifier3D
## Bends a kid's pelvis, spine, arms and legs to places and angles in the kid's own space (x right, y up, -z forward, metres at the kid's
## height), after the clips have played (a SkeletonModifier3D runs after the animation, so the clip keeps the rest of the body): Tumbang
## Preso can acts (both hands on a can, arms thrown up, kicking foot swung through the can) and authentic street locomotion postures
## (analog walk sway, stealth crouch and crouch-walk, baseball slide, jump launch tuck and landing knee-flex compression). View only.
## `KidModel` sets the targets and weights every frame; at weight 0 nothing is touched.

const HIPS_BONE: String = "mixamorig_Hips"
const SPINE_BONES: Array[String] = ["mixamorig_Spine", "mixamorig_Spine1", "mixamorig_Spine2"]
const SPINE_SHARES: Array[float] = [0.35, 0.35, 0.30]
const HEAD_BONES: Array[String] = ["mixamorig_Neck", "mixamorig_Head"]
const HEAD_SHARES: Array[float] = [0.45, 0.55]
const LEFT_ARM: Array[String] = ["mixamorig_LeftArm", "mixamorig_LeftForeArm", "mixamorig_LeftHand"]
const RIGHT_ARM: Array[String] = ["mixamorig_RightArm", "mixamorig_RightForeArm", "mixamorig_RightHand"]
const LEFT_LEG: Array[String] = ["mixamorig_LeftUpLeg", "mixamorig_LeftLeg", "mixamorig_LeftFoot"]
const RIGHT_LEG: Array[String] = ["mixamorig_RightUpLeg", "mixamorig_RightLeg", "mixamorig_RightFoot"]
## The elbows bend out and down, the knees forward and slightly out to each leg's own side.
const ELBOW_POLE: Vector3 = Vector3(1.0, -0.8, 0.2)
const KNEE_POLE: Vector3 = Vector3(0.3, 0.1, -1.0)
const LEFT_KNEE_POLE: Vector3 = Vector3(-0.3, 0.1, -1.0)
const ANKLE_REST_Y: float = 0.11

## The kid whose space the targets are in (a Node3D with the skeleton under it).
var frame: Node3D
## Where the left and right hand go, and how much (0 to 1).
var hand_left: Vector3 = Vector3.ZERO
var hand_right: Vector3 = Vector3.ZERO
var arm_weight: float = 0.0
## Where the right foot (or kicking foot) goes, and how much.
var foot: Vector3 = Vector3.ZERO
var foot_weight: float = 0.0
## Optional explicit left foot target (jump tuck, baseball slide).
var left_foot: Vector3 = Vector3.ZERO
var left_foot_weight: float = 0.0
var left_knee_pole: Vector3 = LEFT_KNEE_POLE
var right_knee_pole: Vector3 = KNEE_POLE
## Pelvis shift in kid space (e.g. negative Y lowers hips for crouch, slide, or landing knee-flex).
var pelvis_offset: Vector3 = Vector3.ZERO
var pelvis_weight: float = 0.0
## Additive kid-space Euler rotations (x = pitch, y = yaw, z = roll; negative x leans forward toward -Z).
var hips_rot: Vector3 = Vector3.ZERO
var spine_rot: Vector3 = Vector3.ZERO
var head_rot: Vector3 = Vector3.ZERO
var torso_weight: float = 0.0
## Pins both ankles to the animation clip's current foot placements (with optional stride/lift scaling) while lowering the pelvis.
var pin_feet_weight: float = 0.0
var stride_scale: float = 1.0
var foot_lift_scale: float = 1.0

var report: String = ""
var _bones: Dictionary[String, int] = {}


func reset_locomotion() -> void:
	left_foot = Vector3.ZERO
	left_foot_weight = 0.0
	left_knee_pole = LEFT_KNEE_POLE
	right_knee_pole = KNEE_POLE
	pelvis_offset = Vector3.ZERO
	pelvis_weight = 0.0
	hips_rot = Vector3.ZERO
	spine_rot = Vector3.ZERO
	head_rot = Vector3.ZERO
	torso_weight = 0.0
	pin_feet_weight = 0.0
	stride_scale = 1.0
	foot_lift_scale = 1.0


func has_any_weight() -> bool:
	return (
		arm_weight > 0.0
		or foot_weight > 0.0
		or left_foot_weight > 0.0
		or pelvis_weight > 0.0
		or torso_weight > 0.0
		or pin_feet_weight > 0.0
	)


func _process_modification_with_delta(_delta: float) -> void:
	var skeleton: Skeleton3D = get_skeleton()
	if skeleton == null or frame == null or not has_any_weight():
		return
	# kid space -> skeleton space
	var to_skeleton: Transform3D = (frame.global_transform.affine_inverse() * skeleton.global_transform).affine_inverse()
	var to_kid: Transform3D = to_skeleton.affine_inverse()
	var pinned_left_kid: Vector3 = Vector3.ZERO
	var pinned_right_kid: Vector3 = Vector3.ZERO
	if pin_feet_weight > 0.0:
		var left_tip: int = _bone(skeleton, LEFT_LEG[2])
		var right_tip: int = _bone(skeleton, RIGHT_LEG[2])
		if left_tip >= 0:
			pinned_left_kid = _scaled_clip_foot(to_kid * skeleton.get_bone_global_pose(left_tip).origin)
		if right_tip >= 0:
			pinned_right_kid = _scaled_clip_foot(to_kid * skeleton.get_bone_global_pose(right_tip).origin)

	var hips: int = _bone(skeleton, HIPS_BONE)
	if hips >= 0:
		if pelvis_weight > 0.0 and pelvis_offset != Vector3.ZERO:
			var hips_global: Transform3D = skeleton.get_bone_global_pose(hips)
			var delta_skel: Vector3 = (to_skeleton.basis * pelvis_offset) * pelvis_weight
			var hips_parent: int = skeleton.get_bone_parent(hips)
			var parent_global: Transform3D = skeleton.get_bone_global_pose(hips_parent) if hips_parent >= 0 else Transform3D.IDENTITY
			skeleton.set_bone_pose_position(hips, parent_global.affine_inverse() * (hips_global.origin + delta_skel))
			skeleton.force_update_bone_child_transform(hips)
		if torso_weight > 0.0 and hips_rot != Vector3.ZERO:
			_rotate_kid_space(skeleton, hips, hips_rot, torso_weight, to_skeleton.basis)

	if torso_weight > 0.0:
		if spine_rot != Vector3.ZERO:
			for i: int in SPINE_BONES.size():
				var sb: int = _bone(skeleton, SPINE_BONES[i])
				if sb >= 0:
					_rotate_kid_space(skeleton, sb, spine_rot * SPINE_SHARES[i], torso_weight, to_skeleton.basis)
		if head_rot != Vector3.ZERO:
			for i: int in HEAD_BONES.size():
				var hb: int = _bone(skeleton, HEAD_BONES[i])
				if hb >= 0:
					_rotate_kid_space(skeleton, hb, head_rot * HEAD_SHARES[i], torso_weight, to_skeleton.basis)

	if left_foot_weight > 0.0:
		_chain(skeleton, LEFT_LEG, to_skeleton * left_foot, to_skeleton.basis * left_knee_pole, left_foot_weight)
	elif pin_feet_weight > 0.0:
		_chain(skeleton, LEFT_LEG, to_skeleton * pinned_left_kid, to_skeleton.basis * LEFT_KNEE_POLE, pin_feet_weight)

	if foot_weight > 0.0:
		_chain(skeleton, RIGHT_LEG, to_skeleton * foot, to_skeleton.basis * right_knee_pole, foot_weight)
		var after: Vector3 = skeleton.get_bone_global_pose(_bone(skeleton, RIGHT_LEG[2])).origin
		report = "target %s reached %s w %s" % [to_skeleton * foot, after, foot_weight]
	elif pin_feet_weight > 0.0:
		_chain(skeleton, RIGHT_LEG, to_skeleton * pinned_right_kid, to_skeleton.basis * KNEE_POLE, pin_feet_weight)

	if arm_weight > 0.0:
		_chain(skeleton, LEFT_ARM, to_skeleton * hand_left, _side_pole(ELBOW_POLE, -1.0, to_skeleton), arm_weight)
		_chain(skeleton, RIGHT_ARM, to_skeleton * hand_right, _side_pole(ELBOW_POLE, 1.0, to_skeleton), arm_weight)


func _scaled_clip_foot(foot_kid: Vector3) -> Vector3:
	var out: Vector3 = foot_kid
	out.z *= stride_scale
	if out.y > ANKLE_REST_Y:
		out.y = ANKLE_REST_Y + (out.y - ANKLE_REST_Y) * foot_lift_scale
	return out


func _rotate_kid_space(skeleton: Skeleton3D, bone: int, euler_kid: Vector3, weight: float, kid_to_skel_basis: Basis) -> void:
	if bone < 0 or weight <= 0.0:
		return
	var skel_axes: Basis = kid_to_skel_basis.orthonormalized()
	var skel_rot: Basis = skel_axes * Basis.from_euler(euler_kid) * skel_axes.inverse()
	var here: Transform3D = skeleton.get_bone_global_pose(bone)
	var parent: int = skeleton.get_bone_parent(bone)
	var parent_basis: Basis = skeleton.get_bone_global_pose(parent).basis.orthonormalized() if parent >= 0 else Basis.IDENTITY
	var local: Basis = parent_basis.inverse() * (skel_rot * here.basis.orthonormalized())
	var target_q: Quaternion = local.get_rotation_quaternion()
	skeleton.set_bone_pose_rotation(bone, skeleton.get_bone_pose_rotation(bone).slerp(target_q, clampf(weight, 0.0, 1.0)))
	skeleton.force_update_bone_child_transform(bone)


## The elbow pole for the arm on `side` (-1 left, 1 right): out to its own side, in skeleton space.
func _side_pole(pole: Vector3, side: float, to_skeleton: Transform3D) -> Vector3:
	return to_skeleton.basis * Vector3(pole.x * side, pole.y, pole.z)


func _bone(skeleton: Skeleton3D, name: String) -> int:
	if not _bones.has(name):
		_bones[name] = skeleton.find_bone(name)
	return _bones[name]


## Two-bone chain `names` (root, middle, tip) to `target` (skeleton space), the middle joint bent toward `pole`.
func _chain(skeleton: Skeleton3D, names: Array[String], target: Vector3, pole: Vector3, weight: float) -> void:
	var root: int = _bone(skeleton, names[0])
	var middle: int = _bone(skeleton, names[1])
	var tip: int = _bone(skeleton, names[2])
	if root < 0 or middle < 0 or tip < 0:
		return
	var s: Vector3 = skeleton.get_bone_global_pose(root).origin
	var e: Vector3 = skeleton.get_bone_global_pose(middle).origin
	var t: Vector3 = skeleton.get_bone_global_pose(tip).origin
	var upper: float = s.distance_to(e)
	var lower: float = e.distance_to(t)
	var reach: Vector3 = target - s
	var distance: float = clampf(reach.length(), absf(upper - lower) + 0.001, upper + lower - 0.001)
	var along: Vector3 = reach.normalized()
	var a: float = (upper * upper - lower * lower + distance * distance) / (2.0 * distance)
	var h: float = sqrt(maxf(upper * upper - a * a, 0.0))
	var side: Vector3 = pole - along * pole.dot(along)
	side = side.normalized() if side.length_squared() > 0.000001 else Vector3.UP
	var elbow: Vector3 = s + along * a + side * h
	_aim(skeleton, root, middle, elbow - s, weight)
	var moved: Vector3 = skeleton.get_bone_global_pose(middle).origin
	_aim(skeleton, middle, tip, (s + along * distance) - moved, weight)


## Turns `bone` the shortest way so that the line to its child points along `direction` (skeleton space).
func _aim(skeleton: Skeleton3D, bone: int, child: int, direction: Vector3, weight: float) -> void:
	var here: Transform3D = skeleton.get_bone_global_pose(bone)
	var line: Vector3 = skeleton.get_bone_global_pose(child).origin - here.origin
	if line.length_squared() < 0.000001 or direction.length_squared() < 0.000001:
		return
	var turn: Quaternion = Quaternion(line.normalized(), direction.normalized())
	var parent: int = skeleton.get_bone_parent(bone)
	var parent_basis: Basis = skeleton.get_bone_global_pose(parent).basis if parent >= 0 else Basis.IDENTITY
	var local: Basis = parent_basis.orthonormalized().inverse() * (Basis(turn) * here.basis.orthonormalized())
	var rotation: Quaternion = local.get_rotation_quaternion()
	skeleton.set_bone_pose_rotation(bone, skeleton.get_bone_pose_rotation(bone).slerp(rotation, weight))
	skeleton.force_update_bone_child_transform(bone)
