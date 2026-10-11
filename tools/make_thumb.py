"""Generates src/EmbyTube.Plugin/thumb.png (neutral logo, no third-party artwork)."""
from PIL import Image, ImageDraw, ImageFont
import sys

W, H = 640, 360
img = Image.new("RGB", (W, H), (24, 28, 38))
d = ImageDraw.Draw(img)
# Ring with a play triangle.
d.ellipse((240, 50, 400, 210), outline=(38, 166, 154), width=14)
d.polygon([(298, 95), (298, 165), (358, 130)], fill=(38, 166, 154))
try:
    font = ImageFont.truetype("arialbd.ttf", 56)
except OSError:
    font = ImageFont.load_default()
text = "EmbyTube"
tw = d.textlength(text, font=font)
d.text(((W - tw) / 2, 255), text, fill=(240, 240, 240), font=font)
img.save(sys.argv[1], optimize=True)
