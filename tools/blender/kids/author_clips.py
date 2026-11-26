# Author the shared roster clips that roster_clips.glb was missing (Blender 5.x, bpy).
#
# Every clip is keyed directly on the shared `mixamorig:*` skeleton of tools/blender/kids/clips/roster_clips.glb:
# a small FK model of the armature plus analytic two-bone IK (legs and arms), so
#   - feet are planted exactly (ball of the foot on the clip ground, in-place stance feet move at the travel speed),
#   - knees and elbows only bend about their local +X hinge (no sideways bends), never fully straight,
#   - the root is in place (Hips location keeps only its vertical offset),
#   - loops repeat frame 0 at their last frame (24 f period = keys 0..24 = 0.80 s),
#   - the R2 helper and twist bones are baked with the same formulas retopo_kid.py uses.
# The 20 existing clips are never touched. New actions get a Fake User.
#
# Strafe_L / Strafe_R / Backpedal are phase-locked to Run by KidView (8-way blend uses Run's normalized time and
# rate), so they cover Run's 2.0 m per cycle and plant the left foot at Run's left-stance phase (0.17..0.46) and the
# right foot at Run's right-stance phase (0.67..0.96).
#
# Usage (live session):  exec(open("tools/blender/kids/author_clips.py").read()); author(["Strafe_L"])
# Usage (batch):
#   blender --background --python tools/blender/kids/author_clips.py -- \
#     --src tools/blender/kids/clips/roster_clips.glb --dst tools/blender/kids/clips/roster_clips.glb [--clips A,B]

import bpy
import math
import os
import sys
from mathutils import Vector, Matrix, Quaternion

KIDS_DIR = os.path.dirname(os.path.abspath(__file__))
if KIDS_DIR not in sys.path:
    sys.path.insert(0, KIDS_DIR)
import upgrade_clips as U   # noqa: E402  (joint limits, capsules and swing-twist math shared with anim_scan.py)

LIMITS_JSON = os.path.join(KIDS_DIR, "..", "..", "..", "unity", "Data", "rig__joint_limits.json")

P = "mixamorig:"
FPS = 30
GROUND = -0.008        # clips plant feet 8 mm under the rest pose (walk and Run stance)
MIN_BEND_DEG = 6.0     # knees and elbows never lock straight
RUN_CYCLE_M = 2.0      # Run's stride distance per cycle (unity/Data/anim__clip_metrics.json)
SIDES = ("Left", "Right")


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


def rot(axis, deg):
    """World-axis rotation. Kid faces -Y; +X is the kid's left. +X pitches forward, +Y leans left, +Z turns left."""
    return Matrix.Rotation(math.radians(deg), 3, axis)


def frame(y, x_hint):
    y = y.normalized()
    x = (x_hint - y * x_hint.dot(y)).normalized()
    return Matrix((x, y, x.cross(y))).transposed()


class Rig:
    """Armature-space FK of the clip skeleton, evaluated in Python (independent of the depsgraph)."""

    def __init__(self, arm):
        self.arm = arm
        self.bones = arm.data.bones
        self.rest = {b.name: b.matrix_local.copy() for b in self.bones}
        self.rel = {b.name: (self.rest[b.parent.name].inverted() @ self.rest[b.name]) if b.parent
                    else self.rest[b.name].copy() for b in self.bones}
        self.names = [b.name for b in sorted(self.bones, key=lambda b: len(b.parent_recursive))]
        self.base = {}
        self.clear()

    def clear(self):
        self.q = {n: Quaternion() for n in self.names}
        for n, q in self.base.items():
            self.q[n] = q.copy()
        self.hips_dz = 0.0
        self._m = {}

    def r3(self, n):
        return self.rest[n].to_3x3()

    def M(self, n):
        m = self._m.get(n)
        if m is None:
            b = self.bones[n]
            basis = self.q[n].to_matrix().to_4x4()
            if n == P + "Hips":
                basis = Matrix.Translation(self.hips_loc()) @ basis
            m = (self.M(b.parent.name) if b.parent else Matrix.Identity(4)) @ self.rel[n] @ basis
            self._m[n] = m
        return m

    def hips_loc(self):
        return self.r3(P + "Hips").transposed() @ Vector((0.0, 0.0, self.hips_dz))

    def head(self, n):
        return self.M(n).translation.copy()

    def parent_frame(self, n):
        b = self.bones[n]
        return ((self.M(b.parent.name) @ self.rel[n]) if b.parent else self.rel[n]).to_3x3().normalized()

    def set_world(self, n, R):
        """Pose bone n so its armature-space orientation is R."""
        self.q[n] = (self.parent_frame(n).transposed() @ R).to_quaternion().normalized()
        self._m = {}

    def add_world(self, n, R):
        """Rotate bone n by world rotation R on top of what it inherits."""
        pf = self.parent_frame(n)
        self.set_world(n, R @ pf @ self.q[n].to_matrix())

    def set_local(self, n, q):
        self.q[n] = q.copy()
        self._m = {}

    def limb(self, a, b, c, target, pole):
        """Two-bone IK: bone a and b reach `target` with joint b pointing along `pole`, bending only about b's +X."""
        A = self.head(a)
        ra, rb, rc = (self.bones[x].head_local for x in (a, b, c))
        la, lb = (rb - ra).length, (rc - rb).length
        u = target - A
        dmax = math.sqrt(la * la + lb * lb + 2.0 * la * lb * math.cos(math.radians(MIN_BEND_DEG)))
        d = min(max(u.length, abs(la - lb) + 1e-3), dmax)
        un = u.normalized()
        x = (la * la - lb * lb + d * d) / (2.0 * d)
        h = math.sqrt(max(la * la - x * x, 0.0))
        v = (pole - un * pole.dot(un)).normalized()
        K = A + un * x + v * h
        T = A + un * d
        n = (K - A).cross(T - K).normalized()
        hinge = self.r3(b).col[0]
        for bone, r, dd in ((a, rb - ra, K - A), (b, rc - rb, T - K)):
            self.set_world(bone, frame(dd, n) @ frame(r, hinge).transposed() @ self.r3(bone))
        return (u.length - d)   # > 0: target was out of reach by this much

    def body_turn(self, n):
        """Bone n's current world turn away from its rest orientation, as a quaternion."""
        return (self.M(n).to_3x3().normalized() @ self.r3(n).transposed()).to_quaternion()

    def body_dir(self, n, v):
        """World vector v (given for the rest pose) carried along by bone n's current turn."""
        return (self.M(n).to_3x3().normalized() @ self.r3(n).transposed() @ Vector(v)).normalized()

    def foot(self, side, ball, yaw=0.0, pitch=0.0, toe_flat=True, pole_out=0.15, pole=None, carry=None):
        """Place the ball of the foot (ToeBase head) at `ball`; pitch > 0 lifts the heel about the ball.
        toe_flat (bool or 0..1) keeps the toes flat on the ground; carry (rotation) turns the whole foot with a body
        part, e.g. the pelvis while airborne."""
        F, Tb = P + side + "Foot", P + side + "ToeBase"
        R = rot('Z', yaw) @ rot('X', pitch)
        flat = rot('Z', yaw)
        if carry is not None:
            R, flat = carry @ R, carry @ flat
        ankle = ball + R @ (self.bones[F].head_local - self.bones[Tb].head_local)
        sgn = 1.0 if side == "Left" else -1.0
        if pole is None:
            pole = rot('Z', yaw) @ Vector((sgn * pole_out, -1.0, 0.0))
        miss = self.limb(P + side + "UpLeg", P + side + "Leg", F, ankle, pole)
        self.set_world(F, R @ self.r3(F))
        c = getattr(self, "contract", None)
        for _ in range(4 if c is not None else 0):
            # The ankle allows no twist: keep the limit-clamped foot turn and re-aim the leg so the ball stays put.
            Rc = c.clamped_world(self, F)
            if (Rc @ self.r3(F).transposed()).to_quaternion().rotation_difference(R.to_quaternion()).angle < 1e-3:
                break
            R = Rc @ self.r3(F).transposed()
            ankle = ball + R @ (self.bones[F].head_local - self.bones[Tb].head_local)
            miss = self.limb(P + side + "UpLeg", P + side + "Leg", F, ankle, pole)
            self.set_world(F, R @ self.r3(F))
        w = float(toe_flat)
        self.set_world(Tb, R.to_quaternion().slerp(flat.to_quaternion(), w).to_matrix() @ self.r3(Tb))
        if c is not None:
            self.set_world(Tb, c.clamped_world(self, Tb))   # the toe is a hinge: keep only its bend
        return miss

    def arm_to(self, side, wrist, pole, hand_q=None):
        self.limb(P + side + "Arm", P + side + "ForeArm", P + side + "Hand", wrist, pole)
        if hand_q is not None:
            self.set_local(P + side + "Hand", hand_q)

    def chest_point(self, local):
        """Point in Spine2's frame (x = kid's left, y = up, z = forward), metres from the Spine2 head."""
        return self.M(P + "Spine2") @ Vector(local)

    def chest_dir(self, local):
        return (self.M(P + "Spine2").to_3x3() @ Vector(local)).normalized()


