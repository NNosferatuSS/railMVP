using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using RailSwitchMVP.Config;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;
using RailSwitchMVP.Visual;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Aplica a direção de arte Aurora (Docs/ArtDirection/2026-10-15-exploracao-v1)
    /// no projeto: importers dos FBX do Blender (Art/Source/RailAurora.blend),
    /// materiais, preset do conector, volume de post-processing, prefabs e cena.
    ///
    /// Idempotente: tudo que cria leva prefixo "Aurora_" e é reconstruído a cada
    /// execução; visuais antigos são DESATIVADOS (não apagados).
    ///
    /// Menu: Tools → RailSwitch → Art.
    /// Batch: -executeMethod RailSwitchMVP.EditorTools.AuroraArtSetup.BatchApplyAndPreview
    /// </summary>
    public static class AuroraArtSetup
    {
        const string Root = "Assets/Graphics/Aurora";
        const string ModelDir = Root + "/Models";
        const string MatDir = Root + "/Materials";
        const string CfgDir = Root + "/Config";
        const string ScenePath = "Assets/Scenes/RailSwitchMVP.unity";
        const string Spawnables = "Assets/Prefabs/Spawnables";
        const string RailGenConfigPath = "Assets/ScriptableObjects/RailSwitchMVP/RailGenConfig_Default.asset";
        const string DifficultyConfigPath = "Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset";
        const string PreviewDir = "Docs/ArtDirection/2026-10-15-aurora-unity";

        // Comprimentos com que os conectores foram modelados no Blender (gap + 0.4 de sobreposição).
        const float StraightMeshLength = 4.4f;
        const float DiagMeshLength = 5.4f;
        const float TrackTopY = 0.1f;

        // ================================================================ Menu

        [MenuItem("Tools/RailSwitch/Art/Aurora — Apply All", priority = 200)]
        public static void ApplyAll()
        {
            EnsureFolders();
            ConfigureImporters();
            var m = BuildMaterials();
            var preset = BuildSwitchPreset();
            var profile = BuildVolumeProfile();
            ApplyTrackTile(m, preset);
            ApplyCollectibles(m);
            ApplyPickups(m);
            ApplyScene(m, profile);
            ConfigurePipelineAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[Aurora] Apply All concluído.");
        }

        [MenuItem("Tools/RailSwitch/Art/Aurora — Render Previews (3 e 7 colunas)", priority = 201)]
        public static void RenderPreviews()
        {
            RenderPreview(3, "aurora_3col.png");
            RenderPreview(7, "aurora_7col.png");
        }

        public static void BatchApplyAndPreview()
        {
            try
            {
                ApplyAll();
                RenderPreviews();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BatchPreviewOnly()
        {
            try { RenderPreviews(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ================================================================ Cena (aditiva — não mexe na cena que o usuário tem aberta)

        static T[] AllInScene<T>(Scene s) where T : Component =>
            s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

        static T FirstInScene<T>(Scene s) where T : Component => AllInScene<T>(s).FirstOrDefault();

        static bool IsGameSceneOpen()
        {
            var s = SceneManager.GetSceneByPath(ScenePath);
            return s.IsValid() && s.isLoaded;
        }

        static Scene OpenGameScene()
        {
            var s = SceneManager.GetSceneByPath(ScenePath);
            if (s.IsValid() && s.isLoaded) return s;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        static void CloseGameScene(Scene s, bool wasOpenBefore)
        {
            if (!wasOpenBefore && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(s, true);
        }

        // ================================================================ Importers

        static void EnsureFolders()
        {
            foreach (var d in new[] { MatDir, CfgDir })
                if (!AssetDatabase.IsValidFolder(d))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace('\\', '/'), Path.GetFileName(d));
        }

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importCameras = false;
                imp.importLights = false;
                imp.bakeAxisConversion = true;
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.isReadable = false;
                imp.importNormals = ModelImporterNormals.Import;
                imp.importBlendShapes = false;
                imp.meshCompression = ModelImporterMeshCompression.Off;
                imp.SaveAndReimport();

                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                    Debug.Log($"[Aurora] mesh {mesh.name}: bounds center {mesh.bounds.center} size {mesh.bounds.size} tris {mesh.triangles.Length / 3}");
            }
        }

        static Mesh LoadMesh(string fbx, string meshName = null)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath($"{ModelDir}/{fbx}.fbx").OfType<Mesh>().ToArray();
            if (meshes.Length == 0) throw new Exception($"[Aurora] FBX sem mesh: {fbx}");
            if (meshName == null) return meshes[0];
            return meshes.FirstOrDefault(m => m.name == meshName)
                ?? throw new Exception($"[Aurora] mesh '{meshName}' não encontrada em {fbx}");
        }

        // ================================================================ Materiais

        public class Mats
        {
            public Material trackBody, trackRim, conOn, conVoid, pearl, crescent, coin, gem,
                powerRing, debuffFrame, hazard, hazardStripe, arc, veilGreen, veilViolet, sky,
                trail, burst, flash, ring, mote, bubble, magnetRing;
        }

        /// <summary>
        /// Cor hex (sRGB) × intensidade aplicada em espaço LINEAR, devolvida codificada
        /// em gamma — é assim que o Material guarda propriedades Color (o Unity
        /// lineariza na GPU em projetos Linear). Ex.: Lin("#6dffc4", 2) = o dobro de
        /// energia da cor, sem deslocar o matiz.
        /// </summary>
        static Color Lin(string hex, float intensity = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                return new Color(c.r * intensity, c.g * intensity, c.b * intensity, 1f);
            var l = c.linear;
            return new Color(Mathf.LinearToGammaSpace(l.r * intensity),
                             Mathf.LinearToGammaSpace(l.g * intensity),
                             Mathf.LinearToGammaSpace(l.b * intensity), 1f);
        }

        static Material Mat(string name, string shader, Action<Material> setup)
        {
            var path = $"{MatDir}/{name}.mat";
            var sh = Shader.Find(shader) ?? throw new Exception($"[Aurora] shader não encontrado: {shader}");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            setup(m);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Valores já em gamma (ver Lin); SetVector grava sem tocar.
        static void C(Material m, string prop, Color c) => m.SetVector(prop, new Vector4(c.r, c.g, c.b, c.a));

        static Material Surface(string name, string lit, string shade, string emit = null, float emitI = 0f,
            string rim = null, float rimI = 0f, float rimPow = 3f, float softness = 0.35f, float fog = 1f,
            float playerGlow = 0f)
        {
            return Mat(name, "Rail/Aurora/Surface", m =>
            {
                C(m, "_BaseColor", Lin(lit));
                C(m, "_ShadeColor", Lin(shade));
                C(m, "_EmissionColor", emit != null ? Lin(emit, emitI) : Color.black);
                C(m, "_RimColor", rim != null ? Lin(rim, rimI) : Color.black);
                m.SetFloat("_RimPower", rimPow);
                m.SetFloat("_ShadeSoftness", softness);
                m.SetFloat("_FogAmount", fog);
                m.SetFloat("_PlayerGlow", playerGlow);
                m.SetFloat("_Flash", 0f);
            });
        }

        static Mats BuildMaterials()
        {
            var r = new Mats
            {
                trackBody = Surface("M_Aurora_TrackBody", "#226f7c", "#0b2742", rim: "#2fc8e8", rimI: 0.12f, rimPow: 4f, playerGlow: 1f),
                trackRim = Surface("M_Aurora_TrackRim", "#000000", "#000000", "#46e0ff", 0.7f, playerGlow: 2.2f),
                conOn = Surface("M_Aurora_ConnectorOn", "#0c3328", "#06201a", "#2effa0", 1.1f, fog: 0.4f),
                conVoid = Surface("M_Aurora_ConnectorVoid", "#3a0a14", "#22060c", "#ff2d55", 1.0f, fog: 0.4f),
                pearl = Surface("M_Aurora_Pearl", "#e2eefa", "#7f9fc8", "#ffffff", 0.03f, "#7fe8ff", 0.45f, 3f, 0.6f, 0f),
                crescent = Surface("M_Aurora_Crescent", "#0a3a50", "#06263a", "#3fd8ff", 1.8f, fog: 0f),
                coin = Surface("M_Aurora_Coin", "#ffc23d", "#c0650e", "#ffa21a", 0.45f, fog: 0.5f),
                gem = Surface("M_Aurora_Gem", "#d9a6ff", "#6a3db8", "#b070ff", 0.6f, fog: 0.5f),
                powerRing = Surface("M_Aurora_PowerRing", "#1a4a58", "#103040", "#8ff6ff", 1.3f, fog: 0.4f),
                debuffFrame = Surface("M_Aurora_DebuffFrame", "#4a1040", "#300a2a", "#ff5fd2", 1.3f, fog: 0.4f),
                hazard = Surface("M_Aurora_Hazard", "#ff5a66", "#9a1a38", "#ff2040", 0.12f, fog: 0.5f),
                hazardStripe = Surface("M_Aurora_HazardStripe", "#ffe6dc", "#ffe6dc", "#ffd8c8", 0.25f, fog: 0.5f),
                arc = Surface("M_Aurora_Arc", "#1a4a80", "#0a1838", rim: "#3a6ad8", rimI: 0.25f, fog: 0.35f),
                veilGreen = Mat("M_Aurora_VeilGreen", "Rail/Aurora/Veil", m =>
                {
                    C(m, "_ColorBottom", Lin("#2dffa0", 1.1f));
                    C(m, "_ColorTop", Lin("#3a4dff", 0.55f));
                    m.SetFloat("_Intensity", 0.75f);
                    m.SetFloat("_RayDensity", 42f);
                    m.SetFloat("_ScrollSpeed", 0.22f);
                    m.SetFloat("_Falloff", 1.6f);
                }),
                veilViolet = Mat("M_Aurora_VeilViolet", "Rail/Aurora/Veil", m =>
                {
                    C(m, "_ColorBottom", Lin("#22e8c0", 0.9f));
                    C(m, "_ColorTop", Lin("#9a4dff", 0.8f));
                    m.SetFloat("_Intensity", 0.6f);
                    m.SetFloat("_RayDensity", 30f);
                    m.SetFloat("_ScrollSpeed", 0.17f);
                    m.SetFloat("_Falloff", 1.3f);
                }),
                sky = Mat("M_Aurora_Sky", "Rail/Aurora/Sky", m =>
                {
                    C(m, "_Deep", Lin("#020617"));
                    C(m, "_Mid", Lin("#0a2150"));
                    C(m, "_Top", Lin("#120c34"));
                    m.SetFloat("_MidHeight", -0.15f);
                    m.SetFloat("_Spread", 0.75f);
                }),
                trail = Mat("M_Aurora_Trail", "Rail/Aurora/Additive", m =>
                {
                    C(m, "_TintColor", Lin("#3fd8ff", 1.8f));
                    m.SetFloat("_Shape", 0f);
                    m.SetFloat("_Softness", 0.9f);
                    m.SetFloat("_Core", 1.2f);
                }),
                burst = Mat("M_Aurora_Burst", "Rail/Aurora/Additive", m =>
                {
                    C(m, "_TintColor", Color.white);
                    m.SetFloat("_Shape", 2f);
                    m.SetFloat("_Softness", 0.35f);
                    m.SetFloat("_Core", 1f);
                }),
                flash = Mat("M_Aurora_Flash", "Rail/Aurora/Additive", m =>
                {
                    C(m, "_TintColor", new Color(0.6f, 0.6f, 0.6f, 1f));
                    m.SetFloat("_Shape", 1f);
                    m.SetFloat("_Softness", 1f);
                    m.SetFloat("_Core", 0.8f);
                }),
                ring = Mat("M_Aurora_Ring", "Rail/Aurora/Additive", m =>
                {
                    C(m, "_TintColor", Color.white);
                    m.SetFloat("_Shape", 3f);
                    m.SetFloat("_Softness", 0.6f);
                    m.SetFloat("_Core", 1.5f);
                }),
                mote = Mat("M_Aurora_Mote", "Rail/Aurora/Additive", m =>
                {
                    C(m, "_TintColor", Color.white);
                    m.SetFloat("_Shape", 1f);
                    m.SetFloat("_Softness", 0.8f);
                    m.SetFloat("_Core", 1f);
                }),
                bubble = Mat("M_Aurora_Bubble", "Rail/Aurora/Fresnel", m =>
                {
                    C(m, "_Color", Lin("#5fdcff", 1.3f));
                    m.SetFloat("_Power", 3.2f);
                    m.SetFloat("_Core", 0.05f);
                    m.SetFloat("_Pulse", 5f);
                    m.SetFloat("_Bands", 9f);
                }),
                magnetRing = Surface("M_Aurora_MagnetRing", "#3a2a08", "#201804", "#ffc23d", 1.6f, fog: 0f),
            };
            AssetDatabase.SaveAssets();
            return r;
        }

        // ================================================================ Preset do conector

        static SwitchVisualConfig BuildSwitchPreset()
        {
            var path = $"{CfgDir}/SwitchVisual_Aurora.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<SwitchVisualConfig>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<SwitchVisualConfig>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            cfg.animMode = SwitchVisualConfig.AnimMode.ExtendZ;
            cfg.colorOnlyConnected = false;
            cfg.sequenceLowerThenRaise = false;
            cfg.raiseDuration = 0.14f;
            cfg.lowerDuration = 0.08f;
            cfg.raiseDelay = 0.02f;
            cfg.raiseEase = SwitchVisualConfig.EasePreset.PopBack;
            cfg.lowerEase = SwitchVisualConfig.EasePreset.EaseOut;
            cfg.overshoot = 1.2f;
            EditorUtility.SetDirty(cfg);

            var rail = AssetDatabase.LoadAssetAtPath<RailGenConfig>(RailGenConfigPath);
            if (rail != null)
            {
                rail.switchVisualConfig = cfg;
                EditorUtility.SetDirty(rail);
            }
            return cfg;
        }

        // ================================================================ Volume

        static VolumeProfile BuildVolumeProfile()
        {
            var path = $"{CfgDir}/Aurora_VolumeProfile.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }

            T Get<T>() where T : VolumeComponent
            {
                if (!p.TryGet<T>(out var c))
                {
                    c = p.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, p);
                }
                c.active = true;
                return c;
            }

            var bloom = Get<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(2.1f);
            bloom.scatter.Override(0.74f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(false);

            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.None);

            var ca = Get<ColorAdjustments>();
            ca.postExposure.Override(0f);
            ca.contrast.Override(4f);
            ca.saturation.Override(12f);

            var vig = Get<Vignette>();
            vig.color.Override(new Color(0.01f, 0.03f, 0.09f));
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);

            var chroma = Get<ChromaticAberration>();
            chroma.intensity.Override(0.06f);

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>Perfis dos pulsos de tela (peso animado pelo AuroraJuice).</summary>
        static (VolumeProfile flash, VolumeProfile hit) BuildPulseProfiles()
        {
            VolumeProfile Profile(string file)
            {
                var path = $"{CfgDir}/{file}.asset";
                var pr = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (pr == null)
                {
                    pr = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(pr, path);
                }
                return pr;
            }
            T Get<T>(VolumeProfile pr) where T : VolumeComponent
            {
                if (!pr.TryGet<T>(out var c))
                {
                    c = pr.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, pr);
                }
                c.active = true;
                return c;
            }

            var flash = Profile("Aurora_PulseFlash");
            var fb = Get<Bloom>(flash);
            fb.intensity.Override(4.5f);
            fb.threshold.Override(0.6f);
            var fc = Get<ColorAdjustments>(flash);
            fc.postExposure.Override(0.5f);
            fc.saturation.Override(25f);
            Get<ChromaticAberration>(flash).intensity.Override(0.35f);
            EditorUtility.SetDirty(flash);

            var hit = Profile("Aurora_PulseHit");
            Get<ChromaticAberration>(hit).intensity.Override(1f);
            var hv = Get<Vignette>(hit);
            hv.color.Override(new Color(0.45f, 0.02f, 0.1f));
            hv.intensity.Override(0.55f);
            hv.smoothness.Override(0.5f);
            var hc = Get<ColorAdjustments>(hit);
            hc.colorFilter.Override(new Color(1f, 0.72f, 0.78f));
            hc.saturation.Override(-15f);
            var hl = Get<LensDistortion>(hit);
            hl.intensity.Override(-0.22f);
            EditorUtility.SetDirty(hit);

            AssetDatabase.SaveAssets();
            return (flash, hit);
        }

        // ================================================================ Helpers de hierarquia

        static void DestroyChildren(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == name)
                    UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static GameObject MeshChild(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Aurora] campo '{field}' não existe em {target.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 InverseScale(Transform t, float k = 1f)
        {
            var s = t.lossyScale;
            return new Vector3(k / Mathf.Max(1e-4f, s.x), k / Mathf.Max(1e-4f, s.y), k / Mathf.Max(1e-4f, s.z));
        }

        /// <summary>
        /// Os spawners instanciam com Quaternion.identity (a rotação salva no root do
        /// prefab é ignorada em jogo). Então o filho só precisa desfazer a ESCALA local
        /// do root pra ficar com escala uniforme k no mundo.
        /// </summary>
        static void NeutralizeParent(Transform child, Transform root, float k)
        {
            child.localRotation = Quaternion.identity;
            var s0 = root.localScale;
            child.localScale = new Vector3(k / Mathf.Max(1e-4f, Mathf.Abs(s0.x)),
                                           k / Mathf.Max(1e-4f, Mathf.Abs(s0.y)),
                                           k / Mathf.Max(1e-4f, Mathf.Abs(s0.z)));
        }

        static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================ TrackTile

        static void ApplyTrackTile(Mats m, SwitchVisualConfig preset)
        {
            var rail = AssetDatabase.LoadAssetAtPath<RailGenConfig>(RailGenConfigPath);
            var trackMesh = LoadMesh("Aurora_Track");
            var straight = LoadMesh("Aurora_ConnectorStraight");
            var diag = LoadMesh("Aurora_ConnectorDiag");

            EditPrefab($"{Spawnables}/TrackTile_Prefab.prefab", root =>
            {
                var t = root.transform;
                var start = t.Find("StartPoint");
                var end = t.Find("EndPoint");
                var sw = t.Find("Switch");

                // Visuais antigos: desativados (não apagados).
                foreach (Transform c in t)
                    if (c != start && c != end && c != sw && !c.name.StartsWith("Aurora_"))
                        c.gameObject.SetActive(false);
                foreach (Transform c in sw)
                    if (!c.name.StartsWith("Aurora_"))
                        c.gameObject.SetActive(false);

                // Trilho: mesh modelado de z=-5 a z=+5 (StartPoint/EndPoint).
                DestroyChildren(t, "Aurora_Track");
                float tileLen = end.localPosition.z - start.localPosition.z;
                var track = MeshChild(t, "Aurora_Track", trackMesh, m.trackBody, m.trackRim);
                track.transform.localPosition = new Vector3(0f, 0f, (start.localPosition.z + end.localPosition.z) * 0.5f);
                track.transform.localScale = new Vector3(1f, 1f, tileLen / 10f);

                // Conectores: pivot no EndPoint. Cada slot (que o SwitchVisual estende em Z)
                // tem um filho "Mesh" com a escala que encaixa o comprimento real do gap.
                DestroyChildren(sw, "Aurora_Connectors");
                var cons = new GameObject("Aurora_Connectors").transform;
                cons.SetParent(sw, false);
                cons.localPosition = sw.InverseTransformPoint(end.position);

                float gapZ = (rail.trackLength + rail.rowGap) - tileLen;
                float lane = rail.laneSpacing;
                float diagLen = Mathf.Sqrt(gapZ * gapZ + lane * lane);
                float yaw = Mathf.Atan2(lane, gapZ) * Mathf.Rad2Deg;

                Transform Slot(string name, float slotYaw, Mesh mesh, float meshLen, float wantLen)
                {
                    var slot = new GameObject(name).transform;
                    slot.SetParent(cons, false);
                    slot.localRotation = Quaternion.Euler(0f, slotYaw, 0f);
                    var mesh0 = MeshChild(slot, "Mesh", mesh, m.conOn);
                    mesh0.transform.localScale = new Vector3(1f, 1f, (wantLen + 0.4f) / meshLen);
                    return slot;
                }

                var left = Slot("Left", -yaw, diag, DiagMeshLength, diagLen);
                var mid = Slot("Middle", 0f, straight, StraightMeshLength, gapZ);
                var right = Slot("Right", yaw, diag, DiagMeshLength, diagLen);

                var sv = sw.GetComponent<SwitchVisual>() ?? sw.gameObject.AddComponent<SwitchVisual>();
                SetRef(sv, "leftMesh", left);
                SetRef(sv, "middleMesh", mid);
                SetRef(sv, "rightMesh", right);
                SetRef(sv, "config", preset);

                var sc = sw.GetComponent<SwitchController>();
                if (sc != null) SetRef(sc, "visual", sv);

                var tile = root.GetComponent<TrackTile>();
                SetRef(tile, "connectivityRenderer", mid.GetComponentInChildren<MeshRenderer>());
                SetRef(tile, "connectedMaterial", m.conOn);
                SetRef(tile, "disconnectedMaterial", m.conVoid);

                // Estado inicial do prefab: Middle estendido, laterais recolhidos.
                left.localScale = new Vector3(1, 1, 0);
                right.localScale = new Vector3(1, 1, 0);
                left.GetComponentInChildren<MeshRenderer>().enabled = false;
                right.GetComponentInChildren<MeshRenderer>().enabled = false;

                // Juice: montagem/desmontagem visual + flash ao trocar direção.
                var tj = root.GetComponent<AuroraTileJuice>() ?? root.AddComponent<AuroraTileJuice>();
                var tjso = new SerializedObject(tj);
                var vis = tjso.FindProperty("visuals");
                vis.arraySize = 2;
                vis.GetArrayElementAtIndex(0).objectReferenceValue = track.transform;
                vis.GetArrayElementAtIndex(1).objectReferenceValue = cons;
                tjso.ApplyModifiedPropertiesWithoutUndo();

                var sj = sw.GetComponent<AuroraSwitchJuice>() ?? sw.gameObject.AddComponent<AuroraSwitchJuice>();
                SetRef(sj, "controller", sc);
                SetRef(sj, "visual", sv);

                Debug.Log($"[Aurora] TrackTile: tileLen {tileLen}, gap {gapZ}, diag {diagLen:0.00} @ {yaw:0.0}°");
            });
        }

        // ================================================================ Coin / Gem

        static void ApplyCollectibles(Mats m)
        {
            var coinMesh = LoadMesh("Aurora_Coin");
            var gemMesh = LoadMesh("Aurora_Gem");

            EditPrefab($"{Spawnables}/Resource_CoinPrefab.prefab", root =>
            {
                foreach (var r in root.GetComponents<Renderer>()) r.enabled = false;
                DestroyChildren(root.transform, "Aurora_Visual");
                var v = MeshChild(root.transform, "Aurora_Visual", coinMesh, m.coin);
                NeutralizeParent(v.transform, root.transform, 1.2f);
                var mo = v.AddComponent<AuroraMotion>();
                mo.bobAmplitude = 0f; mo.pulseAmount = 0.06f; mo.pulseSpeed = 5f;
            });

            EditPrefab($"{Spawnables}/Resource_Gem.prefab", root =>
            {
                foreach (var r in root.GetComponents<Renderer>()) r.enabled = false;
                DestroyChildren(root.transform, "Aurora_Visual");
                var v = MeshChild(root.transform, "Aurora_Visual", gemMesh, m.gem);
                NeutralizeParent(v.transform, root.transform, 1.35f);
                var mo = v.AddComponent<AuroraMotion>();
                mo.bobAmplitude = 0f; mo.pulseAmount = 0.08f; mo.pulseSpeed = 4f;
            });
        }

        // ================================================================ Power-ups / hazards / debuffs

        static Vector2 SpriteSize(SpriteRenderer sr)
        {
            if (sr.drawMode != SpriteDrawMode.Simple) return sr.size;
            return sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        }

        static void ApplyPickups(Mats m)
        {
            var ring = LoadMesh("Aurora_PowerUpRing");
            var frame = LoadMesh("Aurora_DebuffFrame");
            var lethal = LoadMesh("Aurora_HazardLethal");
            var barrier = LoadMesh("Aurora_HazardBarrier");
            var gemMesh = LoadMesh("Aurora_Gem");

            foreach (var path in Directory.GetFiles(Spawnables, "*.prefab").Select(p => p.Replace('\\', '/')))
            {
                var file = Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("_") || file.StartsWith("TrackTile") || file.StartsWith("Resource_")) continue;

                bool isPower = file.StartsWith("PowerUp");
                bool isHazard = file.StartsWith("HazardVariant");
                bool isDebuff = file.StartsWith("DebuffVariant");
                if (!isPower && !isHazard && !isDebuff) continue;

                EditPrefab(path, root =>
                {
                    var t = root.transform;
                    var visual = t.Find("Visual");
                    var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;

                    // limpa execuções anteriores
                    DestroyChildren(t, "Aurora_Visual");
                    if (visual != null) DestroyChildren(visual, "Aurora_Visual");

                    if (isHazard)
                    {
                        if (sr != null) sr.enabled = false;
                        bool isBarrier = file.Contains("Barrier");
                        var v = MeshChild(t, "Aurora_Visual", isBarrier ? barrier : lethal, m.hazard, m.hazardStripe);
                        v.transform.localScale = InverseScale(t);
                        v.AddComponent<AuroraGroundSnap>().surfaceY = TrackTopY;
                        return;
                    }

                    if (sr != null)
                    {
                        // Ícone (sprite billboard) dentro do anel / moldura. Filho do Visual
                        // → herda o billboard e a escala do sprite.
                        var size = SpriteSize(sr);
                        float s = Mathf.Max(size.x, size.y);
                        var v = isDebuff
                            ? MeshChild(visual, "Aurora_Visual", frame, m.debuffFrame)
                            : MeshChild(visual, "Aurora_Visual", ring, m.powerRing);
                        v.transform.localPosition = new Vector3(0f, isDebuff ? 0.06f * s : 0f, 0.02f);
                        v.transform.localScale = Vector3.one * (isDebuff ? s * 1.05f : s * 1.05f);
                        var mo = v.AddComponent<AuroraMotion>();
                        mo.bobAmplitude = 0f; mo.pulseAmount = 0.05f; mo.pulseSpeed = 5f;
                        return;
                    }

                    // Legado (mesh no root, sem sprite): anel + cristal da cor original.
                    var rootR = root.GetComponent<MeshRenderer>();
                    Color tint = Color.cyan;
                    if (rootR != null && rootR.sharedMaterial != null)
                    {
                        var om = rootR.sharedMaterial;
                        tint = om.HasProperty("_BaseColor") ? om.GetColor("_BaseColor") : om.color;
                        rootR.enabled = false;
                    }
                    var hex = "#" + ColorUtility.ToHtmlStringRGB(tint);
                    var core = Surface($"M_Aurora_PowerCore_{file}", hex, "#1a3050", hex, 1.1f, fog: 0.4f);

                    var holder = new GameObject("Aurora_Visual").transform;
                    holder.SetParent(t, false);
                    holder.localScale = InverseScale(t);
                    var hm = holder.gameObject.AddComponent<AuroraMotion>();
                    hm.faceCamera = true; hm.bobAmplitude = 0.06f; hm.bobSpeed = 2.6f;
                    var rg = MeshChild(holder, "Ring", ring, m.powerRing);
                    rg.transform.localScale = Vector3.one * 0.9f;
                    var cr = MeshChild(holder, "Core", gemMesh, core);
                    cr.transform.localScale = Vector3.one * 0.9f;
                    var cm = cr.AddComponent<AuroraMotion>();
                    cm.bobAmplitude = 0f; cm.spinSpeed = 140f; cm.spinAxis = Vector3.up;
                });
            }
        }

        // ================================================================ Cena

        static void ApplyScene(Mats m, VolumeProfile profile)
        {
            bool wasOpen = IsGameSceneOpen();
            var scene = OpenGameScene();

            // ---- Player
            var rider = FirstInScene<PlayerRailRider>(scene);
            if (rider != null)
            {
                var pt = rider.transform;
                foreach (var r in pt.GetComponents<Renderer>()) r.enabled = false;
                DestroyChildren(pt, "Aurora_PlayerVisual");

                var vis = new GameObject("Aurora_PlayerVisual").transform;
                vis.SetParent(pt, false);
                vis.localScale = InverseScale(pt);
                // A altura do root do Player muda em jogo (não é a da cena): o visual se
                // prende à superfície. Pérola com centro em y=0.45 (raio 0.3, topo da pista 0.1).
                var snap = vis.gameObject.AddComponent<AuroraGroundSnap>();
                snap.surfaceY = 0.45f;
                snap.continuous = true;

                var body = new GameObject("Body").transform;
                body.SetParent(vis, false);

                var pearl = MeshChild(body, "Pearl", LoadMesh("Aurora_Player", "Player_Pearl"), m.pearl);
                var cres = MeshChild(body, "Crescent", LoadMesh("Aurora_Player", "Player_Crescent"), m.crescent);
                var cm = cres.AddComponent<AuroraMotion>();
                cm.bobAmplitude = 0f; cm.wobbleAxis = Vector3.up; cm.wobbleDegrees = 10f; cm.wobbleSpeed = 2.2f;

                var trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(vis, false);
                var tr = trailGo.AddComponent<TrailRenderer>();
                tr.time = 0.24f;
                tr.minVertexDistance = 0.06f;
                tr.widthMultiplier = 0.6f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(0.6f, 0.95f, 1f), 0), new GradientColorKey(new Color(0.2f, 0.6f, 1f), 1) },
                          new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0f, 1) });
                tr.colorGradient = g;
                tr.numCapVertices = 2;
                tr.alignment = LineAlignment.View;
                tr.sharedMaterial = m.trail;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.receiveShadows = false;

                // Auras de power-up
                var bubble = MeshChild(body, "ShieldBubble", LoadMesh("Aurora_Player", "Player_Pearl"), m.bubble);
                bubble.transform.localScale = Vector3.one * 2.0f;
                var magnet = MeshChild(vis, "MagnetRing", LoadMesh("Aurora_PowerUpRing"), m.magnetRing);
                magnet.transform.localScale = Vector3.one * 1.5f;

                var pj = vis.gameObject.AddComponent<AuroraPlayerJuice>();
                SetRef(pj, "rider", rider);
                SetRef(pj, "body", body);
                SetRef(pj, "pearl", pearl.GetComponent<MeshRenderer>());
                SetRef(pj, "crescent", cres.GetComponent<MeshRenderer>());
                SetRef(pj, "trail", tr);
                SetRef(pj, "bubble", bubble.GetComponent<MeshRenderer>());
                SetRef(pj, "magnetRing", magnet.transform);
                SetRef(pj, "sparkMaterial", m.burst);

                var applier = pt.GetComponentInChildren<PlayerCharacterApplier>(true);
                // Cor do personagem tinge o crescente; a pérola fica sempre clara (legível sobre o trilho teal).
                if (applier != null) SetRef(applier, "targetRenderer", cres.GetComponent<MeshRenderer>());
            }
            else Debug.LogWarning("[Aurora] PlayerRailRider não encontrado na cena.");

            // ---- Câmera
            var rig = FirstInScene<PlayerCameraRig>(scene);
            var cam = rig != null ? rig.GetComponent<Camera>() : FirstInScene<Camera>(scene);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.allowHDR = true;   // sem HDR tudo clampa em 1 antes do bloom
                cam.allowMSAA = true;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(cam);
            }

            // RenderSettings são por cena (a ativa): ativa a do jogo durante a escrita.
            var prevActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = m.sky;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.24f, 0.36f);
            SceneManager.SetActiveScene(prevActive);

            // ---- Luz principal (só direção importa pro shader Surface)
            foreach (var l in AllInScene<Light>(scene))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
                l.color = new Color(0.85f, 0.95f, 1f);
                l.shadows = LightShadows.None;
                EditorUtility.SetDirty(l);
            }

            // ---- Volume global
            var vol = AllInScene<Volume>(scene).FirstOrDefault(v => v.isGlobal);
            if (vol == null)
            {
                vol = new GameObject("Global Volume").AddComponent<Volume>();
                SceneManager.MoveGameObjectToScene(vol.gameObject, scene);
            }
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            EditorUtility.SetDirty(vol);

            // ---- Ambiente (fundo + VFX)
            foreach (var old in AllInScene<AuroraEnvironment>(scene))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var envGo = BuildEnvironment(m, cam != null ? cam.transform : null, rider);
            SceneManager.MoveGameObjectToScene(envGo, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CloseGameScene(scene, wasOpen);
        }

        static GameObject BuildEnvironment(Mats m, Transform cam, PlayerRailRider playerRider)
        {
            var env = new GameObject("Aurora_Environment");
            var follow = new GameObject("Follow").transform; follow.SetParent(env.transform, false);
            var passing = new GameObject("Passing").transform; passing.SetParent(env.transform, false);

            var veil = LoadMesh("Aurora_BGVeil");
            var arc = LoadMesh("Aurora_BGArc");

            // Véus: cortinas abaixo e dos lados das pistas. Mesh: 60 de comprimento em Z,
            // curva S em X (±8), 22 de altura a partir de y=0.
            void Veil(string n, Vector3 pos, float yaw, float scale, Material mat)
            {
                var v = MeshChild(follow, n, veil, mat);
                v.transform.localPosition = pos;
                v.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                v.transform.localScale = Vector3.one * scale;
            }
            Veil("Veil_L0", new Vector3(-17f, -22f, 34f), -8f, 0.75f, m.veilGreen);
            Veil("Veil_R0", new Vector3(18f, -24f, 44f), 172f, 0.8f, m.veilViolet);
            Veil("Veil_L1", new Vector3(-13f, -30f, 58f), 0f, 1.0f, m.veilGreen);
            Veil("Veil_R1", new Vector3(14f, -34f, 70f), 180f, 1.1f, m.veilViolet);
            Veil("Veil_L2", new Vector3(-24f, -52f, 105f), 12f, 1.6f, m.veilViolet);
            Veil("Veil_R2", new Vector3(26f, -48f, 95f), 168f, 1.5f, m.veilGreen);

            // Arcos: aros gigantes escuros cruzando por baixo das pistas, reciclados
            // pra frente — passam pela tela e dão leitura de velocidade.
            const float spacing = 48f;
            for (int i = 0; i < 5; i++)
            {
                var a = MeshChild(passing, $"Arc_{i}", arc, m.arc);
                float side = (i % 2 == 0) ? 1f : -1f;
                // Arco no plano YZ: um "portal" lateral que o player passa ao lado.
                a.transform.localPosition = new Vector3(side * (19f + (i % 3) * 3f), -34f - (i % 3) * 5f, i * spacing);
                a.transform.localRotation = Quaternion.Euler(0f, 90f + side * 12f, side * -10f);
                a.transform.localScale = Vector3.one * (0.9f + (i % 3) * 0.12f);
            }

            var e = env.AddComponent<AuroraEnvironment>();
            var so = new SerializedObject(e);
            so.FindProperty("fogColor").colorValue = new Color(0.03f, 0.08f, 0.2f, 1f);
            so.FindProperty("fogStart").floatValue = 35f;
            so.FindProperty("fogEnd").floatValue = 130f;
            so.FindProperty("heightFogTop").floatValue = -4f;
            so.FindProperty("heightFogBottom").floatValue = -60f;
            so.FindProperty("cameraTarget").objectReferenceValue = cam;
            so.FindProperty("followRoot").objectReferenceValue = follow;
            so.FindProperty("passingRoot").objectReferenceValue = passing;
            so.FindProperty("passingSpacing").floatValue = spacing;
            so.FindProperty("recycleBehind").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fx = env.AddComponent<AuroraFX>();
            var fso = new SerializedObject(fx);
            fso.FindProperty("burstMaterial").objectReferenceValue = m.burst;
            fso.FindProperty("flashMaterial").objectReferenceValue = m.flash;
            fso.FindProperty("ringMaterial").objectReferenceValue = m.ring;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // Pulsos de tela: dois Volumes globais com peso 0 (o AuroraJuice anima).
            var (flashProfile, hitProfile) = BuildPulseProfiles();
            Volume PulseVolume(string n, VolumeProfile pr, float priority)
            {
                var go = new GameObject(n);
                go.transform.SetParent(env.transform, false);
                var v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.weight = 0f;
                v.priority = priority;
                v.sharedProfile = pr;
                return v;
            }
            var flashVol = PulseVolume("PulseFlash", flashProfile, 10f);
            var hitVol = PulseVolume("PulseHit", hitProfile, 11f);

            var juice = env.AddComponent<AuroraJuice>();
            var jso = new SerializedObject(juice);
            jso.FindProperty("player").objectReferenceValue = playerRider;
            jso.FindProperty("fx").objectReferenceValue = fx;
            jso.FindProperty("playerJuice").objectReferenceValue =
                playerRider != null ? playerRider.GetComponentInChildren<AuroraPlayerJuice>(true) : null;
            jso.FindProperty("flashVolume").objectReferenceValue = flashVol;
            jso.FindProperty("hitVolume").objectReferenceValue = hitVol;
            jso.FindProperty("speedParticleMaterial").objectReferenceValue = m.mote;
            var veilRs = follow.GetComponentsInChildren<MeshRenderer>();
            var vp = jso.FindProperty("veils");
            vp.arraySize = veilRs.Length;
            for (int i = 0; i < veilRs.Length; i++) vp.GetArrayElementAtIndex(i).objectReferenceValue = veilRs[i];
            jso.ApplyModifiedPropertiesWithoutUndo();
            return env;
        }

        // ================================================================ URP assets

        static void ConfigurePipelineAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null) continue;
                a.supportsHDR = true;
                a.msaaSampleCount = 4;
                EditorUtility.SetDirty(a);
                Debug.Log($"[Aurora] URP asset {a.name}: HDR on, MSAA 4x");
            }
        }

        // ================================================================ Preview

        static readonly string[] Layout3 =
        {
            "XXX", "XXX", "XX.", ".XX", "XXX", "X.X", "XX.", ".XX", "XXX", "X.X", "XXX", ".XX",
        };
        static readonly string[] Layout7 =
        {
            ".XXXXX.", "XXXXXXX", "XX.XX.X", ".XXX.XX", "X.XXX.X", "XX.X.XX", ".XXXXX.", "XX.X.XX", "X.XXX.X", "XXX.XXX", ".XX.XX.", "XXXXXXX",
        };

        static void RenderPreview(int lanes, string file)
        {
            bool wasOpen = IsGameSceneOpen();
            var scene = OpenGameScene();
            // Outras cenas abertas (ex.: HomeScene do usuário) ficam ocultas só durante o render.
            var hidden = new System.Collections.Generic.List<GameObject>();
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var other = SceneManager.GetSceneAt(si);
                if (other == scene || !other.isLoaded) continue;
                foreach (var g in other.GetRootGameObjects())
                    if (g.activeSelf) { g.SetActive(false); hidden.Add(g); }
            }
            var prevActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            GameObject holderGo = null;
            try
            {
            var rail = AssetDatabase.LoadAssetAtPath<RailGenConfig>(RailGenConfigPath);
            var diff = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyConfigPath);
            var preset = AssetDatabase.LoadAssetAtPath<SwitchVisualConfig>($"{CfgDir}/SwitchVisual_Aurora.asset");
            var conOn = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Aurora_ConnectorOn.mat");
            var conVoid = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Aurora_ConnectorVoid.mat");
            var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Spawnables}/TrackTile_Prefab.prefab");

            int gmax = rail.globalMaxLanes;
            int first = (gmax - lanes) / 2;
            var layout = lanes == 3 ? Layout3 : Layout7;
            var rng = new System.Random(7 + lanes);
            var tiles = new TrackTile[layout.Length, gmax];
            holderGo = new GameObject("__AuroraPreview");
            SceneManager.MoveGameObjectToScene(holderGo, scene);
            var holder = holderGo.transform;

            for (int r = 0; r < layout.Length; r++)
                for (int i = 0; i < lanes; i++)
                {
                    if (layout[r][i] != 'X') continue;
                    int lane = first + i;
                    var go = (GameObject)UnityEngine.Object.Instantiate(tilePrefab, holder);
                    go.transform.position = TrackTile.ComputeWorldPosition(r, lane, gmax, rail);
                    var tt = go.GetComponent<TrackTile>();
                    tt.Row = r; tt.Lane = lane;
                    tiles[r, lane] = tt;
                }

            bool Has(int r, int lane) => r >= 0 && r < layout.Length && lane >= first && lane < first + lanes && tiles[r, lane] != null;

            for (int r = 0; r < layout.Length; r++)
                for (int lane = 0; lane < gmax; lane++)
                {
                    var tt = tiles[r, lane];
                    if (tt == null) continue;
                    var sv = tt.GetComponentInChildren<SwitchVisual>(true);
                    var options = new[] { SwitchState.Left, SwitchState.Middle, SwitchState.Right };
                    var ok = options.Where(s => Has(r + 1, lane + (int)s)).ToArray();
                    var state = (ok.Length > 0 && rng.NextDouble() > 0.15)
                        ? ok[rng.Next(ok.Length)]
                        : options[rng.Next(3)];
                    sv.SetConfig(preset);
                    sv.Apply(state, false);
                    var rr = sv.GetRenderer(state);
                    if (rr != null) rr.sharedMaterial = (r + 1 >= layout.Length || Has(r + 1, lane + (int)state)) ? conOn : conVoid;
                }

            int mid = first + lanes / 2;
            int left = mid - 1, right = mid + 1;

            // Player na row 1, lane central
            var rider = FirstInScene<PlayerRailRider>(scene);
            var p1 = tiles[1, mid];
            var playerPos = p1.StartPoint.position + Vector3.forward * 2.5f;
            playerPos.y = rider.transform.position.y;
            rider.transform.position = playerPos;
            var trail = rider.GetComponentInChildren<TrailRenderer>(true);
            if (trail != null) trail.enabled = false;   // trail não tem histórico num frame estático

            // Conteúdo
            GameObject Spawn(string prefab, TrackTile tile, int slot, float h)
            {
                if (tile == null) return null;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Spawnables}/{prefab}.prefab");
                // Igual aos spawners do jogo: rotação identity.
                var go = (GameObject)UnityEngine.Object.Instantiate(pf, tile.GetSlotPosition(slot, 5, 0.1f, h), Quaternion.identity, holder);
                return go;
            }
            for (int s = 1; s <= 3; s++) Spawn("Resource_CoinPrefab", tiles[2, mid], s, 0.5f);
            Spawn("HazardVariant_Lethal Variant", tiles[2, left] ?? tiles[2, right], 2, 0.5f);
            Spawn("PowerUpVariant_Shield Variant", tiles[3, mid] ?? tiles[3, right], 2, 0.8f);
            Spawn("Resource_Gem", tiles[4, left] ?? tiles[4, mid], 3, 0.5f);
            Spawn("HazardVariant_Barrier Variant", tiles[4, right] ?? tiles[4, mid], 1, 0.5f);
            Spawn("DebuffVariant_SpeedUp Variant", tiles[5, mid] ?? tiles[5, left], 2, 0.8f);
            Spawn("PowerUpVariant_Magnet Variant", tiles[6, left] ?? tiles[6, mid], 2, 0.8f);
            for (int s = 0; s <= 4; s++) Spawn("Resource_CoinPrefab", tiles[7, mid] ?? tiles[7, right], s, 0.5f);

            // Câmera igual ao PlayerCameraRig (tier 0, sem death cam)
            var rig = FirstInScene<PlayerCameraRig>(scene);
            var cam = rig.GetComponent<Camera>();
            var tier = diff.tiers[0];
            float zoom = (tier.cameraZoom > 0 ? tier.cameraZoom : 10f) * Mathf.Max(0.01f, rail.cameraZoomGlobalMultiplier);
            float tilt = tier.overrideCameraAngle ? tier.cameraTilt : rail.cameraTilt;
            cam.fieldOfView = tier.overrideCameraAngle ? tier.cameraFieldOfView : rail.cameraFieldOfView;
            var rot = Quaternion.Euler(tilt, 0f, 0f);
            var focusBase = new Vector3(0f, playerPos.y, playerPos.z);
            var focus = focusBase + rot * Vector3.up * (zoom * rail.cameraLookAhead);
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * zoom, rot);

            // Billboards / snaps que só rodam em play
            foreach (var b in holder.GetComponentsInChildren<BillboardSprite>(true))
                b.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
            foreach (var mo in holder.GetComponentsInChildren<AuroraMotion>(true))
                if (mo.faceCamera) mo.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
            foreach (var g in holder.GetComponentsInChildren<AuroraGroundSnap>(true)) g.Snap();

            // Juice em edit mode: poça de luz no player + onda congelada a 0.28s do pouso.
            var pj = rider.GetComponentInChildren<AuroraPlayerJuice>(true);
            var bodyPos = pj != null ? pj.BodyPosition : playerPos;
            Shader.SetGlobalVector("_AuroraPlayerPos", new Vector4(bodyPos.x, bodyPos.y, bodyPos.z, 2.4f));
            Shader.SetGlobalColor("_AuroraGlowColor", new Color(0.25f * 0.9f, 0.95f * 0.9f, 1.1f * 0.9f, 1f));
            Shader.SetGlobalFloat("_AuroraTime", 10.28f);
            Shader.SetGlobalVector("_AuroraWave", new Vector4(bodyPos.x, 10f, bodyPos.z - 1f, 0.55f));
            Shader.SetGlobalVector("_AuroraWaveParams", new Vector4(22f, 1.6f, 0.9f, 0f));
            Shader.SetGlobalColor("_AuroraWaveColor", new Color(0.3f, 1.6f, 1.4f));

            var env = FirstInScene<AuroraEnvironment>(scene);
            if (env != null) { env.PushGlobals(); env.Tick(cam.transform.position); }

            // Render (9:19.5, MSAA 4x, com post)
            const int w = 720, h = 1560;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            Directory.CreateDirectory(PreviewDir);
            File.WriteAllBytes(Path.Combine(PreviewDir, file), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[Aurora] preview salvo: {PreviewDir}/{file} (zoom {zoom}, tilt {tilt}, fov {cam.fieldOfView})");
            }
            finally
            {
                foreach (var g in hidden) if (g != null) g.SetActive(true);
                if (prevActiveScene.IsValid() && prevActiveScene.isLoaded) SceneManager.SetActiveScene(prevActiveScene);
                if (wasOpen)
                {
                    // Cena já estava aberta: só remove o layout (player/câmera movidos ficam sujos — não salvar).
                    if (holderGo != null) UnityEngine.Object.DestroyImmediate(holderGo);
                }
                else EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
