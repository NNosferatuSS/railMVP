# Aurora V2 — conectores, coin, gem, player, hazards e anel de power-up.
# Mantém nomes de mesh, pivôs e dimensões que o wizard (AuroraArtSetup) espera;
# só a geometria ganha volume (chanfros, facetas, quilhas, peças separadas).
import bpy, bmesh, math
from mathutils import Vector, Matrix


def loft_rings(bm, rings, band_mats=None, cap_first=True, cap_last=True, cap_mats=(0, 0)):
    """
    Liga anéis 3D (mesma contagem de pontos; anel de 1 ponto = ponta/leque).
    band_mats[i] = material da faixa entre o anel i e o i+1.
    """
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    for i in range(len(vr) - 1):
        a, b = vr[i], vr[i + 1]
        m = band_mats[i] if band_mats else 0
        if len(a) == 1 or len(b) == 1:
            tip, ring = (a[0], b) if len(a) == 1 else (b[0], a)
            n = len(ring)
            for k in range(n):
                tri = (tip, ring[(k + 1) % n], ring[k]) if len(a) == 1 else (ring[k], ring[(k + 1) % n], tip)
                bm.faces.new(tri).material_index = m
            continue
        n = len(a)
        for k in range(n):
            j = (k + 1) % n
            bm.faces.new((a[k], b[k], b[j], a[j])).material_index = m
    if cap_first and len(vr[0]) > 2:
        bm.faces.new(vr[0]).material_index = cap_mats[0]
    if cap_last and len(vr[-1]) > 2:
        bm.faces.new(list(reversed(vr[-1]))).material_index = cap_mats[1]
    return vr


def ring2d(poly, z, s=1.0, off=(0.0, 0.0)):
    return [Vector((p.x * s + off[0], p.y * s + off[1], z)) for p in poly]


def ngon(n, r, rot=0.0, sy=1.0):
    return [Vector((math.cos(rot + 2 * math.pi * k / n) * r, math.sin(rot + 2 * math.pi * k / n) * r * sy)) for k in range(n)]


def box_rings(cx, cy, w, l, z0, z1, ch=0.0, top_ch=0.0):
    """Caixa com chanfro vertical nos cantos (ch) e chanfro no topo (top_ch)."""
    out = chamfered_rect(w, l, ch) if ch > 0 else [Vector((-w / 2, -l / 2)), Vector((w / 2, -l / 2)), Vector((w / 2, l / 2)), Vector((-w / 2, l / 2))]
    out = [p + Vector((cx, cy)) for p in out]
    rings = []
    if top_ch > 0:
        rings.append(ring2d(offset_polygon(out, top_ch), z1))
        rings.append(ring2d(out, z1 - top_ch))
    else:
        rings.append(ring2d(out, z1))
    rings.append(ring2d(out, z0))
    return rings


def solid(bm, rings, mats=None, cap_mats=(0, 0)):
    """Anéis de cima pra baixo → sólido fechado."""
    return loft_rings(bm, rings, mats, cap_mats=cap_mats)


def ao_by_normal(obj, up=1.0, side=0.55, down=0.25, keep_mat=None):
    """AO 'pintado' pela direção da normal (pra peças emissivas: topo aceso, lados apagados)."""
    attr = ensure_ao_attr(obj)
    for p in obj.data.polygons:
        if keep_mat is not None and p.material_index == keep_mat:
            v = 1.0
        else:
            nz = p.normal.z
            v = side + (up - side) * max(0.0, nz) if nz >= 0 else side + (down - side) * (-nz)
        for li in p.loop_indices:
            attr.data[li].color = (v, v, v, 1.0)


def new_obj(name, bm, mats, flat=True, x=0.0):
    me = finish_mesh(bm, name, flat=flat)
    o = replace_object(name, me)
    for n in mats:
        o.data.materials.append(material(n))
    o.location.x = x
    return o