def capture(arm, action_name, f, names):
    act = bpy.data.actions[action_name]
    arm.animation_data.action = act
    arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(f)
    out = {n: arm.pose.bones[n].rotation_quaternion.copy() for n in names}
    arm.animation_data.action = None
    return out


class Contract:
    """The roster contract anim_scan.py checks: joint limits and R2 helper/twist values, both measured from the
    roster reference A-pose (upgrade_clips.build_ref_skeleton_model), and arm/leg capsule clearance."""

    def __init__(self, arm):
        self.skel = U.build_ref_skeleton_model(arm)   # clears the active action and rest-poses the armature
        # build_ref_skeleton_model poses the live armature and reads it back; if the pose is not evaluated (Edit
        # Mode, or NLA blocking the armature) the reference arms stay up and every limit and helper would be wrong.
        d = self.skel["R_ref"][P + "LeftArm"].col[1]
        if abs(math.degrees(math.asin(max(-1.0, min(1.0, -d.z)))) - U.ARM_DROP_DEG) > 1.0:
            raise RuntimeError("reference A-pose not evaluated: leave Edit Mode and check the armature's NLA")
        self.lims, _ = U.load_joint_limits(os.path.normpath(LIMITS_JSON), R_ref=self.skel["R_ref"])
        self.caps = U.measure_reference_capsules(arm)
        self.helpers = {P + s + n for s in SIDES for n in ("ArmTwist", "ForeArmTwist1", "ForeArmTwist2",
                                                          "ForeArmHelper", "LegHelper", "HandHelper")}

    def q_ref(self, rig, bn):
        p = self.skel["parent_of"][bn]
        W = rig.M(bn).to_3x3().normalized()
        if p is None:
            return (self.skel["R_ref"][bn].transposed() @ W).to_quaternion().normalized()
        Wp = rig.M(p).to_3x3().normalized()
        return (self.skel["L_ref"][bn].transposed() @ Wp.transposed() @ W).to_quaternion().normalized()

    def clamped_world(self, rig, bn):
        """World orientation of bone bn once its local turn is clamped into its joint limits."""
        qc = U.clamp_joint_swing_twist(self.q_ref(rig, bn), self.lims[bn[len(P):]], enforce_soft_min=False)[0]
        p = self.skel["parent_of"][bn]
        return rig.M(p).to_3x3().normalized() @ self.skel["L_ref"][bn] @ qc.to_matrix()

    def clamp(self, rig):
        """Pull every limited joint back inside rig__joint_limits.json (parents first)."""
        for bn in self.skel["order"]:
            short = bn[len(P):]
            if not bn.startswith(P) or bn in self.helpers or short not in self.lims:
                continue
            qc, over, _ = U.clamp_joint_swing_twist(self.q_ref(rig, bn), self.lims[short], enforce_soft_min=False)
            if over > 0.05:
                p = self.skel["parent_of"][bn]
                Wp = rig.M(p).to_3x3().normalized() if p else Matrix.Identity(3)
                ref = self.skel["L_ref"][bn] if p else self.skel["R_ref"][bn]
                rig.set_world(bn, Wp @ ref @ qc.to_matrix())

    def bake_helpers(self, rig):
        """R2 helper and twist bones, exactly as anim_scan.py expects them."""
        q = {P + s + n: self.q_ref(rig, P + s + n) for s in SIDES for n in ("Arm", "ForeArm", "Hand", "Leg")}
        one = Quaternion()
        pos = lambda x: x if x.w >= 0.0 else Quaternion((-x.w, -x.x, -x.y, -x.z))
        for s in SIDES:
            ax = {n: Vector(self.lims[s + n]["twistAxis"]).normalized() for n in ("Arm", "ForeArm", "Hand")}
            _, _, arm_tw = U.swing_twist_decompose(q[P + s + "Arm"], ax["Arm"])
            _, _, fa_tw = U.swing_twist_decompose(q[P + s + "ForeArm"], ax["ForeArm"])
            _, _, hand_tw = U.swing_twist_decompose(q[P + s + "Hand"], ax["Hand"])
            tot = fa_tw + hand_tw
            rig.q[P + s + "ArmTwist"] = Quaternion(ax["Arm"], -0.5 * arm_tw)
            rig.q[P + s + "ForeArmTwist1"] = Quaternion(ax["ForeArm"], 0.33 * tot)
            rig.q[P + s + "ForeArmTwist2"] = Quaternion(ax["ForeArm"], 0.66 * tot)
            for h, src in (("ForeArmHelper", "ForeArm"), ("LegHelper", "Leg"), ("HandHelper", "Hand")):
                rig.q[P + s + h] = one.slerp(pos(q[P + s + src]), 0.5)
        rig._m = {}

    def frame_report(self, rig):
        """(limit overshoot per bone, capsule penetration per pair) for the current pose."""
        over = {}
        for bn in self.skel["order"]:
            short = bn[len(P):]
            if bn.startswith(P) and bn not in self.helpers and short in self.lims:
                over[short] = U.clamp_joint_swing_twist(self.q_ref(rig, bn), self.lims[short],
                                                        enforce_soft_min=False)[1]
        W_pos = {bn: rig.head(bn) for bn in self.skel["order"]}
        W_rot = {bn: rig.M(bn).to_3x3().normalized() for bn in self.skel["order"]}
        return over, U.test_capsule_penetrations(self.skel, W_pos, W_rot, self.caps)


# ---- shared body parts ---------------------------------------------------------------------------------------------

