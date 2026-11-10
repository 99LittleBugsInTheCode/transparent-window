# -*- coding: utf-8 -*-
"""Generate assets/icon.ico for the transparency tool."""
import os
from PIL import Image, ImageDraw

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# outer dark rounded square
d.rounded_rectangle([24, 24, S - 24, S - 24], radius=210, fill=(22, 24, 30, 255),
                    outline=(52, 58, 78, 255), width=8)

# ghost window behind (semi transparent)
d.rounded_rectangle([150, 170, 600, 620], radius=70, fill=(240, 243, 255, 38),
                    outline=(240, 243, 255, 80), width=10)

# main window with gradient body
wx0, wy0, wx1, wy1 = 330, 300, 900, 860
head = 120
body = Image.new("RGBA", (wx1 - wx0, wy1 - wy0), (0, 0, 0, 0))
bd = ImageDraw.Draw(body)
for y in range(body.height):
    a = int(250 - (y / float(body.height)) * 210)
    bd.line([(0, y), (body.width, y)], fill=(242, 245, 255, a))
mask = Image.new("L", body.size, 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, body.width - 1, body.height - 1], radius=64, fill=255)
img.paste(body, (wx0, wy0), mask)

# title bar
d.rounded_rectangle([wx0, wy0, wx1, wy0 + head], radius=64, fill=(84, 110, 255, 255))
d.rectangle([wx0, wy0 + head - 64, wx1, wy0 + head], fill=(84, 110, 255, 255))

# window outline
d.rounded_rectangle([wx0, wy0, wx1, wy1], radius=64, outline=(255, 255, 255, 235), width=12)

# content lines
for i, y in enumerate((540, 620, 700)):
    d.rounded_rectangle([wx0 + 60, y, wx0 + 380 - i * 70, y + 26], radius=13, fill=(120, 128, 152, 190))

# little transparency checker hint at bottom right of window
for i in range(3):
    for j in range(2):
        if (i + j) % 2 == 0:
            x = 600 + i * 70
            y = 640 + j * 70
            d.rectangle([x, y, x + 66, y + 66], fill=(255, 255, 255, 90))

os.makedirs(os.path.join(os.path.dirname(__file__), "..", "assets"), exist_ok=True)
out = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "assets", "icon.ico"))
img.save(out, format="ICO", sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
print("wrote", out)