# ------------------------------------------------------------------ conectores

def build_connector(name, length, x):
    """Viga segmentada: placas emissivas sobre uma espinha escura. y de -0.2 a length-0.2."""
    bm = bmesh.new()
    y0, y1 = -0.2, length - 0.2
    # espinha (contínua), topo um pouco abaixo das placas
    def spine(hw, inset_y=0.0):
        return [Vector((-hw, y0 + inset_y)), Vector((hw, y0 + inset_y)), Vector((hw, y1 - inset_y)), Vector((-hw, y1 - inset_y))]
    solid(bm, [ring2d(spine(0.17), 0.02), ring2d(spine(0.22), -0.05),
               ring2d(spine(0.22), -0.13), ring2d(spine(0.11, 0.06), -0.30)])
    # placas com chanfro: passo 0.5, folga 0.07
    pitch, gap = 0.5, 0.07
    n = max(1, int(round((y1 - y0) / pitch)))
    pitch = (y1 - y0) / n
    for k in range(n):
        a = y0 + k * pitch + gap * 0.5
        b = y0 + (k + 1) * pitch - gap * 0.5
        plate = [Vector((-0.26, a)), Vector((0.26, a)), Vector((0.26, b)), Vector((-0.26, b))]
        solid(bm, [ring2d(offset_polygon(plate, 0.07), 0.085), ring2d(plate, 0.045), ring2d(plate, 0.0)])
    o = new_obj(name, bm, ["Glow"], x=x)
    ao_by_normal(o, up=1.0, side=0.42, down=0.15)
    return o


# ------------------------------------------------------------------ coin / gem

def build_coin(x):
    """Losango lapidado: mesa, coroa facetada, cinta e pavilhão (lê volume de cima)."""
    bm = bmesh.new()
    d = [Vector((0, -1)), Vector((1, 0)), Vector((0, 1)), Vector((-1, 0))]
    R = 0.36
    rings = [ring2d(d, 0.12, R * 0.52),   # mesa
             ring2d(d, 0.07, R * 0.86),   # coroa
             ring2d(d, 0.035, R),         # cinta
             ring2d(d, -0.035, R),
             ring2d(d, -0.08, R * 0.7),   # pavilhão
             [Vector((0, 0, -0.13))]]
    solid(bm, rings)
    # emblema: losango menor elevado na mesa
    solid(bm, [ring2d(d, 0.15, R * 0.22), ring2d(d, 0.12, R * 0.32)], cap_mats=(0, 0))
    o = new_obj("Coin_Lozenge", bm, ["Coin"], x=x)
    compute_ao(o, rays=64, distance=0.12)
    remap_ao(o, lo=0.55)
    return o


def build_gem(x):
    """Cristal hexagonal alongado com coroa facetada (mesmo tamanho do V1)."""
    bm = bmesh.new()
    h6 = ngon(6, 1.0, 0.0)
    rings = [[Vector((0, 0, 0.42))],
             ring2d(h6, 0.24, 0.12),
             ring2d(h6, 0.14, 0.2),
             ring2d(h6, -0.06, 0.2),
             ring2d(h6, -0.16, 0.14),
             [Vector((0, 0, -0.32))]]
    solid(bm, rings)
    o = new_obj("Gem_Crystal", bm, ["Gem"], x=x)
    ensure_ao_attr(o)
    remap_ao(o, lo=1.0, height_fn=lambda z: 0.6 + 0.4 * max(0.0, min(1.0, (z + 0.32) / 0.6)))
    return o


# ------------------------------------------------------------------ player