def hips_and_spine(rig, dz, yaw=0.0, lean=0.0, pitch=0.0, chest_yaw=0.0, chest_lean=0.0, chest_pitch=0.0):
    """Pelvis turned by yaw/lean/pitch; the spine takes back the pelvis turn and adds the chest's own."""
    rig.hips_dz = dz
    rig._m = {}
    rig.set_world(P + "Hips", rot('Z', yaw) @ rot('Y', lean) @ rot('X', pitch) @ rig.r3(P + "Hips"))
    world_chest = rot('Z', chest_yaw) @ rot('Y', chest_lean) @ rot('X', chest_pitch)
    for n in ("Spine", "Spine1", "Spine2"):   # spread the correction over three bones
        cur = rig.M(P + n).to_3x3().normalized()
        want = world_chest @ rig.r3(P + n)
        diff = (want @ cur.transposed()).to_quaternion()
        share = {"Spine": 1.0 / 3.0, "Spine1": 0.5, "Spine2": 1.0}[n]
        rig.add_world(P + n, Quaternion().slerp(diff, share).to_matrix())


def head_level(rig, yaw=0.0, pitch=0.0, roll=0.0):
    want = rot('Z', yaw) @ rot('Y', roll) @ rot('X', pitch)
    neck_cur = rig.M(P + "Neck").to_3x3().normalized()
    diff = ((want @ rig.r3(P + "Neck")) @ neck_cur.transposed()).to_quaternion()
    rig.add_world(P + "Neck", Quaternion().slerp(diff, 0.5).to_matrix())
    rig.set_world(P + "Head", want @ rig.r3(P + "Head"))


def gait(t, stance_start, stance_len, travel, home, bulge, lift, land_pitch=4.0, off_pitch=28.0, cycle_m=RUN_CYCLE_M):
    """Ball-of-foot path for one foot of an in-place cycle that travels `travel` (unit dir) cycle_m metres a cycle.

    Stance: the planted ball moves against the travel direction at exactly the travel speed (no slide in game).
    Swing: eased return with a sideways bulge (to pass the other leg) and a lift arc."""
    stride = cycle_m * stance_len
    u = (t - stance_start) % 1.0
    if u < stance_len:
        k = u / stance_len
        pos = home + travel * (0.5 * stride - stride * k)
        pitch = land_pitch * (1.0 - smooth(k / 0.3)) + off_pitch * smooth((k - 0.7) / 0.3)
        return pos, pitch, True
    k = (u - stance_len) / (1.0 - stance_len)
    pos = home + travel * (-0.5 * stride + stride * smooth(k)) + bulge * math.sin(math.pi * k)
    pos.z += lift * math.sin(math.pi * k)
    pitch = off_pitch * (1.0 - k) + land_pitch * k + 10.0 * math.sin(math.pi * k)
    return pos, pitch, False


# Run's stance phases (anim__clip_metrics.json Run contactFrames over its 18-frame period).
L_STANCE, R_STANCE, STANCE_LEN = 4.0 / 24.0, 16.0 / 24.0, 7.0 / 24.0


def ball_home(rig, side):
    b = rig.bones[P + side + "ToeBase"].head_local
    return Vector((b.x, b.y, b.z + GROUND))


def pose_strafe(rig, t, s):
    """s = +1 Strafe_L (travel to the kid's left, +X), -1 Strafe_R. Trailing leg crosses in front, lead leg behind.
    They pass 10-20 cm apart front-to-back, as far as the stride allows; anim_scan's thigh capsules still touch
    near the hips (about 3 cm), which a cross-step at Run speed cannot avoid."""
    travel = Vector((s, 0.0, 0.0))
    lead = "Left" if s > 0 else "Right"
    phase = {"Left": L_STANCE, "Right": R_STANCE}
    # Pelvis bob: low at mid-stance, high in the float between steps.
    bob = math.cos(2.0 * math.pi * 2.0 * (t - (L_STANCE + STANCE_LEN * 0.5)))
    dz = -0.11 - 0.020 * bob
    # Crossover: the pelvis turns so the crossing (trailing) hip comes forward; the chest stays square for aiming.
    trail_mid = phase["Right" if lead == "Left" else "Left"] + STANCE_LEN + (1.0 - STANCE_LEN) * 0.5
    cross = math.cos(2.0 * math.pi * (t - trail_mid))
    yaw = s * (5.0 + 11.0 * cross)
    hips_and_spine(rig, dz, yaw=yaw, lean=s * 5.0, pitch=4.0,
                   chest_yaw=0.0, chest_lean=s * 3.0, chest_pitch=7.0 - 1.5 * bob)
    head_level(rig, pitch=-3.0, roll=0.0)
    for side in SIDES:
        trailing = side != lead
        home = ball_home(rig, side) + Vector((-s * 0.025 if trailing else 0.0, -0.10 if trailing else 0.08, 0.0))
        bulge = Vector((0.0, -0.20 if trailing else 0.18, 0.0))
        pos, pitch, _ = gait(t, phase[side], STANCE_LEN, travel, home, bulge, lift=0.17, land_pitch=12.0)
        yaw_f = s * 12.0 + (6.0 if side == "Left" else -6.0)   # feet point a little toward the travel side
        rig.foot(side, pos, yaw=yaw_f, pitch=pitch, pole_out=0.35)   # knees a little out
    # Ready arms, elbows tucked; a small pump against the legs.
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        pump = math.sin(2.0 * math.pi * (t - phase[side]))
        wrist = rig.chest_point((sgn * 0.13 + s * 0.02 * pump, -0.11 + 0.025 * pump, 0.20))
        pole = rig.chest_dir((sgn * 0.6, -0.5, -0.45))
        rig.arm_to(side, wrist, pole, hand_q=Quaternion(Vector((1, 0, 0)), math.radians(-12.0)))

def pose_backpedal(rig, t):
    """Backward jog facing forward: chest hunched forward, toe-first landings behind the hips, high knee lift."""
    travel = Vector((0.0, 1.0, 0.0))
    phase = {"Left": L_STANCE, "Right": R_STANCE}
    bob = math.cos(2.0 * math.pi * 2.0 * (t - (L_STANCE + STANCE_LEN * 0.5)))
    dz = -0.095 - 0.020 * bob
    sway = math.sin(2.0 * math.pi * (t - L_STANCE - 0.5 * STANCE_LEN))   # weight over the planted leg
    hips_and_spine(rig, dz, yaw=4.0 * sway, lean=2.5 * sway, pitch=8.0,
                   chest_yaw=-2.0 * sway, chest_lean=0.0, chest_pitch=13.0 - 1.5 * bob)
    head_level(rig, pitch=-2.0)
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        home = ball_home(rig, side) + Vector((sgn * 0.01, 0.0, 0.0))
        bulge = Vector((sgn * 0.04, 0.0, 0.0))
        pos, pitch, _ = gait(t, phase[side], STANCE_LEN, travel, home, bulge, lift=0.24,
                             land_pitch=22.0, off_pitch=8.0)
        rig.foot(side, pos, yaw=sgn * -5.0, pitch=pitch, pole_out=0.2)
    # Arms pump against the legs: a hand comes forward while its own foot is planted in front.
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        other = phase["Right" if side == "Left" else "Left"]
        pump = math.cos(2.0 * math.pi * (t - other - 0.5 * STANCE_LEN))
        wrist = rig.chest_point((sgn * 0.15, -0.20 + 0.06 * pump, 0.07 + 0.13 * pump))
        pole = rig.chest_dir((sgn * 0.45, -0.35, -0.8))
        rig.arm_to(side, wrist, pole, hand_q=Quaternion(Vector((1, 0, 0)), math.radians(-10.0)))


