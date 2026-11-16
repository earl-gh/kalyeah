# Drive the contract helper and twist bones in Blender (tests and bakes); the game applies the same rule.
#   <Side>ForeArmHelper / LegHelper / HandHelper: 50% of their joint's rotation
#   <Side>ForeArmTwist1 / ForeArmTwist2: 33% / 66% of the hand's twist (about the bone axis)
P = "mixamorig:"


def add_drivers(arm):
    pb = arm.pose.bones
    for s in ("Left", "Right"):
        for helper, src in (("ForeArmHelper", "ForeArm"), ("LegHelper", "Leg"), ("HandHelper", "Hand")):
            h = pb.get(P + s + helper)
            if h is None or P + s + src not in pb:
                continue
            c = h.constraints.new('COPY_ROTATION')
            c.target = arm; c.subtarget = P + s + src
            c.owner_space = 'LOCAL'; c.target_space = 'LOCAL'; c.mix_mode = 'REPLACE'; c.influence = 0.5
        for twist, share in (("ForeArmTwist1", 0.33), ("ForeArmTwist2", 0.66)):
            t = pb.get(P + s + twist)
            if t is None or P + s + "Hand" not in pb:
                continue
            c = t.constraints.new('COPY_ROTATION')
            c.target = arm; c.subtarget = P + s + "Hand"
            c.use_x = False; c.use_z = False; c.use_y = True
            c.owner_space = 'LOCAL'; c.target_space = 'LOCAL'; c.mix_mode = 'REPLACE'; c.influence = share
