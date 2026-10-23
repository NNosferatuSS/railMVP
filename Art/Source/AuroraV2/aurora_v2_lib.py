# Aurora V2 — utilitários de modelagem procedural (bpy/bmesh).
# Uso no Blender (MCP): exec(open(<este arquivo>).read()) e chamar as funções.
# Convenção: Blender (x, y, z) → Unity (x, z, y). Y = comprimento, Z = cima.
import bpy, bmesh, math
from mathutils import Vector, Matrix

V2_COLLECTION = "Aurora_V2"


def v2_collection():
    col = bpy.data.collections.get(V2_COLLECTION)
    if col is None:
        col = bpy.data.collections.new(V2_COLLECTION)
        bpy.context.scene.collection.children.link(col)
    return col


def replace_object(name, mesh):
    """Cria (ou substitui) o objeto `name` na coleção V2 com o mesh dado."""
    old = bpy.data.objects.get(name)
    if old is not None:
        old_mesh = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        if old_mesh.users == 0:
            bpy.data.meshes.remove(old_mesh)
    mesh.name = name
    obj = bpy.data.objects.new(name, mesh)
    v2_collection().objects.link(obj)
    return obj


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    return m


# ------------------------------------------------------------------ polígonos 2D

def chamfered_rect(w, l, cx, cy=None):
    """Retângulo w × l centrado na origem com cantos chanfrados (CCW, vista de cima)."""
    cy = cx if cy is None else cy
    hw, hl = w * 0.5, l * 0.5
    return [Vector((-hw + cx, -hl)), Vector((hw - cx, -hl)), Vector((hw, -hl + cy)),
            Vector((hw, hl - cy)), Vector((hw - cx, hl)), Vector((-hw + cx, hl)),
            Vector((-hw, hl - cy)), Vector((-hw, -hl + cy))]


def offset_polygon(poly, d):
    """Offset para dentro (d > 0) de um polígono convexo CCW."""
    n = len(poly)
    out = []
    for i in range(n):
        p0, p1, p2 = poly[i - 1], poly[i], poly[(i + 1) % n]
        e1 = (p1 - p0).normalized()
        e2 = (p2 - p1).normalized()
        n1 = Vector((-e1.y, e1.x))   # normal para dentro (CCW)
        n2 = Vector((-e2.y, e2.x))
        k = 1.0 + n1.dot(n2)
        out.append(p1 + (n1 + n2) * (d / max(k, 1e-4)))
    return out


# ------------------------------------------------------------------ loft de perfis

def chamfered_inset(w, l, c):
    """Offset exato de chamfered_rect(w, l, c) — não inverte os chanfros com inset grande."""
    def fn(d):
        return chamfered_rect(w - 2 * d, l - 2 * d, max(0.015, c - d * (math.sqrt(2) - 1)))
    return fn


def loft(bm, outline, profile, cap_top=True, cap_bottom=True, mat_top=0, mat_bottom=0, inset_fn=None):
    """
    Extruda `outline` (2D, CCW) por uma lista de anéis `profile` = [(inset, z, mat_da_faixa)].
    A faixa i liga o anel i ao i+1 e usa profile[i][2] como índice de material.
    `inset_fn(d)` substitui offset_polygon (ex.: chamfered_inset). Retorna (rings, bands, caps).
    """
    rings = []
    for inset, z, _ in profile:
        if inset_fn is not None:
            poly = inset_fn(inset)
        else:
            poly = offset_polygon(outline, inset) if abs(inset) > 1e-6 else outline
        rings.append([bm.verts.new((p.x, p.y, z)) for p in poly])
    bands = []
    n = len(outline)
    for r in range(len(rings) - 1):
        a, b = rings[r], rings[r + 1]
        faces = []
        for i in range(n):
            j = (i + 1) % n
            # anel r é "de dentro/cima"; a ordem garante normais para fora
            f = bm.faces.new((a[i], b[i], b[j], a[j]))
            f.material_index = profile[r][2]
            faces.append(f)
        bands.append(faces)
    caps = {}
    if cap_top:
        f = bm.faces.new(rings[0])
        f.material_index = mat_top
        caps["top"] = f
    if cap_bottom:
        f = bm.faces.new(list(reversed(rings[-1])))
        f.material_index = mat_bottom
        caps["bottom"] = f
    return rings, bands, caps


def finish_mesh(bm, name, flat=True):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = not flat
    return me


def shade_auto(obj, angle_deg=35.0):
    """Normais suaves só abaixo do ângulo (bordas duras acima) — aplica no mesh."""
    me = obj.data
    for p in me.polygons:
        p.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(me)
    lim = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            e.smooth = e.calc_face_angle(0) < lim
        else:
            e.smooth = False
    bm.to_mesh(me)
    bm.free()