def chest(rig, yaw=0.0, lean=0.0, pitch=0.0):
    """Upper-body-mask safe: turn only Spine1 and Spine2 (the Spine1+ mask keeps both) so Spine2 ends at the world turn."""
    want = rot('Z', yaw) @ rot('Y', lean) @ rot('X', pitch)
    for n, share in (("Spine1", 0.5), ("Spine2", 1.0)):
        cur = rig.M(P + n).to_3x3().normalized()
        diff = ((want @ rig.r3(P + n)) @ cur.transposed()).to_quaternion()
        rig.add_world(P + n, Quaternion().slerp(diff, share).to_matrix())


def fingers(rig, side, curl, thumb=None, index=None):
    """Curl about each finger's local +X (toward the palm): curl = (knuckle, middle, tip) degrees."""
    for f in ("Middle", "Ring", "Pinky", "Index"):
        c = index if (f == "Index" and index is not None) else curl
        for k, deg in zip("123", c):
            rig.set_local(P + side + "Hand" + f + k, Quaternion(Vector((1, 0, 0)), math.radians(deg)))
    if thumb is not None:
        for k, deg in zip("123", thumb):
            rig.set_local(P + side + "HandThumb" + k, Quaternion(Vector((1, 0, 0)), math.radians(deg)))


def hand_aim(rig, side, fingers_dir, roll=0.0):
    """Point a hand's fingers along fingers_dir with the least twist from its forearm, then roll it about them."""
    fa = rig.M(P + side + "ForeArm").to_3x3().normalized()
    R = frame(fingers_dir, fa.col[0])
    rig.set_world(P + side + "Hand", Matrix.Rotation(math.radians(roll), 3, R.col[1]) @ R)


def stand(rig, dz=-0.02, width=0.03, stagger=0.06):
    """Planted ready stance for upper-body clips (legs only matter when the clip plays full body)."""
    rig.hips_dz = dz
    rig._m = {}
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        ball = ball_home(rig, side) + Vector((sgn * width, -sgn * stagger, 0.0))
        rig.foot(side, ball, yaw=sgn * 8.0, pitch=0.0, pole_out=0.3)


AIM_FORK = Vector((0.0, -0.46, 1.34))     # left wrist: arm forward at shoulder height, on the aim line
AIM_CHEEK = Vector((-0.09, -0.07, 1.37))   # right wrist: band drawn to the right cheek


def aim_pose(rig, breath=0.0, pull=1.0, draw=Vector(), fork_kick=Vector(), fork_tilt=0.0, chest_kick=0.0,
             open_hand=0.0, reach=None, reach_w=0.0, chest_turn=0.0, chest_bend=0.0, look_down=0.0):
    """Shared Tirador aim: chest turned right so the left shoulder leads, left fist on the fork, right hand at the cheek.
    pull 0..1 moves the right hand from just behind the fork (band slack) to the cheek anchor; draw / fork_kick offset
    the two wrists, fork_tilt pitches the fork fist up (degrees), chest_kick leans the chest back, open_hand 0..1 lets
    go of the band. reach / reach_w blend the right wrist toward another point (hand turning fingers-down),
    chest_turn / chest_bend / look_down add to the chest yaw, chest pitch and head pitch."""
    stand(rig)
    chest(rig, yaw=-24.0 + chest_turn, lean=-2.0, pitch=4.0 + 0.8 * breath - chest_kick + chest_bend)
    head_level(rig, yaw=0.0, pitch=-2.0 + look_down, roll=-5.0 * (1.0 - min(1.0, look_down / 15.0)))
    fork = AIM_FORK + Vector((0.0, 0.0, 0.004 * breath)) + fork_kick
    rig.arm_to("Left", fork, Vector((0.35, 0.25, -1.0)))
    hand_aim(rig, "Left", (rot('X', -fork_tilt) @ Vector((0.05, -1.0, 0.08))))
    fingers(rig, "Left", (78.0, 92.0, 60.0), thumb=(30.0, 40.0, 25.0))
    slack = AIM_FORK + Vector((-0.04, 0.16, 0.03))
    cheek = AIM_CHEEK + Vector((0.0, 0.003 * breath, 0.003 * breath))
    wrist = slack.lerp(cheek, pull) + draw
    pole = Vector((-1.0, 0.6, -0.3))
    if reach is not None:
        wrist = wrist.lerp(reach, reach_w)
        pole = pole.lerp(Vector((-0.6, 1.0, 0.2)), reach_w)
    rig.arm_to("Right", wrist, pole)
    to_fork = (AIM_FORK - rig.head(P + "RightHand")).normalized()
    hand_aim(rig, "Right", to_fork.lerp(Vector((0.1, -0.35, -1.0)).normalized(), reach_w).normalized())
    k = 1.0 - open_hand
    fingers(rig, "Right", (80.0 * k + 10.0 * open_hand, 90.0 * k + 12.0 * open_hand, 55.0 * k + 8.0 * open_hand),
            thumb=(25.0 * k + 8.0 * open_hand, 30.0 * k + 5.0 * open_hand, 20.0 * k),
            index=(45.0 * k + 8.0 * open_hand, 50.0 * k + 8.0 * open_hand, 30.0 * k + 5.0 * open_hand))


def keys(f, table):
    """Piecewise-smooth value at frame f from [(frame, value), ...] (values: float or Vector)."""
    if f <= table[0][0]:
        return table[0][1]
    for (f0, v0), (f1, v1) in zip(table, table[1:]):
        if f <= f1:
            w = smooth((f - f0) / float(f1 - f0))
            return v0 * (1.0 - w) + v1 * w if not isinstance(v0, Vector) else v0.lerp(v1, w)
    return table[-1][1]


def pose_aim_hold(rig, t):
    aim_pose(rig, breath=math.sin(2.0 * math.pi * t))


def pose_fire(rig, t):
    """Fire (0.40 s, release at f3): f0-2 micro-pull, f3 band snap + recoil, f5-8 the draw hand comes forward and seats
    a fresh stone in the band's pouch (fork fist dips to meet it), f9-12 draws back to the aim pose."""
    f = t * 12.0
    zero = Vector()
    aim_pose(rig,
             pull=keys(f, [(0, 1.0), (4, 1.0), (7, 0.62), (8, 0.64), (12, 1.0)]),
             draw=keys(f, [(0, zero), (2, Vector((0.0, 0.015, 0.0))), (3, Vector((-0.035, 0.05, 0.01))),
                           (4, Vector((-0.04, 0.055, 0.005))), (7, Vector((0.0, 0.0, -0.02))),
                           (8, Vector((0.0, 0.0, -0.025))), (12, zero)]),
             fork_kick=keys(f, [(0, zero), (2, Vector((0.0, -0.005, 0.0))), (3, Vector((0.0, 0.03, 0.035))),
                                (5, Vector((0.0, 0.02, 0.015))), (8, Vector((0.0, 0.01, -0.02))), (12, zero)]),
             fork_tilt=keys(f, [(0, 0.0), (2, -2.0), (3, 14.0), (5, 8.0), (8, -4.0), (12, 0.0)]),
             chest_kick=keys(f, [(0, 0.0), (2, -0.6), (3, 2.5), (5, 1.5), (8, -0.5), (12, 0.0)]),
             open_hand=keys(f, [(0, 0.0), (2, 0.0), (3, 0.28), (4, 0.56), (5, 0.7), (7, 0.25), (8, 0.0), (12, 0.0)]))

POUCH = Vector((-0.2, -0.05, 0.97))   # right wrist over the stone pouch on the right hip


