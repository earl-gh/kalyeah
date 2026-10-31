class_name KidPose
extends SkeletonModifier3D
## Bends a kid's arms and one leg to places in the kid's own space (x right, y up, -z forward, metres at the kid's height), after the clips
## have played (a SkeletonModifier3D runs after the animation, so the clip keeps the rest of the body): both hands on a can, the arms thrown
## up, the kicking foot swung through the can. View only. `KidModel` sets the targets and weights every frame; at weight 0 nothing is touched.
## Each arm and the leg is a two-bone chain (the shoulder or hip, the elbow or knee, the hand or foot): the middle joint bends toward the
## `pole` side, the bones are turned the shortest way onto the chain, so the rig's own axes never matter.

const LEFT_ARM: Array[String] = ["mixamorig_LeftArm", "mixamorig_LeftForeArm", "mixamorig_LeftHand"]
const RIGHT_ARM: Array[String] = ["mixamorig_RightArm", "mixamorig_RightForeArm", "mixamorig_RightHand"]
const LEFT_LEG: Array[String] = ["mixamorig_LeftUpLeg", "mixamorig_LeftLeg", "mixamorig_LeftFoot"]
const RIGHT_LEG: Array[String] = ["mixamorig_RightUpLeg", "mixamorig_RightLeg", "mixamorig_RightFoot"]
## The elbows bend out and down, the knee forward and a little out.
const ELBOW_POLE: Vector3 = Vector3(1.0, -0.8, 0.2)
const KNEE_POLE: Vector3 = Vector3(0.3, 0.1, -1.0)

## The kid whose space the targets are in (a Node3D with the skeleton under it).
var frame: Node3D
## Where the left and right hand go, and how much (0 to 1).
var hand_left: Vector3 = Vector3.ZERO
var hand_right: Vector3 = Vector3.ZERO
var arm_weight: float = 0.0
## Where the kicking foot goes (the right one), and how much.
var foot: Vector3 = Vector3.ZERO
var foot_weight: float = 0.0

var report: String = ""
var _bones: Dictionary[String, int] = {}


func _process_modification_with_delta(_delta: float) -> void:
	var skeleton: Skeleton3D = get_skeleton()
	if skeleton == null or frame == null or (arm_weight <= 0.0 and foot_weight <= 0.0):
		return
	# kid space -> skeleton space
	var to_skeleton: Transform3D = (frame.global_transform.affine_inverse() * skeleton.global_transform).affine_inverse()
	if arm_weight > 0.0:
		_chain(skeleton, LEFT_ARM, to_skeleton * hand_left, _side_pole(ELBOW_POLE, -1.0, to_skeleton), arm_weight)
		_chain(skeleton, RIGHT_ARM, to_skeleton * hand_right, _side_pole(ELBOW_POLE, 1.0, to_skeleton), arm_weight)
	if foot_weight > 0.0:
		_chain(skeleton, RIGHT_LEG, to_skeleton * foot, to_skeleton.basis * KNEE_POLE, foot_weight)
		var after: Vector3 = skeleton.get_bone_global_pose(_bone(skeleton, RIGHT_LEG[2])).origin
		report = "target %s reached %s w %s" % [to_skeleton * foot, after, foot_weight]


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
