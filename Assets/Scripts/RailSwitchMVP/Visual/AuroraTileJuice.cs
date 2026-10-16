using System.Collections.Generic;
using UnityEngine;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Os trilhos "se montam" subindo do vazio ao longe e afundam depois que o player
    /// passa. Só VISUAL: desloca o mesh do trilho, os conectores e os filhos visuais
    /// ("Visual" / "Aurora_Visual") dos itens spawnados. Colliders, StartPoint/EndPoint
    /// e posições de gameplay não se movem. Perto do player o deslocamento é zero.
    /// </summary>
    public class AuroraTileJuice : MonoBehaviour
    {
        [SerializeField] private Transform[] visuals;

        [Header("Montagem à frente")]
        [Tooltip("Margem (Z) antes da borda de cima da tela onde o tile começa a afundar. " +
            "O início acompanha o alcance real da câmera (AuroraJuice.VisibleAhead), então a " +
            "montagem nunca esconde rows que o jogador precisa ler.")]
        [SerializeField] private float riseMarginBeforeEdge = 4f;
        [SerializeField] private float riseLength = 26f;
        [SerializeField] private float riseDepth = 9f;
        [Tooltip("Atraso por lane (efeito cascata).")]
        [SerializeField] private float laneStagger = 2.2f;

        [Header("Desmontagem atrás")]
        [SerializeField] private float sinkStart = 10f;
        [SerializeField] private float sinkLength = 16f;
        [SerializeField] private float sinkDepth = 7f;

        Vector3[] _base;
        readonly Dictionary<Transform, float> _applied = new Dictionary<Transform, float>();
        readonly List<Transform> _dead = new List<Transform>();
        Track.TrackTile _tile;

        void Awake()
        {
            _tile = GetComponent<Track.TrackTile>();
            _base = new Vector3[visuals != null ? visuals.Length : 0];
            for (int i = 0; i < _base.Length; i++)
                if (visuals[i] != null) _base[i] = visuals[i].localPosition;
        }

        void OnEnable() => _applied.Clear();

        void LateUpdate()
        {
            float dz = transform.position.z - AuroraJuice.PlayerPos.z;
            float lane = _tile != null ? _tile.Lane : 0;
            float off = 0f;

            float riseStart = Mathf.Max(20f, AuroraJuice.VisibleAhead - riseMarginBeforeEdge);
            float ahead = dz - (riseStart + (lane % 3) * laneStagger);
            if (ahead > 0f)
            {
                float k = Mathf.Clamp01(ahead / riseLength);
                off = -k * k * riseDepth;
            }
            else if (dz < -sinkStart)
            {
                float k = Mathf.Clamp01((-dz - sinkStart) / sinkLength);
                off = -k * k * sinkDepth;
            }

            for (int i = 0; i < _base.Length; i++)
                if (visuals[i] != null)
                    visuals[i].localPosition = _base[i] + Vector3.up * off;

            // Itens spawnados (filhos diretos não-permanentes): desloca só o filho visual.
            for (int c = 0; c < transform.childCount; c++)
            {
                var item = transform.GetChild(c);
                var vis = item.Find("Aurora_Visual") ?? item.Find("Visual");
                if (vis == null || IsPermanent(item)) continue;
                _applied.TryGetValue(vis, out float prev);
                if (Mathf.Abs(prev - off) < 1e-4f) continue;
                vis.position += Vector3.up * (off - prev);
                _applied[vis] = off;
            }

            if (_applied.Count > 24)
            {
                _dead.Clear();
                foreach (var kv in _applied) if (kv.Key == null) _dead.Add(kv.Key);
                foreach (var d in _dead) _applied.Remove(d);
            }
        }

        bool IsPermanent(Transform t)
        {
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null && (t == visuals[i] || visuals[i].IsChildOf(t))) return true;
            return t.name == "StartPoint" || t.name == "EndPoint" || t.name == "Switch";
        }
    }
}