def pose_reload(rig, t):
    """Reload (1.50 s): f0-8 fork drops to low ready while the right hand heads for the hip pouch, f14 grabs a stone,
    f20 lifts it, f30 seats it in the band's pouch behind the fork, f31-33 tug test, f34-44 snap back to aim (ready f44)."""
    f = t * 45.0
    zero = Vector()
    low = Vector((0.03, 0.1, -0.16))
    aim_pose(rig,
             pull=keys(f, [(0, 1.0), (9, 0.3), (20, 0.0), (30, 0.0), (31, 0.18), (33, 0.0), (34, 0.0), (44, 1.0)]),
             reach=POUCH,
             reach_w=keys(f, [(0, 0.0), (10, 0.88), (14, 1.0), (17, 1.0), (29, 0.0)]),
             fork_kick=keys(f, [(0, zero), (8, low), (26, low), (30, low * 0.5), (34, low * 0.4), (44, zero)]),
             fork_tilt=keys(f, [(0, 0.0), (8, -12.0), (26, -12.0), (30, -4.0), (44, 0.0)]),
             chest_turn=keys(f, [(0, 0.0), (8, 12.0), (20, 10.0), (30, 4.0), (44, 0.0)]),
             chest_bend=keys(f, [(0, 0.0), (8, 9.0), (16, 11.0), (24, 4.0), (44, 0.0)]),
             look_down=keys(f, [(0, 0.0), (8, 16.0), (16, 18.0), (24, 10.0), (30, 8.0), (40, 0.0)]),
             open_hand=keys(f, [(0, 0.0), (6, 0.2), (11, 0.9), (12, 0.9), (18, 0.0), (44, 0.0)]),
             draw=keys(f, [(0, zero), (4, Vector((-0.1, -0.1, 0.0))), (10, zero), (20, zero),
                           (26, Vector((0.02, -0.02, 0.03))), (30, zero)]))   # swing out around the shoulder


def curve(f, table):
    """Catmull-Rom through [(frame, value), ...] (values: float or Vector): passes every key without stopping."""
    if f <= table[0][0]:
        return table[0][1]
    if f >= table[-1][0]:
        return table[-1][1]
    i = max(k for k in range(len(table) - 1) if table[k][0] <= f)
    (f1, p1), (f2, p2) = table[i], table[i + 1]
    p0 = table[i - 1][1] if i > 0 else p1
    p3 = table[i + 2][1] if i + 2 < len(table) else p2
    d0 = (f2 - table[i - 1][0]) if i > 0 else (f2 - f1)
    d1 = (table[i + 2][0] - f1) if i + 2 < len(table) else (f2 - f1)
    h = f2 - f1
    u = (f - f1) / h
    m1 = (p2 - p0) * (h / d0)
    m2 = (p3 - p1) * (h / d1)
    u2, u3 = u * u, u * u * u
    return p1 * (2 * u3 - 3 * u2 + 1) + m1 * (u3 - 2 * u2 + u) + p2 * (-2 * u3 + 3 * u2) + m2 * (u3 - u2)


def weight(f, table):
    return min(1.0, max(0.0, curve(f, table)))


G = 0.017   # ball-of-foot height on the clip ground (rest ball height + GROUND)
LEDGE_Z = 0.95


def V(x, y, z):
    return Vector((x, y, z))


def pose_vault(rig, t):
    """Speed vault, keys 0-18 (0.60 s; the sim vault is 0.4 s, so play it at 1.5x), in place: the game carries the kid
    over waist-high cover. Beats below are in 12-frame design time (key = f * 1.5).
    f0-2 crouch and spring off the left foot; f3-6 the left palm is planted on the ledge (sliding back with the cover)
    and the whole pelvis rolls about 88 degrees onto it, so the legs swing over extended and hip-width apart (knees
    about 40 degrees, feet pointed) up the right side above ledge height; f7-9 the hand lets go and the body rolls upright, right foot reaching down; f10 right foot
    lands; f11-12 both feet absorb."""
    f = t * 12.0
    roll = curve(f, [(0, 0.0), (2, 10.0), (4, 55.0), (5.5, 88.0), (7, 78.0), (8.5, 35.0), (10, 4.0), (12, 0.0)])
    chest_pitch = curve(f, [(0, 18.0), (2, 28.0), (4, 34.0), (6, 30.0), (8, 18.0), (10, 10.0), (11, 15.0), (12, 12.0)])
    hips_and_spine(rig, curve(f, [(0, -0.10), (2, -0.02), (4, 0.30), (5.8, 0.34), (7, 0.14), (8.5, -0.02), (10, -0.05), (11.3, -0.10),
                                 (12, -0.09)]),
                   yaw=curve(f, [(0, 0.0), (5, -10.0), (8, -6.0), (11, 0.0)]),
                   lean=roll,
                   pitch=curve(f, [(0, 14.0), (2, 20.0), (4, 26.0), (6, 22.0), (8, 12.0), (10, 8.0), (11, 12.0),
                                  (12, 8.0)]),
                   chest_lean=roll * 0.55, chest_pitch=chest_pitch)
    head_level(rig, pitch=0.5 * chest_pitch, roll=0.3 * roll)
    # Feet: ground keys slide back 0.1 m a frame (3 m/s); in the air they reach out near full leg length in the pelvis
    # frame (x left, y up, z forward from the Hips head) with pointed feet, so the roll swings the extended legs over.
    hips = rig.M(P + "Hips")
    air_l = weight(f, [(0, 0.0), (1, 0.0), (5, 1.0), (7, 1.0), (10.5, 0.0)])
    air_r = weight(f, [(0, 0.0), (0, 0.0), (4, 1.0), (6.3, 1.0), (10, 0.0)])
    tuck_l = hips @ curve(f, [(0, V(0.10, -0.55, 0.05)), (3, V(0.100, -0.740, 0.020)), (8, V(0.100, -0.740, 0.020)), (12, V(0.10, -0.55, 0.05))])
    tuck_r = hips @ curve(f, [(0, V(-0.12, -0.55, 0.0)), (3, V(-0.100, -0.740, 0.140)), (8, V(-0.100, -0.740, 0.140)), (12, V(-0.12, -0.55, 0.0))])
    ground_l = curve(f, [(0, V(0.12, -0.10, G)), (2, V(0.12, 0.10, G)), (10, V(0.08, -0.12, G)), (11, V(0.10, -0.02, G)),
                        (12, V(0.10, 0.08, G))])
    ground_r = curve(f, [(0, V(-0.12, 0.15, G)), (1, V(-0.12, 0.25, G)), (9, V(-0.12, -0.25, G)),
                        (10, V(-0.12, -0.15, G)), (11, V(-0.12, -0.05, G)), (12, V(-0.12, 0.05, G))])
    for side, ground, tuck, air, pitch in (
            ("Left", ground_l, tuck_l, air_l, curve(f, [(0, 0.0), (1, 8.0), (2, 30.0), (4, 55.0), (8, 55.0), (9.5, 25.0), (11, 0.0)])),
            ("Right", ground_r, tuck_r, air_r, curve(f, [(0, 20.0), (1, 28.0), (3.5, 55.0), (7.5, 55.0), (9, 25.0), (10, 0.0)]))):
        sgn = 1.0 if side == "Left" else -1.0
        pole = rig.body_dir(P + "Hips", (sgn * 0.25, -1.0, 0.3 * air))
        carry = Quaternion().slerp(rig.body_turn(P + "Hips"), air).to_matrix()   # airborne feet ride the pelvis
        rig.foot(side, ground.lerp(tuck, air), yaw=-12.0 * air + sgn * 4.0, pitch=pitch, toe_flat=1.0 - air,
                 pole=pole, carry=carry)
    # Arms ride with the chest (Spine2 frame: x left, y up, z forward); the left wrist blends onto the ledge for the
    # f3-6 plant (sliding back with the cover), the right arm swings up and out for balance.
    plant = weight(f, [(0, 0.0), (1, 0.0), (3, 1.0), (6, 1.0), (9, 0.0)])
    ledge = curve(f, [(0, V(0.34, -0.34, LEDGE_Z + 0.03)), (3, V(0.34, -0.34, LEDGE_Z + 0.03)),
                     (6, V(0.38, -0.20, LEDGE_Z + 0.03)), (12, V(0.38, -0.20, LEDGE_Z + 0.03))])
    lw = rig.chest_point(curve(f, [(0, V(0.17, -0.22, 0.10)), (2, V(0.14, -0.12, 0.33)), (7, V(0.20, -0.12, 0.30)),
                                  (9, V(0.26, -0.10, 0.18)), (12, V(0.17, -0.22, 0.10))]))
    rig.arm_to("Left", lw.lerp(ledge, plant), rig.body_dir(P + "Spine2", (0.5, 1.0, -0.5)))
    hand_aim(rig, "Left", V(0.3, -1.0, -0.35))
    rig.set_local(P + "LeftHand", Quaternion().slerp(rig.q[P + "LeftHand"], 0.6 * plant))   # palm part-flat
    fingers(rig, "Left", tuple(35.0 * (1.0 - plant) + 4.0 * plant for _ in range(2)) + (3.0 * plant,),
            thumb=(35.0 * (1.0 - plant) + 10.0 * plant, 29.0 * (1.0 - plant) + 5.0 * plant, 0.0))
    rw = rig.chest_point(curve(f, [(0, V(-0.17, -0.22, 0.10)), (3, V(-0.28, 0.02, 0.12)), (6, V(-0.32, 0.10, 0.04)),
                                  (9, V(-0.26, -0.06, 0.12)), (12, V(-0.17, -0.22, 0.10))]))
    rig.arm_to("Right", rw, rig.body_dir(P + "Spine2", (-0.5, 1.0, -0.5)))

