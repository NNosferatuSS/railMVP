"""
Gera os ícones de mundo dos pickups com linguagem de forma + cor:
  BOM  (power-up)        → CÍRCULO dourado, glifo branco.
  RUIM (debuff / hazard) → TRIÂNGULO de alerta vermelho, glifo branco.
Uso: python make_pickup_badges.py <projeto>
Saída: Assets/Graphics/Aurora/Pickups/*.png (256x256, RGBA).
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(ROOT, "Assets/Graphics/Aurora/Pickups")
S = 256
SS = 4  # supersampling
PICTO = os.path.join(ROOT, "Assets/Layer Lab/GUI Pro-MinimalGame/Shared/Icons/PictoIcon/256")
UIICON = os.path.join(ROOT, "Assets/UI/Sprites/icons")
FONT = os.path.join(ROOT, "Assets/UI/Fonts/BarlowSemiCondensed-Bold.ttf")


def radial(size, inner, outer, center=(0.42, 0.36)):
    w = h = size
    im = Image.new("RGB", (w, h))
    px = im.load()
    cx, cy = center[0] * w, center[1] * h
    rmax = ((w) ** 2 + (h) ** 2) ** 0.5 * 0.62
    for y in range(h):
        for x in range(w):
            t = min(1.0, (((x - cx) ** 2 + (y - cy) ** 2) ** 0.5) / rmax)
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(3))
    return im


def glyph_from_picto(name):
    im = Image.open(os.path.join(PICTO, name)).convert("RGBA")
    return im.split()[3]


def glyph_from_ui(name):
    """Extrai o glifo branco de um ícone de UI (fundo colorido)."""
    im = Image.open(os.path.join(UIICON, name)).convert("RGB")
    w, h = im.size
    m = Image.new("L", (w, h))
    src, dst = im.load(), m.load()
    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            lo, hi = min(r, g, b), max(r, g, b)
            v = 0
            if lo > 200 and hi - lo < 40:
                v = 255
            elif lo > 170 and hi - lo < 60:
                v = int((lo - 170) / 30 * 255)
            dst[x, y] = max(0, min(255, v))
    # tira a borda (só o centro tem glifo)
    box = Image.new("L", (w, h), 0)
    ImageDraw.Draw(box).rectangle([w * 0.14, h * 0.14, w * 0.86, h * 0.86], fill=255)
    return ImageChops.multiply(m, box)


def glyph_text(txt):
    m = Image.new("L", (S * SS, S * SS), 0)
    d = ImageDraw.Draw(m)
    f = ImageFont.truetype(FONT, int(150 * SS))
    bb = d.textbbox((0, 0), txt, font=f)
    d.text(((S * SS - (bb[2] - bb[0])) / 2 - bb[0], (S * SS - (bb[3] - bb[1])) / 2 - bb[1]), txt, font=f, fill=255)
    return m.resize((S, S), Image.LANCZOS)


def glyph_comet():
    """Cometa: estrela de 5 pontas com 3 rastros saindo pra trás (diagonal)."""
    import math
    big = S * SS
    m = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(m)
    cx, cy, R, r = big * 0.6, big * 0.4, big * 0.26, big * 0.11
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rad = R if i % 2 == 0 else r
        pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    d.polygon(pts, fill=255)
    for k, (dx, w) in enumerate([(-0.07, 0.05), (0.0, 0.065), (0.07, 0.05)]):
        x0, y0 = cx - big * 0.12 + big * dx * 0.7, cy + big * 0.12 + big * dx * 0.7
        x1, y1 = x0 - big * (0.34 - 0.06 * abs(k - 1)), y0 + big * (0.34 - 0.06 * abs(k - 1))
        d.line([(x0, y0), (x1, y1)], fill=255, width=int(big * w))
    return m.resize((S, S), Image.LANCZOS)


def fit(mask, scale, offset=(0, 0)):
    """Centraliza o glifo pelo bbox e escala pra caber em 'scale' do badge."""
    bb = mask.getbbox()
    if bb is None:
        return Image.new("L", (S, S), 0)
    g = mask.crop(bb)
    gw, gh = g.size
    k = (S * scale) / max(gw, gh)
    g = g.resize((max(1, int(gw * k)), max(1, int(gh * k))), Image.LANCZOS)
    out = Image.new("L", (S, S), 0)
    out.paste(g, (int((S - g.size[0]) / 2 + offset[0]), int((S - g.size[1]) / 2 + offset[1])))
    return out


def compose(shape_mask, fill_rgb, outline_rgb, glyph, glyph_shadow_rgb):
    big = S * SS
    shape = shape_mask
    # contorno escuro (borda) + corpo com gradiente + brilho de topo
    outline = shape.filter(ImageFilter.MaxFilter(1))
    body = ImageChops.subtract(shape, Image.new("L", shape.size, 0))
    inner = shape.resize((big, big)).filter(ImageFilter.MinFilter(4 * SS + 1)).resize((S, S), Image.LANCZOS)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    img.paste(Image.new("RGBA", (S, S), outline_rgb + (255,)), (0, 0), outline)
    grad = fill_rgb.convert("RGBA")
    img.paste(grad, (0, 0), inner)
    # sombra do glifo + glifo branco
    sh = glyph.filter(ImageFilter.GaussianBlur(3))
    shadow = Image.new("RGBA", (S, S), glyph_shadow_rgb + (0,))
    shadow.putalpha(ImageChops.multiply(sh, Image.new("L", (S, S), 150)))
    img = Image.alpha_composite(img, ImageChops.offset(shadow, 0, 4))
    white = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    white.putalpha(glyph)
    return Image.alpha_composite(img, white)


def circle_mask():
    m = Image.new("L", (S * SS, S * SS), 0)
    pad = 8 * SS
    ImageDraw.Draw(m).ellipse([pad, pad, S * SS - pad, S * SS - pad], fill=255)
    return m.resize((S, S), Image.LANCZOS)


def triangle_mask():
    big = S * SS
    m = Image.new("L", (big, big), 0)
    pad = 10 * SS
    pts = [(big / 2, pad + 6 * SS), (big - pad, big - pad - 14 * SS), (pad, big - pad - 14 * SS)]
    d = ImageDraw.Draw(m)
    d.polygon(pts, fill=255)
    # cantos arredondados
    m = m.filter(ImageFilter.GaussianBlur(9 * SS)).point(lambda v: 255 if v > 128 else 0)
    return m.resize((S, S), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    gold = radial(S, (255, 236, 150), (232, 142, 18))
    red = radial(S, (255, 110, 120), (178, 10, 40), center=(0.5, 0.45))
    circ, tri = circle_mask(), triangle_mask()

    good = {
        "Pickup_Shield": fit(glyph_from_ui("icon_shield.png"), 0.46),
        "Pickup_Magnet": fit(glyph_from_ui("icon_magnet.png"), 0.46),
        "Pickup_Ghost": fit(glyph_from_picto("orb.png"), 0.5),
        "Pickup_2x": fit(glyph_text("x2"), 0.5),
        "Pickup_Comet": fit(glyph_comet(), 0.56),
    }
    bad = {
        "Pickup_Bad_SpeedUp": fit(glyph_from_picto("growth.png"), 0.36, (0, 22)),
        "Pickup_Bad_LaneSwap": fit(glyph_from_picto("arrowhead.png"), 0.34, (0, 22)),
        "Pickup_Bad_Vortex": fit(glyph_from_picto("stun.png"), 0.36, (0, 22)),
    }
    for name, g in good.items():
        compose(circ, gold, (70, 40, 0), g, (120, 60, 0)).save(os.path.join(OUT, name + ".png"))
    for name, g in bad.items():
        compose(tri, red, (50, 0, 12), g, (90, 0, 20)).save(os.path.join(OUT, name + ".png"))
    print("ok", OUT)


if __name__ == "__main__":
    main()
