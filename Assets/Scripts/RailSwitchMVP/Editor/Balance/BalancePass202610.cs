using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Passe de balanceamento 2026-10 (análise em Docs/Balance/Balance_2026-10.md,
    /// modelo em Docs/Balance/balance_model.py). Reaplicável: escreve valores absolutos.
    ///
    /// - Curva de velocidade suavizada (sem o salto 16→22 e o teto 30 desumano).
    /// - Corrige tiers inválidos (maxLanes 10 > globalMaxLanes 9; dois tiers no mesmo trigger).
    /// - Conteúdo novo entra nos tiers altos (Lethal, debuffs, power-ups de suporte) —
    ///   antes tudo a partir do tier 5 usava o mesmo pool só com Barrier.
    /// - Risco/recompensa: decoys passam a ter coins/power-ups às vezes (decoyCoinChance era 0).
    /// - rowsAhead coerente com o que a câmera mostra (18 rows geradas pra ~5 visíveis).
    ///
    /// Menu: Tools → RailSwitch → Balance → Apply 2026-10 pass.
    /// </summary>
    public static class BalancePass202610
    {
        const string DifficultyPath = "Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset";
        const string RailGenPath = "Assets/ScriptableObjects/RailSwitchMVP/RailGenConfig_Default.asset";
        const string PoolDir = "Assets/ScriptableObjects/Pools";

        // trigger, maxLanes, minPerRow, maxPerRow, critPaths, rowsAhead, speed, zoom,
        // decoyCoinChance, decoyCoinsMax, hazardOnDecoy, powerUpOnDecoy, hazardPool, powerUpPool
        struct T
        {
            public float trigger, speed, zoom, decoyCoin, hazard, puDecoy;
            public int lanes, minRow, maxRow, crit, ahead, decoyCoinsMax;
            public string hz, pu;
        }

        static readonly T[] Plan =
        {
            //        trig  lanes min max crit ahead  speed  zoom   dCoin dMax  hazard puDecoy  hazardPool        powerUpPool
            new T { trigger = 0,    lanes = 3, minRow = 1, maxRow = 2, crit = 1, ahead = 4, speed = 5f,    zoom = 10f,   decoyCoin = 0f,   decoyCoinsMax = 0, hazard = 0f,    puDecoy = 0f,    hz = null,               pu = null },
            new T { trigger = 30,   lanes = 3, minRow = 1, maxRow = 2, crit = 1, ahead = 4, speed = 6f,    zoom = 11f,   decoyCoin = 0f,   decoyCoinsMax = 0, hazard = 0.08f, puDecoy = 0.03f, hz = null,               pu = null },
            new T { trigger = 100,  lanes = 4, minRow = 1, maxRow = 3, crit = 1, ahead = 5, speed = 7.5f,  zoom = 12f,   decoyCoin = 0.3f, decoyCoinsMax = 1, hazard = 0.2f,  puDecoy = 0.04f, hz = null,               pu = null },
            new T { trigger = 210,  lanes = 5, minRow = 1, maxRow = 3, crit = 1, ahead = 6, speed = 9f,    zoom = 13f,   decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.3f,  puDecoy = 0.05f, hz = null,               pu = null },
            new T { trigger = 400,  lanes = 5, minRow = 2, maxRow = 4, crit = 2, ahead = 6, speed = 10.5f, zoom = 13.5f, decoyCoin = 0.3f, decoyCoinsMax = 3, hazard = 0.45f, puDecoy = 0.06f, hz = null,               pu = null },
            new T { trigger = 650,  lanes = 6, minRow = 2, maxRow = 4, crit = 2, ahead = 7, speed = 12f,   zoom = 15f,   decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.5f,  puDecoy = 0.06f, hz = null,               pu = null },
            new T { trigger = 1000, lanes = 7, minRow = 2, maxRow = 5, crit = 2, ahead = 8, speed = 14f,   zoom = 16.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.55f, puDecoy = 0.06f, hz = "HazardPool_Tier6", pu = "PowerUpPool_Tier6" },
            new T { trigger = 1500, lanes = 7, minRow = 2, maxRow = 5, crit = 2, ahead = 8, speed = 16f,   zoom = 17.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.6f,  puDecoy = 0.06f, hz = "HazardPool_Tier6", pu = "PowerUpPool_Tier6" },
            new T { trigger = 2200, lanes = 8, minRow = 2, maxRow = 5, crit = 2, ahead = 8, speed = 18f,   zoom = 18.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.65f, puDecoy = 0.06f, hz = "HazardPool_Tier8", pu = "PowerUpPool_Tier6" },
            new T { trigger = 3000, lanes = 9, minRow = 2, maxRow = 6, crit = 2, ahead = 8, speed = 20.5f, zoom = 19.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.7f,  puDecoy = 0.06f, hz = "HazardPool_Tier8", pu = "PowerUpPool_Tier6" },
            new T { trigger = 4000, lanes = 9, minRow = 3, maxRow = 7, crit = 2, ahead = 8, speed = 23f,   zoom = 20.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.7f,  puDecoy = 0.06f, hz = "HazardPool_Tier8", pu = "PowerUpPool_Tier6" },
            new T { trigger = 5200, lanes = 9, minRow = 3, maxRow = 7, crit = 2, ahead = 8, speed = 25f,   zoom = 21.5f, decoyCoin = 0.3f, decoyCoinsMax = 2, hazard = 0.72f, puDecoy = 0.06f, hz = "HazardPool_Tier8", pu = "PowerUpPool_Tier6" },
        };

        [MenuItem("Tools/RailSwitch/Balance/Apply 2026-10 pass", priority = 300)]
        public static void Apply()
        {
            var pu6 = PowerUpPoolAsset("PowerUpPool_Tier6", new[]
            {
                (PowerUpType.Shield, 1f), (PowerUpType.DoubleCoins, 0.5f), (PowerUpType.Magnet, 1f),
                (PowerUpType.Ghost, 0.7f), (PowerUpType.Teleport, 0.4f), (PowerUpType.SlowDown, 0.6f),
                (PowerUpType.LanePreview, 0.5f), (PowerUpType.CoinRadar, 0.35f), (PowerUpType.TimeFreeze, 0.3f),
            });
            var hz8 = HazardPoolAsset("HazardPool_Tier8", new[]
            {
                (HazardKind.Lethal, 0.45f), (HazardKind.Barrier, 0.3f), (HazardKind.SpeedUp, 0.15f),
                (HazardKind.LaneSwap, 0.12f), (HazardKind.Vortex, 0.1f),
            });
            var hz6 = AssetDatabase.LoadAssetAtPath<HazardPool>($"{PoolDir}/HazardPool_Tier6.asset");

            var cfg = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyPath);
            if (cfg == null) throw new Exception("DifficultyConfig não encontrado");
            Undo.RecordObject(cfg, "Balance 2026-10");

            // Garante 12 tiers (clona o último pra criar os que faltam).
            while (cfg.tiers.Count < Plan.Length)
                cfg.tiers.Add(JsonUtility.FromJson<DifficultyTier>(JsonUtility.ToJson(cfg.tiers[cfg.tiers.Count - 1])));
            while (cfg.tiers.Count > Plan.Length)
                cfg.tiers.RemoveAt(cfg.tiers.Count - 1);

            var sb = new StringBuilder("[Balance 2026-10]\n");
            for (int i = 0; i < Plan.Length; i++)
            {
                var p = Plan[i];
                var t = cfg.tiers[i];
                t.triggerAtDistance = p.trigger;
                t.maxLanes = p.lanes;
                t.minLanesPerRow = p.minRow;
                t.maxLanesPerRow = p.maxRow;
                t.criticalPathsPerRow = p.crit;
                t.rowsAhead = p.ahead;
                t.playerSpeed = p.speed;
                t.cameraZoom = p.zoom;
                t.decoyCoinChance = p.decoyCoin;
                t.decoyCoinsMin = 0;
                t.decoyCoinsMax = p.decoyCoinsMax;
                t.hazardChanceOnDecoy = p.hazard;
                t.powerUpChanceOnDecoy = p.puDecoy;
                if (p.hz == "HazardPool_Tier6" && hz6 != null) t.hazardPool = hz6;
                if (p.hz == "HazardPool_Tier8") t.hazardPool = hz8;
                if (p.pu == "PowerUpPool_Tier6") t.powerUpPool = pu6;
                cfg.tiers[i] = t;   // DifficultyTier é struct
                sb.AppendLine($"T{i}: {p.trigger}m lanes {p.lanes} speed {p.speed} zoom {p.zoom} " +
                    $"hz {(t.hazardPool != null ? t.hazardPool.name : "-")} pu {(t.powerUpPool != null ? t.powerUpPool.name : "-")}");
            }
            cfg.maxStartingTierIndex = Mathf.Min(cfg.maxStartingTierIndex, 5);
            EditorUtility.SetDirty(cfg);

            var rail = AssetDatabase.LoadAssetAtPath<RailGenConfig>(RailGenPath);
            if (rail != null)
            {
                Undo.RecordObject(rail, "Balance 2026-10");
                rail.speedRampPerSecond = 5f;
                rail.cameraLateralFollow = 0.6f;
                rail.cameraLateralSafeZone = 0.65f;
                rail.cameraLateralSmoothTime = 0.28f;
                EditorUtility.SetDirty(rail);
            }

            AssetDatabase.SaveAssets();
            var warn = cfg.GetValidationWarnings();
            if (!string.IsNullOrEmpty(warn)) sb.AppendLine("Validação:\n" + warn);
            Debug.Log(sb.ToString());
        }

        public static void BatchApply()
        {
            try
            {
                Apply();
                StressTestRunner.Run10k();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static PowerUpPool PowerUpPoolAsset(string name, (PowerUpType type, float w)[] entries)
        {
            var path = $"{PoolDir}/{name}.asset";
            var pool = AssetDatabase.LoadAssetAtPath<PowerUpPool>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<PowerUpPool>();
                AssetDatabase.CreateAsset(pool, path);
            }
            pool.entries = new List<PowerUpWeight>();
            foreach (var (type, w) in entries)
                pool.entries.Add(new PowerUpWeight { type = type, weight = w, cooldownRows = 0 });
            EditorUtility.SetDirty(pool);
            return pool;
        }

        static HazardPool HazardPoolAsset(string name, (HazardKind kind, float w)[] entries)
        {
            var path = $"{PoolDir}/{name}.asset";
            var pool = AssetDatabase.LoadAssetAtPath<HazardPool>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<HazardPool>();
                AssetDatabase.CreateAsset(pool, path);
            }
            pool.entries = new List<HazardWeight>();
            foreach (var (kind, w) in entries)
                pool.entries.Add(new HazardWeight { kind = kind, weight = w, cooldownRows = 0 });
            EditorUtility.SetDirty(pool);
            return pool;
        }
    }
}