def pose_cast_place(rig, t):
    """Cast_Place (0.80 s) for ground traps (papel_trap, jacks): f0-6 squat and gather down, f7-9 the right hand
    reaches the street in front of the feet (release f9 = 0.30 s), f9-11 hold, f12-24 spring up to a ready stance. Both feet stay planted; the body folds through the spine (each spine joint allows 25 degrees) over a
    deep squat, and the right arm hangs from the shoulder so the fingertips come down in front of the feet. Within
    the rig's joint limits they stop about 15 cm above the street. The left hand keeps the Tirador low for balance."""
    f = t * 24.0
    dz = curve(f, [(0, -0.03), (3, -0.14), (6.5, -0.27), (9, -0.36), (11.5, -0.37), (17, -0.2), (22, -0.06), (24, -0.04)])
    pitch = curve(f, [(0, 4.0), (6.5, 12.0), (9, 14.0), (11.5, 16.0), (21.5, 8.0), (24, 5.0)])
    chest_p = curve(f, [(0, 6.0), (3, 30.0), (6.5, 68.0), (9, 84.0), (11.5, 86.0), (16, 50.0), (20, 18.0), (24, 7.0)])
    hips_and_spine(rig, dz, pitch=pitch, chest_yaw=curve(f, [(0, 0.0), (5, -8.0), (9, -10.0), (14, -4.0), (24, 0.0)]),
                   chest_pitch=chest_p)
    head_level(rig, pitch=curve(f, [(0, 0.0), (6, 22.0), (10, 30.0), (14, 14.0), (24, 0.0)]))
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        ball = ball_home(rig, side) + Vector((sgn * 0.02, 0.0, 0.0))
        rig.foot(side, ball, yaw=sgn * 8.0, pitch=0.0, pole_out=0.15)   # knees track over the turned-out toes
    # Right hand: a small wind-up at the chest, then the arm hangs from the shoulder as the body folds, so the
    # fingertips come down onto the street in front of the feet (release f9, held to f11), then it swings back up.
    rest_r = rig.chest_point((-0.18, -0.24, 0.08))
    wind = rig.chest_point((-0.20, -0.20, 0.16))
    shoulder = rig.head(P + "RightArm")
    hang = shoulder + V(0.02, -0.04, -0.4)
    w_up = 0.0
    w_dn = weight(f, [(0, 0.0), (2, 0.18), (5, 0.55), (8, 0.92), (9, 1.0), (11.5, 1.0), (15.5, 0.6), (19.5, 0.18), (22.5, 0.0)])
    wrist = rest_r.lerp(wind, w_up).lerp(hang, w_dn)
    rig.arm_to("Right", wrist, rig.body_dir(P + "Spine2", (-0.5, 0.8, 0.0)))
    point = V(0.05, -0.6, -1.0)   # fingertips down onto the street
    hand_aim(rig, "Right", rig.M(P + "RightForeArm").to_3x3().col[1].lerp(point, w_dn).normalized())
    fingers(rig, "Right", tuple(c * (1.0 - w_dn) + o * w_dn for c, o in zip((35.0, 35.0, 0.0), (8.0, 6.0, 3.0))))
    # Left hand keeps the Tirador low and a little out for balance.
    rig.arm_to("Left", rig.chest_point(keys(f, [(0, V(0.18, -0.24, 0.08)), (9, V(0.24, -0.16, 0.12)),
                                                (16, V(0.19, -0.22, 0.10)), (24, V(0.18, -0.24, 0.08))])),
               rig.body_dir(P + "Spine2", (0.6, 1.0, -0.3)))

