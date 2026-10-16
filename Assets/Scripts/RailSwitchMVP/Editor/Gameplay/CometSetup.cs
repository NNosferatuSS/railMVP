using UnityEditor;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.PowerUps;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Monta o power-up COMETA no projeto (idempotente):
    ///  • prefab PowerUpVariant_Comet Variant (clone do Magnet com CometPickup no lugar do MagnetPickup);
    ///  • entrada nos pools dos tiers médios/altos (raro, com cooldown longo);
    ///  • binding type → prefab no ProceduralRailGenerator da cena (chamado pelo Aurora Apply All).
    /// Visual (badge dourado + halo) vem do AuroraArtSetup.ApplyPickups.
    /// </summary>
    public static class CometSetup
    {
        const string Spawnables = "Assets/Prefabs/Spawnables";
        public const string PrefabPath = Spawnables + "/PowerUpVariant_Comet Variant.prefab";
        const string SourcePath = Spawnables + "/PowerUpVariant_Magnet Variant.prefab";
        const string PoolDir = "Assets/ScriptableObjects/Pools";

        // pool, peso, cooldown em rows
        static readonly (string pool, float weight, int cooldown)[] Pools =
        {
            ("PowerUpPool_Tier4", 0.2f, 60),
            ("PowerUpPool_Tier5", 0.25f, 55),
            ("PowerUpPool_Tier6", 0.3f, 50),
        };

        [MenuItem("Tools/RailSwitch/Gameplay/Comet — Setup prefab + pools", priority = 320)]
        public static void EnsurePrefabAndPools()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePath, PrefabPath))
                {
                    Debug.LogError($"[Comet] não consegui copiar {SourcePath}");
                    return;
                }
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var magnet = root.GetComponent<MagnetPickup>();
                float spin = magnet != null ? magnet.spinSpeed : 0f;
                foreach (var mp in root.GetComponents<MagnetPickup>()) Object.DestroyImmediate(mp, true);
                var cp = root.GetComponent<CometPickup>();
                if (cp == null) { cp = root.AddComponent<CometPickup>(); cp.spinSpeed = spin; }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            foreach (var (name, weight, cooldown) in Pools)
            {
                var pool = AssetDatabase.LoadAssetAtPath<PowerUpPool>($"{PoolDir}/{name}.asset");
                if (pool == null) { Debug.LogWarning($"[Comet] pool não encontrado: {name}"); continue; }
                int idx = pool.entries.FindIndex(e => e.type == PowerUpType.Comet);
                var w = new PowerUpWeight { type = PowerUpType.Comet, weight = weight, cooldownRows = cooldown };
                if (idx >= 0) pool.entries[idx] = w; else pool.entries.Add(w);
                EditorUtility.SetDirty(pool);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Comet] prefab + pools prontos.");
        }

        const string HudPrefab = "Assets/UI/Prefabs/HUD/Canvas_HUD.prefab";
        public const string BadgePath = "Assets/Graphics/Aurora/Pickups/Pickup_Comet.png";

        /// <summary>Ícone do cometa no indicador de power-up do HUD (puckIconMap do HUDViewController).</summary>
        public static void BindHudIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(BadgePath);
            if (icon == null) { Debug.LogWarning("[Comet] badge do cometa não importado como Sprite."); return; }
            var root = PrefabUtility.LoadPrefabContents(HudPrefab);
            try
            {
                var hud = root.GetComponentInChildren<RailSwitchMVP.UI.HUDViewController>(true);
                if (hud == null) return;
                var so = new SerializedObject(hud);
                var map = so.FindProperty("puckIconMap");
                int found = -1;
                for (int i = 0; i < map.arraySize; i++)
                    if (map.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue == (int)PowerUpType.Comet) { found = i; break; }
                if (found < 0) { map.InsertArrayElementAtIndex(map.arraySize); found = map.arraySize - 1; }
                var el = map.GetArrayElementAtIndex(found);
                el.FindPropertyRelative("type").intValue = (int)PowerUpType.Comet;
                el.FindPropertyRelative("icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Registra o prefab do cometa no gerador (lista type → prefab).</summary>
        public static void BindGenerator(ProceduralRailGenerator gen)
        {
            if (gen == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogWarning("[Comet] prefab ausente — rode o setup antes."); return; }
            var so = new SerializedObject(gen);
            var list = so.FindProperty("powerUpPrefabs");
            int found = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue == (int)PowerUpType.Comet) { found = i; break; }
            if (found < 0) { list.InsertArrayElementAtIndex(list.arraySize); found = list.arraySize - 1; }
            var el = list.GetArrayElementAtIndex(found);
            el.FindPropertyRelative("type").intValue = (int)PowerUpType.Comet;
            el.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
