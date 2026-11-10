# A sign whose painted words are too blurred to clean (the tricycle terminal's): lettered again in its own colours.
# usage: sign_letter.py "WORDS" font.ttf board_hex border_hex ink_hex width height out.png
import sys
from PIL import Image, ImageDraw, ImageFont
words, font, board, border, ink = sys.argv[1:6]; W, H = int(sys.argv[6]), int(sys.argv[7])
im = Image.new("RGB", (W, H), board); d = ImageDraw.Draw(im)
e = int(H * 0.11); d.rectangle([0, 0, W - 1, H - 1], outline=border, width=e)
size = H
while True:
	f = ImageFont.truetype(font, size); b = d.textbbox((0, 0), words, font=f)
	if b[2] - b[0] <= W * 0.86 and b[3] - b[1] <= H * 0.56: break
	size -= 2
d.text(((W - (b[2] - b[0])) / 2 - b[0], (H - (b[3] - b[1])) / 2 - b[1]), words, font=f, fill=ink)
im.save(sys.argv[8]); print("lettered", words, W, H)