def pose_cast_slam(rig, t):
    """Cast_Slam (1.20 s) for heavy AoE / ultimates (ult_bagsak, delay 0.45 s): f0-2 dip and big leap (takeoff f2),
    knees tucked high and the back arched with both fists overhead at the top (f6-7); f7-13 the legs reach down
    while the fists slam down and the torso whips forward, landing f13; f14 impact with both fists down in front of
    the feet, sinking into a deep knee crouch (to f18); f19-36 bounce back to a ready stance. Feet planted from f13."""
    f = t * 36.0
    dz = curve(f, [(0, -0.03), (1.5, -0.07), (3, 0.06), (6, 0.28), (8, 0.24), (10.5, 0.12), (13.0, -0.06),
                   (14.0, -0.10), (15, -0.15), (16.5, -0.21), (18, -0.25), (19.5, -0.26), (25, -0.12),
                   (31, -0.02), (36, -0.04)])
    air = weight(f, [(0, 0.0), (1.5, 0.0), (3.5, 1.0), (11.5, 1.0), (13.0, 0.0)])
    leg = curve(f, [(0, 0.62), (2, 0.60), (3, 0.57), (4, 0.52), (5, 0.46), (6, 0.40), (7.5, 0.34), (9, 0.40), (11, 0.52), (13.0, 0.6), (36, 0.6)])
    chest_p = curve(f, [(0, 8.0), (1.5, 18.0), (4, -6.0), (6.5, -20.0), (8.5, -12.0), (11, 22.0), (13, 50.0), (14, 62.0),
                        (18, 66.0), (25, 26.0), (31, 8.0), (36, 6.0)])
    hips_and_spine(rig, dz, pitch=curve(f, [(0, 6.0), (1.5, 10.0), (4, 0.0), (7, -4.0), (11, 6.0), (14, 10.0),
                                            (18, 12.0), (26, 8.0), (36, 4.0)]), chest_pitch=chest_p)
    head_level(rig, pitch=curve(f, [(0, 0.0), (4, -24.0), (7, -32.0), (10, -6.0), (13, 22.0), (14, 30.0), (18, 30.0),
                                    (28, 4.0), (36, 0.0)]))
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        ground = ball_home(rig, side) + V(sgn * 0.05, -sgn * 0.02, 0.0)
        tuck = ground + V(sgn * 0.03, 0.0, max(0.0, rig.hips_dz + 0.70 - leg))   # knees tucked, legs reach for the ground
        rig.foot(side, ground.lerp(tuck, air), yaw=sgn * 10.0, pitch=18.0 * air, toe_flat=1.0 - air, pole_out=0.3)
    # Both fists arc forward round the body: low at the sides, up overhead, out in front, slam down in front of the
    # feet, recover (points in the chest frame: x left, y up, z forward).
    path = curve(f, [(0, V(0.18, -0.24, 0.08)), (2.5, V(0.16, -0.02, 0.26)), (6.5, V(0.10, 0.32, 0.26)), (8, V(0.10, 0.32, 0.26)),
                     (10.5, V(0.11, 0.16, 0.38)), (14, V(0.09, -0.12, 0.28)), (18, V(0.09, -0.12, 0.28)),
                     (25, V(0.15, -0.10, 0.24)), (31, V(0.18, -0.24, 0.08)), (36, V(0.18, -0.24, 0.08))])
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        wrist = rig.chest_point((sgn * path.x, path.y, path.z))
        rig.arm_to(side, wrist, rig.body_dir(P + "Spine2", (sgn * 0.8, 0.5, -0.3)))
        fingers(rig, side, (70.0, 85.0, 55.0), thumb=(30.0, 35.0, 20.0))   # fists

def pose_cast_buff(rig, t):
    """Cast_Buff (0.90 s, upper body) for self/ally buffs (heal, papel_shield, Merienda): f0-4 fists draw in to the
    chest, elbows forward, f5-10 burst into a confident cheer, both arms straight and extended upward in a V (chest
    open, head up; peak f10 = 0.33 s, held to f12), f13-27 lower the straight arms out to the sides and settle back to
    the ready pose. Only Spine1 and above move, so it plays over running legs."""
    f = t * 27.0
    stand(rig)
    chest(rig, yaw=0.0, lean=0.0, pitch=curve(f, [(0, 4.0), (4.0, 12.0), (10, -10.0), (12, -10.0), (19, 0.0), (27, 4.0)]))
    head_level(rig, pitch=curve(f, [(0, 0.0), (4.0, 8.0), (10, -14.0), (12, -14.0), (20, 0.0), (27, 0.0)]))
    # Arm aim from the shoulder (chest frame: x out, y up, z forward) and elbow bend, both animated.
    aim = curve(f, [(0, V(0.30, -0.95, 0.12)), (4.0, V(0.3, 0.05, 0.95)), (7, V(0.457, 0.603, 0.654)),
                    (10, V(0.42, 0.90, 0.08)), (12, V(0.42, 0.90, 0.08)), (17, V(0.88, -0.30, 0.30)),
                    (22, V(0.55, -0.82, 0.12)), (27, V(0.30, -0.95, 0.12))])
    bend = curve(f, [(0, 25.0), (4.0, 100.0), (7, 45.0), (10, 14.0), (12, 14.0), (17, 22.0), (22, 20.0), (27, 25.0)])
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        a = rig.chest_dir((sgn * aim.x, aim.y, aim.z))
        la = (rig.bones[P + side + "ForeArm"].head_local - rig.bones[P + side + "Arm"].head_local).length
        lb = (rig.bones[P + side + "Hand"].head_local - rig.bones[P + side + "ForeArm"].head_local).length
        reach = math.sqrt(la * la + lb * lb + 2.0 * la * lb * math.cos(math.radians(bend)))
        rig.arm_to(side, rig.head(P + side + "Arm") + a * reach, rig.body_dir(P + "Spine2", (sgn * 0.3, 0.3, -1.0)))
        fingers(rig, side, (70.0, 85.0, 55.0), thumb=(30.0, 35.0, 20.0))   # fists

def pose_cast_channel(rig, t):
    """Cast_Channel (1.00 s loop, upper body) for sustained casts and rescues: braced and leaning forward, both hands
    extended in front of the chest, cupped and close together; two energy pulses a loop push the hands out a few cm
    and tense the chest, shoulders and fingers. Loop: key 30 = key 0. The legs hold a wide braced stance."""
    pulse = 0.5 - 0.5 * math.cos(2.0 * math.pi * 2.0 * t)       # 0..1, two pulses a loop
    stand(rig, dz=-0.05, width=0.07, stagger=0.10)
    chest(rig, yaw=0.0, lean=0.0, pitch=10.0 + 3.0 * pulse)
    head_level(rig, pitch=4.0 + 2.0 * pulse)
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        wrist = rig.chest_point((sgn * (0.075 - 0.01 * pulse), -0.03 + 0.01 * pulse, 0.32 + 0.035 * pulse))
        rig.arm_to(side, wrist, rig.body_dir(P + "Spine2", (sgn * 0.7, -0.2, -0.7)))
        hand_aim(rig, side, rig.chest_dir((-sgn * 0.25, 0.25, 1.0)), roll=sgn * 60.0)   # palms turned to each other
        c = 18.0 + 10.0 * pulse
        fingers(rig, side, (c, c + 8.0, c * 0.7), thumb=(12.0, 10.0, 6.0))   # cupped, flexing with the pulse


def pose_sit(rig, t):
    """Sit (4.00 s loop) for tagged kids waiting to be rescued: sitting on the street with the knees up and the feet
    planted in front, forearms resting on the knees, a little slumped; two slow breaths and a look round per loop, and
    once a loop (f42-104) the right hand goes up and scratches the side of the head. Loop: key 120 = key 0."""
    f = t * 120.0
    w = 2.0 * math.pi * t
    breath = math.sin(2.0 * w)
    scratch = weight(f, [(0, 0.0), (42, 0.0), (62, 1.0), (86, 1.0), (104, 0.0), (120, 0.0)])
    look = math.sin(w + 0.6) * (1.0 - scratch)
    rub = math.sin(2.0 * math.pi * (f - 62) / 8.0) * min(1.0, scratch * 1.5)
    hips_and_spine(rig, -0.66, pitch=-25.0, chest_yaw=4.0 * look - 3.0 * scratch, chest_pitch=18.0 + 1.5 * breath)
    head_level(rig, yaw=22.0 * look - 6.0 * scratch, pitch=6.0 - 2.0 * breath + 4.0 * scratch,
               roll=-3.0 * look + 6.0 * scratch + 1.5 * rub)
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        ball = ball_home(rig, side) + V(sgn * 0.06, -0.4, 0.0)
        rig.foot(side, ball, yaw=sgn * 10.0, pitch=0.0, pole=V(sgn * 0.05, -0.2, 1.0))   # knees up
    chest3 = rig.M(P + "Spine2").to_3x3().normalized()
    for side in SIDES:
        sgn = 1.0 if side == "Left" else -1.0
        knee = rig.head(P + side + "Leg")
        rest = knee + V(-sgn * 0.02, -0.06, 0.04) + V(0.0, 0.0, 0.006 * breath)   # forearms resting on the knees
        pole = rig.body_dir(P + "Spine2", (sgn * 0.6, 0.6, -0.4))
        if side == "Right":
            # Scratch: the wrist goes up beside the right side of the head and rubs back and forth (chest frame:
            # x left, y up, z forward, metres from the Head bone's root).
            spot = rig.head(P + "Head") + chest3 @ V(-0.13, 0.09, 0.02 + 0.025 * rub)
            rest = rest.lerp(spot, scratch)
            pole = pole.lerp(rig.body_dir(P + "Spine2", (-1.0, 0.1, -0.3)), scratch)
        rig.arm_to(side, rest, pole)
        c = scratch * 25.0 if side == "Right" else 0.0
        fingers(rig, side, (30.0 + c + (8.0 * rub if side == "Right" else 0.0), 35.0 + c, 15.0 + c),
                thumb=(20.0, 15.0, 5.0))

