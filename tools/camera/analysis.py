#!/usr/bin/env python3
"""A model of the gameplay camera (the same maths as scripts/view/camera_rules.gd, no lens) and the measures a
vertical MOBA camera has to balance. Run it to compare settings:

    python3 tools/camera/analysis.py                 # the table of candidates
    python3 tools/camera/analysis.py 52 55 12.5 0 .64  # one setting: pitch, vfov, visible width, yaw, hero_y

Everything is measured at 720x1280 (9:16) unless an aspect is given. The hero is H; "ahead" is toward the enemy base.
"""
import math
import sys
import numpy as np

ASPECT = 9.0 / 16.0
SCREEN_H = 1280.0
KID = 1.85  # metres tall (KidModel.KID_HEIGHT)
LANE = (12.0, 24.0)  # half width and half length of the street


def rig(pitch, vfov, width, yaw, hero_y, aspect=ASPECT):
    """Camera position, basis and focal length for the aim point at the origin; returns the hero's ground point too."""
    t = math.tan(math.radians(vfov) / 2.0)
    dist = width / (2.0 * t * aspect)
    p, y = math.radians(pitch), math.radians(yaw)
    pos = np.array([dist * math.cos(p) * math.sin(y), dist * math.sin(p), dist * math.cos(p) * math.cos(y)])
    fwd = -pos / np.linalg.norm(pos)
    right = np.cross(fwd, np.array([0.0, 1.0, 0.0]))
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    cam = dict(pos=pos, fwd=fwd, right=right, up=up, t=t, aspect=aspect, dist=dist)
    hero = ground(cam, 0.5, hero_y)
    return cam, hero


def ray(cam, u, s):
    return cam["fwd"] + (2 * u - 1) * cam["t"] * cam["aspect"] * cam["right"] + (1 - 2 * s) * cam["t"] * cam["up"]


def ground(cam, u, s):
    d = ray(cam, u, s)
    if d[1] >= -1e-6:
        return None
    k = -cam["pos"][1] / d[1]
    return cam["pos"] + d * k


def project(cam, point):
    rel = point - cam["pos"]
    z = rel @ cam["fwd"]
    u = 0.5 + (rel @ cam["right"]) / (z * cam["t"] * cam["aspect"] * 2.0)
    s = 0.5 - (rel @ cam["up"]) / (z * cam["t"] * 2.0)
    return np.array([u * ASPECT * SCREEN_H, s * SCREEN_H]), z


def measures(pitch, vfov, width, yaw, hero_y, aspect=ASPECT):
    cam, hero = rig(pitch, vfov, width, yaw, hero_y, aspect)
    top = ground(cam, 0.5, 0.0)
    bottom = ground(cam, 0.5, 1.0)
    left = ground(cam, 0.0, hero_y)
    right = ground(cam, 1.0, hero_y)
    ahead = (hero[2] - top[2]) if top is not None else float("inf")
    behind = bottom[2] - hero[2]
    row_hero = float(np.linalg.norm(right - left))
    # the row 12 m ahead of the hero (toward -z): its left and right ground points
    target_z = hero[2] - 12.0
    row12 = None
    for s in np.linspace(hero_y, 0.0, 200):
        l, r = ground(cam, 0.0, s), ground(cam, 1.0, s)
        if l is None:
            break
        if min(l[2], r[2]) <= target_z <= max(l[2], r[2]) or (l[2] + r[2]) / 2 <= target_z:
            row12 = float(np.linalg.norm(r - l))
            break
    h_px, _ = project(cam, hero)
    h_top, _ = project(cam, hero + np.array([0.0, KID, 0.0]))
    kid_px = float(np.linalg.norm(h_top - h_px))
    if top is not None:
        t_px, _ = project(cam, top)
        t_top, _ = project(cam, top + np.array([0.0, KID, 0.0]))
        far_px = float(np.linalg.norm(t_top - t_px))
        gradient = kid_px / far_px if far_px > 0 else float("inf")
    else:
        gradient = float("inf")
    # a 5 m circle on the ground round the hero, drawn on the screen: height over width
    pts = [project(cam, hero + 5.0 * np.array([math.cos(a), 0.0, math.sin(a)]))[0] for a in np.linspace(0, 2 * math.pi, 72, endpoint=False)]
    pts = np.array(pts)
    ellipse = (pts[:, 1].max() - pts[:, 1].min()) / (pts[:, 0].max() - pts[:, 0].min())
    # a world direction 45 degrees off "ahead" shows on the screen at this angle (the finger would point at 45)
    a45 = project(cam, hero + np.array([5.0, 0.0, -5.0]))[0] - project(cam, hero)[0]
    seen45 = math.degrees(math.atan2(abs(a45[0]), abs(a45[1])))
    # how much of the playable street is on screen, averaged over where the hero can stand (the camera follows the hero)
    cover, count = 0.0, 0
    for hx in (-9.0, -4.5, 0.0, 4.5, 9.0):
        for hz in (-18.0, -9.0, 0.0, 9.0, 18.0):
            inside, total = 0, 0
            for u in np.linspace(0.02, 0.98, 25):
                for s in np.linspace(0.02, 0.98, 40):
                    g = ground(cam, u, s)
                    if g is None:
                        continue
                    total += 1
                    wx, wz = g[0] - hero[0] + hx, g[2] - hero[2] + hz
                    if abs(wx) <= LANE[0] and abs(wz) <= LANE[1]:
                        inside += 1
            cover += inside / max(total, 1)
            count += 1
    return dict(height=cam["pos"][1], dist=cam["dist"], ahead=ahead, behind=behind, row_hero=row_hero, row12=row12 or 0.0, kid_px=kid_px,
                gradient=gradient, ellipse=ellipse, seen45=seen45, cover=cover / count)


def main(args):
    if len(args) == 5:
        pitch, vfov, width, yaw, hero_y = (float(x) for x in args)
        for key, value in measures(pitch, vfov, width, yaw, hero_y).items():
            print(f"{key:10s} {value:8.2f}")
        return
    print(f"{'pitch':>5} {'fov':>4} {'w':>5} {'yaw':>4} {'hero':>5} | {'high':>5} {'dist':>5} {'ahead':>6} {'wide@h':>7} {'wide@12':>8} {'kid px':>7} {'grad':>5} {'circle':>6} {'45->':>5} {'lane%':>6}")
    for pitch in (46, 52, 56):
        for vfov in (45, 60, 75):
            for width in (12.0, 14.0):
                for yaw in (0, 10, 20):
                    m = measures(pitch, vfov, width, yaw, 0.64)
                    print(f"{pitch:5d} {vfov:4d} {width:5.1f} {yaw:4d} {0.64:5.2f} | {m['height']:5.1f} {m['dist']:5.1f} {m['ahead']:6.1f} {m['row_hero']:7.1f} {m['row12']:8.1f} {m['kid_px']:7.0f} {m['gradient']:5.2f} {m['ellipse']:6.2f} {m['seen45']:5.0f} {m['cover'] * 100:6.0f}")


if __name__ == "__main__":
    main(sys.argv[1:])