def build_crescent(x):
    """Crescente como tubo varrido e afinado nas pontas, abraçando a pérola por trás."""
    bm = bmesh.new()
    seg, sides = 40, 10
    cx, cy, R = 0.0, -0.12, 0.40
    a0, a1 = math.radians(-90 - 125), math.radians(-90 + 125)
    rings = []
    for i in range(seg + 1):
        t = i / seg
        a = a0 + (a1 - a0) * t
        c = Vector((cx + math.cos(a) * R, cy + math.sin(a) * R, 0.0))
        tangent = Vector((-math.sin(a), math.cos(a), 0.0))
        radial = Vector((math.cos(a), math.sin(a), 0.0))
        w = math.sin(math.pi * t) ** 0.7
        r = 0.018 + 0.062 * w
        ring = []
        for k in range(sides):
            th = 2 * math.pi * k / sides
            p = c + radial * (math.cos(th) * r * 1.25) + Vector((0, 0, math.sin(th) * r * 0.75))
            ring.append(p)
        rings.append(ring)
    rings = [[rings[0][0].lerp(rings[0][sides // 2], 0.5)]] + rings + [[rings[-1][0].lerp(rings[-1][sides // 2], 0.5)]]
    loft_rings(bm, rings, cap_first=False, cap_last=False)
    o = new_obj("Player_Crescent", bm, ["Crescent"], flat=False, x=x)
    ensure_ao_attr(o)
    return o


def build_pearl(x):
    src = bpy.data.objects.get("V1_Player_Pearl")
    me = src.data.copy()
    o = replace_object("Player_Pearl", me)
    o.location.x = x
    for p in o.data.polygons:
        p.use_smooth = True
    ensure_ao_attr(o)
    return o


# ------------------------------------------------------------------ hazards

def stripes_on_face(bm, x0, x1, z0, z1, y, n, slant, normal_sign, mat):
    """Faixas diagonais em relevo (0.012) numa face vertical plana em y."""
    w = (x1 - x0) / (n * 2)
    for k in range(n):
        a = x0 + w * (2 * k + 0.5)
        quad = [Vector((a, y, z0)), Vector((a + w, y, z0)), Vector((a + w + slant, y, z1)), Vector((a + slant, y, z1))]
        dy = 0.014 * normal_sign
        outer = [q + Vector((0, dy, 0)) for q in quad]
        vs = [bm.verts.new(q) for q in quad] + [bm.verts.new(q) for q in outer]
        for f in ((4, 5, 6, 7), (3, 2, 1, 0), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            bm.faces.new([vs[i] for i in f]).material_index = mat


def spike(bm, cx, cy, base, h, z0, mat=0):
    sq = ngon(4, base, math.pi / 4)
    solid(bm, [[Vector((cx, cy, z0 + h))],
               ring2d(sq, z0 + h * 0.28, 0.62, (cx, cy)),
               ring2d(sq, z0 + 0.02, 1.0, (cx, cy)),
               ring2d(sq, z0 - 0.02, 1.0, (cx, cy))], [mat, mat, mat])


def build_hazard_lethal(x):
    """Base chanfrada com faixas + fileira de espinhos facetados. Base em z=0."""
    bm = bmesh.new()
    W, D = 1.1, 0.62
    solid(bm, box_rings(0, 0, W, D, 0.0, 0.26, ch=0.1, top_ch=0.05))
    stripes_on_face(bm, -0.4, 0.4, 0.05, 0.19, -D / 2, 3, 0.1, -1, 1)
    stripes_on_face(bm, -0.4, 0.4, 0.05, 0.19, D / 2, 3, 0.1, 1, 1)
    for cx, b, h in ((-0.33, 0.2, 0.5), (0.0, 0.24, 0.6), (0.33, 0.2, 0.5)):
        spike(bm, cx, 0.0, b, h, 0.26)
    for cx in (-0.17, 0.17):
        spike(bm, cx, 0.16, 0.1, 0.24, 0.26)
        spike(bm, cx, -0.16, 0.1, 0.24, 0.26)
    o = new_obj("Hazard_Lethal", bm, ["Hazard", "HazardStripe"], x=x)
    compute_ao(o, rays=80, distance=0.3)
    remap_ao(o, lo=0.4)
    return o


def build_hazard_barrier(x):
    """Barreira de obra: dois postes chanfrados + painel listrado. Base em z=0."""
    bm = bmesh.new()
    for cx in (-0.53, 0.53):
        solid(bm, box_rings(cx, 0, 0.16, 0.26, 0.0, 0.62, ch=0.04, top_ch=0.03))
        solid(bm, box_rings(cx, 0, 0.24, 0.3, 0.0, 0.07, ch=0.05, top_ch=0.02))  # sapata
    solid(bm, box_rings(0, 0, 0.92, 0.14, 0.24, 0.54, ch=0.03, top_ch=0.025))
    stripes_on_face(bm, -0.44, 0.44, 0.27, 0.51, -0.07, 4, 0.1, -1, 1)
    stripes_on_face(bm, -0.44, 0.44, 0.27, 0.51, 0.07, 4, 0.1, 1, 1)
    solid(bm, box_rings(0, 0, 0.92, 0.1, 0.09, 0.16, ch=0.02, top_ch=0.015))  # travessa
    o = new_obj("Hazard_Barrier", bm, ["Hazard", "HazardStripe"], x=x)
    compute_ao(o, rays=80, distance=0.3)
    remap_ao(o, lo=0.4)
    return o


# ------------------------------------------------------------------ power-up

def build_powerup_ring(x):
    """Anel com seção chanfrada + 8 marcadores; plano XZ (fino em Y) como o V1."""
    bm = bmesh.new()
    seg = 48
    Ro, Ri, T = 0.545, 0.455, 0.06
    prof = [(Ro - 0.02, T), (Ro, T * 0.4), (Ro, -T * 0.4), (Ro - 0.02, -T),
            (Ri + 0.02, -T), (Ri, -T * 0.4), (Ri, T * 0.4), (Ri + 0.02, T)]
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        rings.append([Vector((math.cos(a) * r, y, math.sin(a) * r)) for r, y in prof])
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    n = len(prof)
    for i in range(seg):
        a, b = vr[i], vr[(i + 1) % seg]
        for k in range(n):
            j = (k + 1) % n
            bm.faces.new((a[k], a[j], b[j], b[k]))
    for i in range(8):  # marcadores salientes (lê como "mostrador")
        a = 2 * math.pi * (i + 0.5) / 8
        c = Vector((math.cos(a), 0, math.sin(a)))
        t = Vector((-math.sin(a), 0, math.cos(a)))
        pts = []
        for (u, v) in ((-0.03, Ro - 0.01), (0.03, Ro - 0.01), (0.018, Ro + 0.05), (-0.018, Ro + 0.05)):
            pts.append(c * v + t * u)
        ring_f = [p + Vector((0, T * 0.8, 0)) for p in pts]
        ring_b = [p + Vector((0, -T * 0.8, 0)) for p in pts]
        vs = [bm.verts.new(p) for p in ring_f + ring_b]
        for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([vs[q] for q in f])
    o = new_obj("PowerUp_Ring", bm, ["PowerRing"], x=x)
    ao_by_normal(o, up=1.0, side=1.0, down=1.0)
    return o


def build_all():
    objs = {
        "ConnectorStraight": build_connector("Connector_Straight", 4.4, 2.0),
        "ConnectorDiag": build_connector("Connector_Diag", 5.4, 3.0),
        "Coin": build_coin(4.5),
        "Gem": build_gem(5.5),
        "Crescent": build_crescent(7.0),
        "Pearl": build_pearl(7.0),
        "HazardLethal": build_hazard_lethal(9.0),
        "HazardBarrier": build_hazard_barrier(10.8),
        "PowerUpRing": build_powerup_ring(12.6),
    }
    for k, o in objs.items():
        print(k, o.name, len(o.data.polygons), "faces", tuple(round(v, 3) for v in o.dimensions))
    return objs


ASSETS = build_all()