CLIPS = {
    # name: (last key, loop, pose function(rig, t))
    "Strafe_L": (24, True, lambda rig, t: pose_strafe(rig, t, +1.0)),
    "Strafe_R": (24, True, lambda rig, t: pose_strafe(rig, t, -1.0)),
    "Backpedal": (24, True, pose_backpedal),
    "Aim_Hold": (30, True, pose_aim_hold),
    "Fire": (12, False, pose_fire),
    "Reload": (45, False, pose_reload),
    "Vault": (18, False, pose_vault),
    "Cast_Place": (24, False, pose_cast_place),
    "Cast_Slam": (36, False, pose_cast_slam),
    "Cast_Buff": (27, False, pose_cast_buff),
    "Cast_Channel": (30, True, pose_cast_channel),
    "Sit": (120, True, pose_sit),
}

BASE_FROM = ("Run", 0)   # fingers, cape and hair rest values (bones the clips never solve)


def base_names(rig):
    return [n for n in rig.names if any(k in n for k in ("Hand" + "Index", "HandMiddle", "HandRing", "HandPinky",
                                                         "HandThumb", "Cape", "Hair", "headfront"))]


def key_clip(rig, name):
    last, loop, fn = CLIPS[name]
    arm = rig.arm
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data.action = act
    prev = {}
    misses = []
    first = None
    for f in range(last + 1):
        if loop and f == last:
            qs = first
        else:
            rig.clear()
            fn(rig, f / float(last))
            rig.contract.clamp(rig)
            rig.contract.bake_helpers(rig)
            qs = {n: rig.q[n].normalized() for n in rig.names}
            qs["__dz"] = rig.hips_dz
        for n in rig.names:
            q = qs[n]
            if n in prev and prev[n].dot(q) < 0.0:
                q = Quaternion((-q.w, -q.x, -q.y, -q.z))
            prev[n] = q
            pbn = arm.pose.bones[n]
            pbn.rotation_mode = "QUATERNION"
            pbn.rotation_quaternion = q
            pbn.keyframe_insert("rotation_quaternion", frame=f, group=n)
        hips = arm.pose.bones[P + "Hips"]
        hips.location = rig.r3(P + "Hips").transposed() @ Vector((0.0, 0.0, qs["__dz"]))
        hips.keyframe_insert("location", frame=f, group=P + "Hips")
        if first is None:
            first = qs
    for layer in act.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = "LINEAR"
    arm.animation_data.action = act
    return act


def author(names, arm=None):
    arm = arm or next(o for o in bpy.data.objects if o.type == "ARMATURE")
    if arm.animation_data is None:
        arm.animation_data_create()
    scn = bpy.context.scene
    scn.render.fps, scn.render.fps_base = FPS, 1.0
    rig = Rig(arm)
    rig.contract = Contract(arm)
    rig.base = capture(arm, BASE_FROM[0], BASE_FROM[1], base_names(rig))
    made = [key_clip(rig, n) for n in names]
    act = made[-1]
    arm.animation_data.action = act
    arm.animation_data.action_slot = act.slots[0]
    scn.frame_start, scn.frame_end = 0, int(act.frame_range[1])
    scn.frame_set(0)
    return made


def main(argv):
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--dst", required=True)
    ap.add_argument("--clips", default=",".join(CLIPS))
    a = ap.parse_args(argv)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=a.src)
    author([c for c in a.clips.split(",") if c])
    export(a.dst)


def scan(names, arm=None):
    """anim_scan.py's gates on freshly generated frames: pops (> 25 deg, loop wrap included), joint limits
    (> 1.25 deg; a straight-locked elbow or knee reads 99, a T-pose frame 98), capsule penetration (> 3 mm).
    Returns {clip: (max pop, worst limit, worst penetration mm)}."""
    arm = arm or next(o for o in bpy.data.objects if o.type == "ARMATURE")
    rig = Rig(arm)
    rig.contract = Contract(arm)
    rig.base = capture(arm, BASE_FROM[0], BASE_FROM[1], base_names(rig))
    c = rig.contract
    out = {}
    for name in names:
        last, loop, fn = CLIPS[name]
        qs, pops, lim, pen = [], (0.0, -1, ""), (0.0, -1, ""), (0.0, -1, "")
        for f in range(last + 1):
            rig.clear()
            fn(rig, f / float(last))
            c.clamp(rig)
            c.bake_helpers(rig)
            bends = {s: U.measure_joint_angles_deg(c.q_ref(rig, P + s), c.lims[s])[0]
                     for s in ("LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg")}
            for s, flex in bends.items():   # anim_scan: no straight-locked elbow or knee (< 2 deg)
                if flex < 2.0:
                    lim = max(lim, (99.0, f, s + " locked"))
            up = [(rig.head(P + s + "ForeArm") - rig.head(P + s + "Arm")).normalized().z for s in SIDES]
            if all(abs(z) < 0.25 for z in up) and max(bends["LeftForeArm"], bends["RightForeArm"]) < 12.0:
                lim = max(lim, (98.0, f, "T-pose"))   # anim_scan: no T-pose frame
            q = {bn: c.q_ref(rig, bn) for bn in c.skel["order"] if bn.startswith(P) and bn not in c.helpers}
            over, pn = c.frame_report(rig)
            b, v = max(over.items(), key=lambda kv: kv[1])
            if v > lim[0]:
                lim = (round(v, 2), f, b)
            b, v = max(pn.items(), key=lambda kv: kv[1])
            if v * 1000.0 > pen[0]:
                pen = (round(v * 1000.0, 1), f, b)
            qs.append(q)
        pairs = list(zip(qs, qs[1:])) + ([(qs[-2], qs[0])] if loop else [])
        for i, (qa, qb) in enumerate(pairs):
            for bn in qa:
                d = math.degrees(qa[bn].rotation_difference(qb[bn]).angle)
                if d > pops[0]:
                    pops = (round(d, 1), i + 1, bn[len(P):])
        out[name] = (pops, lim, pen)
    return out


def export(path):
    """Same exporter settings as upgrade_clips.py, so the clip pack keeps its format."""
    for o in bpy.data.objects:
        o.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format="GLB", use_selection=True, export_yup=True,
        export_animations=True, export_animation_mode="ACTIONS", export_force_sampling=False,
        export_bake_animation=False, export_skins=True, export_all_influences=False, export_image_format="AUTO",
        export_morph=False, export_apply=False, export_texcoords=True, export_normals=True,
        export_optimize_animation_size=True, export_optimize_animation_keep_anim_armature=False)

if __name__ == "__main__" and "--" in sys.argv:
    main(sys.argv[sys.argv.index("--") + 1:])