# ------------------------------------------------------------------ vertex color (AO)

AO_ATTR = "AO"


def ensure_ao_attr(obj, value=1.0):
    me = obj.data
    attr = me.color_attributes.get(AO_ATTR)
    if attr is None:
        attr = me.color_attributes.new(AO_ATTR, 'BYTE_COLOR', 'CORNER')
    for d in attr.data:
        d.color = (value, value, value, 1.0)
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = me.color_attributes.find(AO_ATTR)
    return attr


def compute_ao(obj, rays=96, distance=0.5, inset=0.12, extra=None, seed=7):
    """
    AO por canto de face via ray-cast (BVHTree) no espaço local — determinístico e sem
    autointerseção nas arestas duras (o bake de vertex color do Cycles zera esses cantos).
    O ponto de amostra recua `inset` do canto rumo ao centro da face. `extra` = meshes
    (BVHTree) adicionais que ocluem (ex.: o deck do trilho sob um hazard).
    """
    import random
    from mathutils.bvhtree import BVHTree
    rnd = random.Random(seed)
    dirs = []
    for _ in range(rays):  # hemisfério cosseno em Z+, rotacionado por face
        u, v = rnd.random(), rnd.random()
        r, th = math.sqrt(u), 2 * math.pi * v
        dirs.append(Vector((r * math.cos(th), r * math.sin(th), math.sqrt(max(0.0, 1 - u)))))
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    trees = [tree] + list(extra or [])
    attr = ensure_ao_attr(obj)
    up = Vector((0, 0, 1))
    for p in me.polygons:
        n = p.normal
        rot = up.rotation_difference(n).to_matrix()
        c = p.center
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            o = co.lerp(c, inset) + n * 1e-3
            hit = 0.0
            for d in dirs:
                wd = rot @ d
                for t in trees:
                    loc, _, _, dist = t.ray_cast(o, wd, distance)
                    if loc is not None:
                        hit += 1.0 - (dist / distance) ** 2
                        break
            ao = 1.0 - hit / rays
            attr.data[li].color = (ao, ao, ao, 1.0)
    return attr


def bake_ao(objs, samples=64, distance=0.6):
    """Assa AO (Cycles) no atributo de cor 'AO' (domínio corner) de cada objeto."""
    sc = bpy.context.scene
    prev_engine = sc.render.engine
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.device = 'CPU'
    sc.render.bake.target = 'VERTEX_COLORS'
    sc.world = sc.world or bpy.data.worlds.new("World")
    sc.world.light_settings.distance = distance
    # Cada objeto assa sozinho (V1 e vizinhos não podem ocluir).
    render_state = {x: x.hide_render for x in sc.objects}
    for o in objs:
        for x in sc.objects:
            x.hide_render = x is not o
        ensure_ao_attr(o)
        # Cycles exige um material com nodes p/ assar.
        if not o.material_slots:
            o.data.materials.append(material("AO_Bake"))
        for s in o.material_slots:
            if s.material and not s.material.use_nodes:
                s.material.use_nodes = True
        bpy.ops.object.select_all(action='DESELECT')
        o.hide_set(False)
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.bake(type='AO')
    for x, h in render_state.items():
        x.hide_render = h
    sc.render.engine = prev_engine


def tint_faces(obj, predicate, value):
    """Sobrescreve a cor AO das faces onde predicate(poly) é True (ex.: faixas emissivas = 1)."""
    me = obj.data
    attr = me.color_attributes[AO_ATTR]
    for p in me.polygons:
        if predicate(p):
            for li in p.loop_indices:
                attr.data[li].color = (value, value, value, 1.0)


def remap_ao(obj, lo=0.35, gamma=1.0, height_fn=None):
    """Remapeia AO cru [0,1] → [lo,1] e multiplica por um gradiente opcional de altura."""
    me = obj.data
    attr = me.color_attributes[AO_ATTR]
    for p in me.polygons:
        for li in p.loop_indices:
            c = attr.data[li].color[0]
            c = lo + (1.0 - lo) * (c ** gamma)
            if height_fn is not None:
                vz = me.vertices[me.loops[li].vertex_index].co.z
                c *= height_fn(vz)
            attr.data[li].color = (c, c, c, 1.0)


# ------------------------------------------------------------------ export

def export_fbx(path, objs):
    """Exporta os meshes no espaço LOCAL (sem loc/rot/escala do objeto), como no V1."""
    # O nome do objeto vira o nome do mesh no Unity (o wizard procura por ele).
    saved = [(o, o.matrix_world.copy(), o.hide_get()) for o in objs]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.hide_set(False)
        o.matrix_world = Matrix.Identity(4)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
        colors_type='LINEAR', prioritize_active_color=True,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    for o, mw, hid in saved:
        o.matrix_world = mw
        o.hide_set(hid)
