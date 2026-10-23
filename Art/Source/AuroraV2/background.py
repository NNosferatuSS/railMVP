# Aurora V2 — ilha flutuante de cristal (substitui o arco BG_Arc no mesmo slot do wizard).
# Pivô na base (z=0, ponta da raiz), topo ~z=24, como o arco V1 (27 de altura).
# O AuroraEnvironment recicla 5 instâncias nas laterais, abaixo das pistas (paralaxe).
import bpy, bmesh, math, random
from mathutils import Vector


def jitter_ring(n, r, z, rnd, jr=0.18, jz=0.6, rot=0.0, sy=1.0):
    pts = []
    for k in range(n):
        a = rot + 2 * math.pi * k / n
        rr = r * (1.0 + rnd.uniform(-jr, jr))
        pts.append(Vector((math.cos(a) * rr, math.sin(a) * rr * sy, z + rnd.uniform(-jz, jz))))
    return pts


def spire(bm, base, h, r, tilt, rnd):
    """Cristal hexagonal inclinado saindo do platô."""
    ax = Vector((math.sin(tilt[0]), math.sin(tilt[1]), 1.0)).normalized()
    side = ax.orthogonal().normalized()
    other = ax.cross(side)
    ring_b, ring_m = [], []
    for k in range(6):
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.1, 0.1)
        d = side * math.cos(a) + other * math.sin(a)
        ring_b.append(base + d * r - ax * 0.8)
        ring_m.append(base + d * r * 0.92 + ax * h * 0.72)
    tip = base + ax * h
    vb = [bm.verts.new(p) for p in ring_b]
    vm = [bm.verts.new(p) for p in ring_m]
    vt = bm.verts.new(tip)
    for k in range(6):
        j = (k + 1) % 6
        bm.faces.new((vb[k], vb[j], vm[j], vm[k]))
        bm.faces.new((vm[k], vm[j], vt))
    bm.faces.new(list(reversed(vb)))


def build_island(seed=11):
    rnd = random.Random(seed)
    bm = bmesh.new()
    n = 11
    rings = [
        jitter_ring(n, 6.4, 23.0, rnd, 0.12, 0.35),   # platô
        jitter_ring(n, 7.4, 21.6, rnd, 0.10, 0.5),    # borda
        jitter_ring(n, 6.6, 18.5, rnd, 0.16, 0.8),
        jitter_ring(n, 4.6, 13.0, rnd, 0.22, 1.0),
        jitter_ring(n, 2.4, 6.5, rnd, 0.25, 0.8),
        [Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0.0))],
    ]
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    for i in range(len(vr) - 1):
        a, b = vr[i], vr[i + 1]
        if len(b) == 1:
            for k in range(n):
                bm.faces.new((a[k], b[0], a[(k + 1) % n]))
            continue
        for k in range(n):
            j = (k + 1) % n
            bm.faces.new((a[k], b[k], b[j], a[j]))
    bm.faces.new(vr[0])
    # cristais no platô
    for (x, y, h, r, tx, ty) in ((-1.5, 0.8, 7.5, 1.1, -0.25, 0.1), (1.6, -0.6, 5.2, 0.85, 0.3, -0.15),
                                 (0.4, 2.6, 4.0, 0.7, 0.05, 0.4), (-3.4, -1.9, 3.1, 0.6, -0.4, -0.3),
                                 (3.6, 2.0, 2.6, 0.5, 0.45, 0.2)):
        spire(bm, Vector((x, y, 23.0)), h, r, (tx, ty), rnd)
    # pedras soltas orbitando (vendem "flutuar")
    for (x, y, z, s) in ((9.5, 1.0, 15.0, 1.3), (-9.0, -2.0, 11.0, 0.9), (6.0, -4.5, 6.0, 0.7)):
        top = jitter_ring(5, s, z + s * 0.5, rnd, 0.2, 0.1)
        mid = jitter_ring(5, s * 1.1, z, rnd, 0.2, 0.1, rot=0.3)
        top = [p + Vector((x, y, 0)) for p in top]
        mid = [p + Vector((x, y, 0)) for p in mid]
        vt = [bm.verts.new(p) for p in top]
        vm = [bm.verts.new(p) for p in mid]
        vb = bm.verts.new((x, y, z - s * 1.6))
        for k in range(5):
            j = (k + 1) % 5
            bm.faces.new((vt[k], vm[k], vm[j], vt[j]))
            bm.faces.new((vm[k], vb, vm[j]))
        bm.faces.new(vt)
    me = finish_mesh(bm, "BG_Arc")
    o = replace_object("BG_Arc", me)
    o.data.materials.append(material("Arc"))
    o.location = (-20.0, 0.0, -30.0)
    compute_ao(o, rays=48, distance=4.0)
    remap_ao(o, lo=0.45, height_fn=lambda z: 0.55 + 0.45 * min(1.0, z / 23.0))
    return o


isl = build_island()
print("BG_Arc", len(isl.data.polygons), "faces", tuple(round(v, 2) for v in isl.dimensions))
