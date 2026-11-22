#!/usr/bin/env python3
"""Phase 1: Verify standardized competitive proportions across all 12 kids & export rig__hitbox_capsules.json.

Reads assets/models/characters/<kid>_rig.json and unity/Data/rules__game_rules.json, verifies every kid's
standardized height, head/shoulder/eye-line ratios, and fair cylinder hitbox fit (<= player_radius 0.45m),
and writes unity/Data/rig__hitbox_capsules.json wrapped identically to unity/Data/rig__joint_limits.json
so SimData.Load and WeaponTests.EveryExportedPropertyHasAHomeInThePort pass with zero unmapped keys.

Usage:
  python tools/blender/kids/export_hitbox_capsules.py
"""
from __future__ import annotations

import json
import math
from pathlib import Path

ALL_KIDS = (
    "domeng", "inggo", "jopay", "jumong", "karding", "kikay",
    "manoy", "narda", "oriang", "paeng", "pamela", "tere",
)
MATCH_KID_HEIGHT_M = 1.85
MATCH_VIEW_SCALE = 0.82
MATCH_EFFECTIVE_HEIGHT_M = round(MATCH_KID_HEIGHT_M * MATCH_VIEW_SCALE, 3)


def main() -> int:
    root = Path(__file__).resolve().parents[3]
    chars_dir = root / "assets" / "models" / "characters"
    data_dir = root / "unity" / "Data"

    rules = json.loads((data_dir / "rules__game_rules.json").read_text(encoding="utf-8"))["data"]
    player_radius = float(rules.get("player_radius", 0.45))

    roster_props: dict[str, dict] = {}
    all_fit = True
    for kid in ALL_KIDS:
        rig_path = chars_dir / f"{kid}_rig.json"
        if not rig_path.is_file():
            raise FileNotFoundError(f"Missing rig JSON: {rig_path}")
        rig = json.loads(rig_path.read_text(encoding="utf-8"))
        bones = rig["bones"]
        H = float(rig["heightM"])
        head_y = float(bones["Head"]["head"][1])
        head_top_y = float(bones.get("HeadTop_End", bones["Head"])["tail"][1])
        eye_y = head_y + 0.50 * max(head_top_y - head_y, H * 0.16)
        l_arm = bones["LeftArm"]["head"]
        r_arm = bones["RightArm"]["head"]
        shoulder_w = math.hypot(l_arm[0] - r_arm[0], l_arm[2] - r_arm[2])
        girth = rig.get("girth", {})
        torso_r = float(girth.get("torsoRadiusM", 0.16))
        arm_r = float(girth.get("armRadiusM", 0.06))
        # Competitive core half-width in match scale (~0.892 of 1.70m authored scale)
        scale_to_match = MATCH_EFFECTIVE_HEIGHT_M / max(H, 0.5)
        match_body_half_w = (0.5 * shoulder_w + min(arm_r, 0.085)) * scale_to_match
        fits = match_body_half_w <= player_radius and (1.65 <= H <= 1.75)
        all_fit = all_fit and fits
        roster_props[kid] = {
            "authoredHeightM": round(H, 3),
            "matchHeightM": MATCH_EFFECTIVE_HEIGHT_M,
            "headBaseRatio": round(head_y / H, 3),
            "eyeLineRatio": round(min(eye_y / H, 0.95), 3),
            "shoulderWidthM": round(shoulder_w, 3),
            "torsoRadiusM": round(torso_r, 3),
            "matchCoreRadiusM": round(match_body_half_w, 3),
            "fitsPlayerCylinderRadius": fits,
            "springChains": [c["name"] for c in rig.get("springChains", [])],
        }

    # Standardized competitive Valorant-bar hitbox capsules (identical across all 12 kids for zero hitbox bias!)
    # Coordinates are in bone-local Unity/glTF rest space (+Y along bone head->tail, in authored 1.70m metres;
    # runtime scales automatically with the KidView uniform model scale!).
    capsules = {
        "Head": {
            "bone": "mixamorig:Head",
            "zone": "head",
            "damageMultiplier": 1.5,
            "localOffset": [0.0, 0.135, 0.012],
            "radius": 0.132,
            "height": 0.295,
            "axis": "Y",
        },
        "Chest": {
            "bone": "mixamorig:Spine2",
            "zone": "torso",
            "damageMultiplier": 1.0,
            "localOffset": [0.0, 0.075, 0.0],
            "radius": 0.165,
            "height": 0.34,
            "axis": "Y",
        },
        "Stomach": {
            "bone": "mixamorig:Spine",
            "zone": "torso",
            "damageMultiplier": 1.0,
            "localOffset": [0.0, 0.055, 0.0],
            "radius": 0.155,
            "height": 0.27,
            "axis": "Y",
        },
        "UpperArm_L": {
            "bone": "mixamorig:LeftArm",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.105, 0.0],
            "radius": 0.068,
            "height": 0.24,
            "axis": "Y",
        },
        "UpperArm_R": {
            "bone": "mixamorig:RightArm",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.105, 0.0],
            "radius": 0.068,
            "height": 0.24,
            "axis": "Y",
        },
        "Forearm_L": {
            "bone": "mixamorig:LeftForeArm",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.11, 0.0],
            "radius": 0.058,
            "height": 0.24,
            "axis": "Y",
        },
        "Forearm_R": {
            "bone": "mixamorig:RightForeArm",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.11, 0.0],
            "radius": 0.058,
            "height": 0.24,
            "axis": "Y",
        },
        "Thigh_L": {
            "bone": "mixamorig:LeftUpLeg",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.17, 0.0],
            "radius": 0.092,
            "height": 0.36,
            "axis": "Y",
        },
        "Thigh_R": {
            "bone": "mixamorig:RightUpLeg",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.17, 0.0],
            "radius": 0.092,
            "height": 0.36,
            "axis": "Y",
        },
        "Calf_L": {
            "bone": "mixamorig:LeftLeg",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.18, 0.0],
            "radius": 0.072,
            "height": 0.38,
            "axis": "Y",
        },
        "Calf_R": {
            "bone": "mixamorig:RightLeg",
            "zone": "limbs",
            "damageMultiplier": 0.85,
            "localOffset": [0.0, 0.18, 0.0],
            "radius": 0.072,
            "height": 0.38,
            "axis": "Y",
        },
    }

    # Outer wrapper matches unity/Data/rig__joint_limits.json identically so SimData.Load and
    # WeaponTests.EveryExportedPropertyHasAHomeInThePort skip non-sim rig metadata cleanly.
    doc = {
        "source": "res://data/rig/hitbox_capsules.tres",
        "class": "RigJointLimits",
        "data": {
            "$class": "HitboxCapsules",
            "schemaVersion": 1,
            "coordSpace": "bone_local_unity",
            "standardizedHeightM": 1.7,
            "matchKidHeightM": MATCH_KID_HEIGHT_M,
            "matchViewScale": MATCH_VIEW_SCALE,
            "matchEffectiveHeightM": MATCH_EFFECTIVE_HEIGHT_M,
            "simPlayerRadiusM": player_radius,
            "allKidsFitCylinderHitbox": all_fit,
            "damageMultipliers": {
                "head": 1.5,
                "torso": 1.0,
                "limbs": 0.85,
            },
            "capsules": capsules,
            "rosterProportions": roster_props,
        },
    }

    out_path = data_dir / "rig__hitbox_capsules.json"
    out_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"[hitbox] wrote {out_path} ({len(capsules)} capsules, {len(roster_props)} kids, all_fit={all_fit})")
    return 0 if all_fit else 1


if __name__ == "__main__":
    raise SystemExit(main())
