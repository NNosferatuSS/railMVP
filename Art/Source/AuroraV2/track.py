# Aurora V2 — trilho (Track_Ribbon). Footprint igual ao V1: 1.36 × 10, centro na origem,
# topo do deck em z = 0.10 (TrackTopY do wizard). Materiais: 0 = Body, 1 = Rim.
# Volume: lábio emissivo no contorno, chanfro externo, parede, linha de underglow e
# quilha afunilada que some no fog de altura. Chevrons rebaixados apontam pro switch.
import bpy, bmesh, math
from mathutils import Vector, Matrix

W, L, CH = 1.36, 10.0, 0.22
BODY, RIM = 0, 1

PROFILE = [  # (inset a partir do contorno externo, z, material da faixa até o próximo anel)
    (0.40, 0.100, BODY),   # faixa central do deck (AO 1 → gradiente pro lábio)
    (0.17, 0.100, BODY),   # borda do deck
    (0.135, 0.130, RIM),   # sobe pro lábio  -> faixa emissiva (topo do lábio)
    (0.065, 0.130, BODY),
    (0.000, 0.070, BODY),  # chanfro externo
    (0.000, -0.120, BODY), # parede
    (0.030, -0.150, RIM),  # linha de underglow
    (0.060, -0.165, BODY),
    (0.300, -0.460, BODY), # quilha
    (0.440, -0.900, BODY),
]


def build_track():
    bm = bmesh.new()
    outline = chamfered_rect(W, L, CH)
    inset = chamfered_inset(W, L, CH)
    rings, _, caps = loft(bm, outline, PROFILE, mat_top=BODY, mat_bottom=BODY, inset_fn=inset)
    # Raiz de cristal sob a quilha: plataforma flutuante; a ponta some no fog de altura.
    keel = rings[-1]
    bm.faces.remove(caps["bottom"])
    root_poly = inset(PROFILE[-1][0])
    mid = [bm.verts.new((p.x * 0.55, p.y * 0.42, -1.75)) for p in root_poly]
    low = [bm.verts.new((p.x * 0.22, p.y * 0.16, -2.7)) for p in root_poly]
    tip = bm.verts.new((0.0, 0.0, -3.6))
    n = len(root_poly)
    for a, b in ((keel, mid), (mid, low)):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], b[i], b[j], a[j])).material_index = BODY
    for i in range(n):
        bm.faces.new((low[i], tip, low[(i + 1) % n])).material_index = BODY
    me = finish_mesh(bm, "Track_Ribbon")
    obj = replace_object("Track_Ribbon", me)
    for n in ("Body", "Rim"):
        obj.data.materials.append(material(n))

    # Chevrons rebaixados (boolean) perto da ponta, apontando pra +Y (sentido da corrida).
    # Um cortador (caixa rotacionada) por braço; os do mesmo chevron se cruzam no vértice.
    arm, span, rise, depth = 0.085, 0.34, 0.22, 0.03
    yaw = math.atan2(rise, span)
    arm_len = math.hypot(span, rise) + arm * 0.5
    bpy.context.view_layer.objects.active = obj
    for y0 in (3.05, 3.65):
        for side in (-1, 1):
            cbm = bmesh.new()
            bmesh.ops.create_cube(cbm, size=1.0)
            rot = Matrix.Rotation(-side * yaw, 4, 'Z')
            for v in cbm.verts:
                p = Vector((v.co.x * arm_len + side * arm_len * 0.5, v.co.y * arm, v.co.z * 0.3))
                p = rot @ p
                v.co = Vector((p.x, p.y + y0, p.z + 0.1 - depth + 0.15))
            c =bpy.data.objects.new("_cut", finish_mesh(cbm, "_cut"))
            bpy.context.scene.collection.objects.link(c)
            mod = obj.modifiers.new("chevron", 'BOOLEAN')
            mod.operation = 'DIFFERENCE'
            mod.solver = 'EXACT'
            mod.object = c
            bpy.ops.object.modifier_apply(modifier=mod.name)
            cme = c.data
            bpy.data.objects.remove(c, do_unlink=True)
            bpy.data.meshes.remove(cme)
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def ao_track(obj):
    """AO ray-cast + deck abaulado (centro claro, bordas escuras) + raiz escurecendo pra baixo."""
    compute_ao(obj, rays=96, distance=0.45)

    def height(z):
        if z >= -0.9:
            return 1.0
        return 1.0 - 0.55 * min(1.0, (-0.9 - z) / 2.7)
    remap_ao(obj, lo=0.3, height_fn=height)
    me = obj.data
    attr = me.color_attributes[AO_ATTR]
    for p in me.polygons:
        if p.normal.z < 0.95 or p.center.z < 0.09:
            continue
        for li in p.loop_indices:
            x = abs(me.vertices[me.loops[li].vertex_index].co.x)
            t = min(1.0, max(0.0, (x - 0.28) / 0.23))
            k = 1.0 - 0.28 * t * t * (3 - 2 * t)
            c = attr.data[li].color[0] * k
            attr.data[li].color = (c, c, c, 1.0)
    tint_faces(obj, lambda p: p.material_index == RIM, 1.0)


track = build_track()
ao_track(track)
print("Track_Ribbon faces", len(track.data.polygons), "dims", tuple(round(x, 3) for x in track.dimensions))
