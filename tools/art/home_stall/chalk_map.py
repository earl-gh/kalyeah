# Karding's chalk map of the Palengke (LORE: "draws chalk maps of every aisle"; callouts from MAPS.md), drawn in code as a
# stand-in for the owner's 2D art. White chalk on transparent, 2048 x 1024.
import math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
random.seed(11); np.random.seed(11)
W, H = 2048, 1024
ink = Image.new("L", (W, H), 0)
d = ImageDraw.Draw(ink)
FONT = "/home/user/kalyeah/assets/fonts/brand/KalyeahDisplay-Regular.ttf"

def wobble(pts, amp=3.0, step=14):
    out = []
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / step))
        for i in range(n):
            t = i / n
            out.append((x0 + (x1 - x0) * t + random.uniform(-amp, amp), y0 + (y1 - y0) * t + random.uniform(-amp, amp)))
    out.append(pts[-1])
    return out

def stroke(pts, w=7, passes=3, amp=2.5):
    for p in range(passes):
        q = wobble(pts, amp)
        d.line(q, fill=random.randint(150, 230), width=max(2, w - p * 2), joint="curve")

def box(x, y, bw, bh, **k):
    w, h = bw, bh
    stroke([(x, y), (x + w + random.uniform(-6, 6), y + random.uniform(-4, 4)), (x + w, y + h), (x + random.uniform(-5, 5), y + h + random.uniform(-4, 4)), (x, y)], **k)

def circle(cx, cy, r, **k):
    pts = [(cx + r * math.cos(a) * random.uniform(.97, 1.03), cy + r * math.sin(a) * random.uniform(.97, 1.03)) for a in np.linspace(0, 2 * math.pi + .3, 40)]
    stroke(pts, **k)

def text(x, y, s, size, rot=0):
    f = ImageFont.truetype(FONT, size)
    tw = int(d.textlength(s, font=f)) + 20
    t = Image.new("L", (tw, size + 30), 0)
    ImageDraw.Draw(t).text((10, 5), s, font=f, fill=220)
    t = t.rotate(rot, expand=True, resample=Image.BICUBIC)
    ink.paste(Image.fromarray(np.maximum(np.array(ink.crop((x, y, x + t.width, y + t.height))), np.array(t))), (x, y))

def arrow(x0, y0, x1, y1):
    stroke([(x0, y0), (x1, y1)], w=6)
    a = math.atan2(y1 - y0, x1 - x0)
    for s in (2.6, -2.6):
        stroke([(x1, y1), (x1 + 34 * math.cos(a + s), y1 + 34 * math.sin(a + s))], w=6)

# the market's outline and the two halves
stroke([(120, 150), (1930, 132), (1945, 860), (110, 878), (120, 150)], w=9)
for i, y in enumerate((210, 380, 550)):
    box(190, y, 300, 120); box(560, y + 10, 230, 115)
    box(1250, y - 5, 230, 120); box(1550, y + 6, 300, 118)
stroke([(1010, 170), (1018, 820)], w=6)                      # main aisle
arrow(960, 700, 960, 330); arrow(1070, 330, 1070, 700)
circle(1015, 470, 78, w=8)                                    # the big fish tank
stroke([(975, 470), (1005, 450), (1045, 470), (1005, 490), (975, 470), (955, 452), (955, 488), (975, 470)], w=5)  # a fish
for x in range(160, 1900, 70):                                # the catwalk, dashed
    stroke([(x, 760 + 10 * math.sin(x / 140)), (x + 38, 760 + 10 * math.sin((x + 38) / 140))], w=6, passes=2)
stroke([(110, 820), (1945, 808)], w=6)                        # loading bay road
# little drawings: rice sacks on the dry side, ice cubes on the wet side, a radio, an X
for k in range(3):
    stroke([(1600 + k * 60, 690), (1590 + k * 60, 650), (1620 + k * 60, 630), (1650 + k * 60, 650), (1640 + k * 60, 690), (1600 + k * 60, 690)], w=5, passes=2)
for k in range(3):
    box(250 + k * 54, 680, 40, 40, w=5, passes=2)
box(1680, 920, 120, 70, w=6); circle(1715, 955, 22, w=5); stroke([(1760, 935), (1785, 935)], w=4); stroke([(1700, 920), (1660, 870)], w=4)
stroke([(1460, 640), (1520, 700)], w=9); stroke([(1520, 640), (1460, 700)], w=9)
# callouts (MAPS.md) and his own note
text(250, 52, "WET", 92, 2); text(1600, 40, "DRY", 92, -2)
text(905, 880, "LOADING BAY", 70, 1); text(1100, 210, "MAIN AISLE", 52, -90)
text(1160, 700, "CATWALK", 52, 3); text(930, 560, "TANK", 46, 0)
text(1830, 900, "ayos na!", 46, 8)

# chalk grain: break the strokes up and add dust
a = np.array(ink).astype(np.float32) / 255.0
grain = np.random.rand(H, W).astype(np.float32)
a = a * np.clip((grain - 0.18) * 1.8, 0, 1)
dust = (np.random.rand(H, W) > 0.9985).astype(np.float32) * 0.5
smear = np.array(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(9))).astype(np.float32) / 255.0 * 0.35
a = np.clip(a + dust + smear, 0, 1)
rgba = np.zeros((H, W, 4), np.uint8); rgba[..., 0] = 244; rgba[..., 1] = 240; rgba[..., 2] = 228; rgba[..., 3] = (a * 255).astype(np.uint8)
Image.fromarray(rgba, "RGBA").save(sys.argv[1])
print("chalk", sys.argv[1])
