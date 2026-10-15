using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Player;
using RailSwitchMVP.PowerUps;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Audio
{
    /// <summary>
    /// Áudio da direção Aurora: música adaptativa em camadas + SFX. Se cria sozinho
    /// (RuntimeInitializeOnLoadMethod) e sobrevive entre cenas. Clips em
    /// Resources/AuroraAudio (gerados por Art/Source/Audio/aurora_synth.py).
    ///
    /// Música: 5 loops sincronizados (mesmo comprimento, PlayScheduled no mesmo dspTime).
    /// Cada camada tem volume-alvo por estado (menu / warmup / tier / morte / pause).
    /// Volumes do usuário: PlayerPrefs "settings.music" / "settings.sfx" (sliders do Pause).
    ///
    /// Só escuta eventos — não altera gameplay.
    /// </summary>
    public class AuroraAudio : MonoBehaviour
    {
        public static AuroraAudio Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("AuroraAudio");
            go.AddComponent<AuroraAudio>();
        }

        // ------------------------------------------------------------ config

        static readonly string[] Layers = { "music_pad", "music_arp", "music_drums", "music_bass", "music_sparkle" };
        const int Pad = 0, Arp = 1, Drums = 2, Bass = 3, Sparkle = 4;

        const string KMusic = "settings.music";
        const string KSfx = "settings.sfx";

        const float CoinComboWindow = 1.4f;
        const int SfxVoices = 16;

        enum Mode { Menu, Warmup, Run, Dead }

        // ------------------------------------------------------------ estado

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        AudioSource[] _music;
        AudioLowPassFilter[] _lpf;
        AudioSource[] _sfx;
        int _sfxNext;
        readonly float[] _layerVol = new float[5];
        float _musicPitch = 1f, _cutoff = 22000f;

        Mode _mode = Mode.Menu;
        int _tierSteps;
        float _sceneTime;
        float _lastCoinTime = -10f;
        int _coinCombo;
        float _deathTime = -10f;
        bool _wasInGap;
        float _userMusic = 0.7f, _userSfx = 0.8f, _prefsTimer;

        GameManager _gm;
        DifficultyManager _dm;
        PowerUpManager _pm;
        PlayerRailRider _rider;
        Camera _cam;
        readonly HashSet<int> _wiredButtons = new HashSet<int>();
        float _buttonScanTimer;

        // ------------------------------------------------------------ ciclo de vida

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (var c in Resources.LoadAll<AudioClip>("AuroraAudio")) _clips[c.name] = c;

            _music = new AudioSource[Layers.Length];
            _lpf = new AudioLowPassFilter[Layers.Length];
            for (int i = 0; i < Layers.Length; i++)
            {
                var go = new GameObject(Layers[i]);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.clip = Clip(Layers[i]);
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0f;
                s.spatialBlend = 0f;
                s.priority = 0;
                s.ignoreListenerPause = true;
                _music[i] = s;
                _lpf[i] = go.AddComponent<AudioLowPassFilter>();
                _lpf[i].cutoffFrequency = 22000f;
            }

            _sfx = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.priority = 64;
                _sfx[i] = s;
            }

            double start = AudioSettings.dspTime + 0.25;
            foreach (var s in _music) if (s.clip != null) s.PlayScheduled(start);

            CollectibleCoin.Collected += OnCoin;
            GemPickup.Collected += OnGem;
            PowerUpBase.Collected += OnPowerUpCollected;
            SwitchController.AnyNudged += OnNudge;
            WarmupController.CountdownStep += OnCountdown;
            PlayerCameraRig.ShakeRequested += OnShake;
            SceneManager.sceneLoaded += OnSceneLoaded;

            ReadPrefs();
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            CollectibleCoin.Collected -= OnCoin;
            GemPickup.Collected -= OnGem;
            PowerUpBase.Collected -= OnPowerUpCollected;
            SwitchController.AnyNudged -= OnNudge;
            WarmupController.CountdownStep -= OnCountdown;
            PlayerCameraRig.ShakeRequested -= OnShake;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindScene();
            Instance = null;
        }

        AudioClip Clip(string name) => _clips.TryGetValue(name, out var c) ? c : null;

        // ------------------------------------------------------------ cenas

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            UnbindScene();
            _sceneTime = Time.unscaledTime;
            _tierSteps = 0;
            _coinCombo = 0;
            _wiredButtons.Clear();
            _buttonScanTimer = 0f;
            _mode = scene.name == SceneNames.Game ? Mode.Warmup : Mode.Menu;
            StartCoroutine(BindNextFrame());
        }

        IEnumerator BindNextFrame()
        {
            yield return null;   // singletons da cena já passaram pelo Awake
            _gm = GameManager.Instance;
            _dm = DifficultyManager.Instance;
            _pm = PowerUpManager.Instance;
            _rider = FindFirstObjectByType<PlayerRailRider>();
            if (_gm != null)
            {
                _gm.OnGameOver += OnGameOver;
                _gm.OnRevived += OnRevived;
                _gm.OnWarmupEnded += OnWarmupEnded;
                if (_gm.IsPlaying) _mode = Mode.Run;
            }
            if (_dm != null) _dm.OnTierChanged += OnTier;
            if (_pm != null)
            {
                _pm.OnPowerUpActivated += OnPowerUpActivated;
                _pm.OnPowerUpExpired += OnPowerUpExpired;
            }
            if (_rider != null) _rider.OnTileEntered += OnLand;
        }

        void UnbindScene()
        {
            if (_gm != null)
            {
                _gm.OnGameOver -= OnGameOver;
                _gm.OnRevived -= OnRevived;
                _gm.OnWarmupEnded -= OnWarmupEnded;
            }
            if (_dm != null) _dm.OnTierChanged -= OnTier;
            if (_pm != null)
            {
                _pm.OnPowerUpActivated -= OnPowerUpActivated;
                _pm.OnPowerUpExpired -= OnPowerUpExpired;
            }
            if (_rider != null) _rider.OnTileEntered -= OnLand;
            _gm = null; _dm = null; _pm = null; _rider = null;
        }

        // ------------------------------------------------------------ eventos → SFX

        void OnCoin(Vector3 p)
        {
            float now = Time.unscaledTime;
            _coinCombo = now - _lastCoinTime <= CoinComboWindow ? _coinCombo + 1 : 0;
            _lastCoinTime = now;
            Play($"sfx_coin_{Mathf.Min(_coinCombo, 9)}", 0.5f, 1f, PanOf(p));
        }

        void OnGem(Vector3 p) => Play("sfx_gem", 0.8f, 1f, PanOf(p));
        void OnPowerUpCollected(Vector3 p) => Play("sfx_powerup_collect", 0.8f, 1f, PanOf(p));

        void OnPowerUpActivated(PowerUpType t)
        {
            bool debuff = t == PowerUpType.SpeedUpDebuff || t == PowerUpType.LaneSwapDebuff;
            if (debuff) Play("sfx_debuff", 0.75f);
            else Play("sfx_powerup_activate", 0.5f, PitchFor(t));
        }

        void OnPowerUpExpired(PowerUpType t)
        {
            if (_mode == Mode.Dead) return;
            if (t == PowerUpType.Shield) Play("sfx_shield_break", 0.85f);
            else if (t != PowerUpType.SpeedUpDebuff && t != PowerUpType.LaneSwapDebuff) Play("sfx_powerdown", 0.35f);
        }

        void OnNudge(SwitchState s)
        {
            string clip = s == SwitchState.Left ? "sfx_switch_left" : s == SwitchState.Right ? "sfx_switch_right" : "sfx_switch_mid";
            Play(clip, 0.42f, Random.Range(0.98f, 1.02f), (int)s * 0.25f);
        }

        void OnLand(TrackTile tile)
        {
            if (_mode == Mode.Dead) return;
            Play("sfx_land", 0.18f, Random.Range(0.94f, 1.06f));
        }

        void OnCountdown(int step)
        {
            if (step > 0) Play("sfx_count", 0.6f, step == 1 ? 1.122f : 1f);   // "1" um tom acima
            else Play("sfx_go", 0.85f);
        }

        void OnWarmupEnded() => _mode = Mode.Run;

        void OnTier(DifficultyTier tier)
        {
            // O tier inicial é aplicado no começo da run: não comemorar isso.
            if (Time.unscaledTime - _sceneTime < 1.5f || _mode == Mode.Warmup) return;
            _tierSteps++;
            Play("sfx_tier_up", 0.75f);
        }

        void OnShake(float intensity)
        {
            if (_mode == Mode.Dead || Time.unscaledTime - _deathTime < 0.5f) return;
            if (intensity >= 0.25f) Play("sfx_impact", Mathf.Clamp01(intensity * 1.3f));
        }

        void OnGameOver(GameOverReason reason)
        {
            _mode = Mode.Dead;
            _deathTime = Time.unscaledTime;
            Play("sfx_death", 0.95f);
        }

        void OnRevived()
        {
            _mode = Mode.Run;
            Play("sfx_revive", 0.8f);
        }

        static float PitchFor(PowerUpType t)
        {
            // Variação tonal por tipo, sempre em intervalos da escala (1, 9/8, 6/5, 4/3, 3/2).
            switch (t)
            {
                case PowerUpType.Shield: return 1f;
                case PowerUpType.Magnet: return 1.125f;
                case PowerUpType.DoubleCoins: return 1.5f;
                case PowerUpType.Ghost: return 1.2f;
                case PowerUpType.TimeFreeze:
                case PowerUpType.SlowDown: return 0.75f;
                default: return 1.333f;
            }
        }

        // ------------------------------------------------------------ playback

        float PanOf(Vector3 world)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return 0f;
            return Mathf.Clamp((world.x - _cam.transform.position.x) / 9f, -0.6f, 0.6f);
        }

        /// <summary>Toca um SFX 2D (pool round-robin). Público pra outros sistemas (UI etc.).</summary>
        public void Play(string clipName, float volume = 1f, float pitch = 1f, float panStereo = 0f)
        {
            var clip = Clip(clipName);
            if (clip == null || _userSfx <= 0.001f) return;
            var s = _sfx[_sfxNext];
            _sfxNext = (_sfxNext + 1) % _sfx.Length;
            s.Stop();
            s.clip = clip;
            s.volume = volume * _userSfx;
            s.pitch = pitch;
            s.panStereo = panStereo;
            s.Play();
        }

        // ------------------------------------------------------------ música adaptativa

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _prefsTimer -= dt;
            if (_prefsTimer <= 0f) { ReadPrefs(); _prefsTimer = 0.5f; }

            TrackGap();
            ScanButtons(dt);

            bool paused = Time.timeScale <= 0.001f && _mode != Mode.Dead && _mode != Mode.Menu;
            int tierIdx = _dm != null ? _dm.CurrentTierIndex : 0;

            // Volumes-alvo por estado (0..1, o mix já vem balanceado do synth).
            float pad = 0.9f, arp = 0f, drums = 0f, bass = 0f, sparkle = 0f;
            float cutoff = 22000f, pitch = 1f;
            switch (_mode)
            {
                case Mode.Menu:
                    arp = 0.55f; sparkle = 0.7f; cutoff = 3200f;
                    break;
                case Mode.Warmup:
                    arp = 0.85f; sparkle = 0.35f; bass = 0.35f; cutoff = 6000f;
                    break;
                case Mode.Run:
                    arp = 1f; drums = 1f; bass = 1f;
                    sparkle = Mathf.Clamp01((Mathf.Max(tierIdx, _tierSteps) - 1) * 0.5f);
                    pad = 0.8f;
                    break;
                case Mode.Dead:
                    arp = 0.6f; bass = 0.3f; drums = 0f; sparkle = 0.2f;
                    cutoff = 520f; pitch = 0.86f;
                    break;
            }
            if (paused) { cutoff = Mathf.Min(cutoff, 900f); drums *= 0.5f; }

            float k = 1f - Mathf.Exp(-dt * (_mode == Mode.Dead ? 1.6f : 2.5f));
            _layerVol[Pad] = Mathf.Lerp(_layerVol[Pad], pad, k);
            _layerVol[Arp] = Mathf.Lerp(_layerVol[Arp], arp, k);
            _layerVol[Drums] = Mathf.Lerp(_layerVol[Drums], drums, k * 2f);
            _layerVol[Bass] = Mathf.Lerp(_layerVol[Bass], bass, k * 1.5f);
            _layerVol[Sparkle] = Mathf.Lerp(_layerVol[Sparkle], sparkle, k * 0.5f);
            // cutoff em escala log (soa natural)
            _cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(_cutoff), Mathf.Log(cutoff), k * 1.5f));
            _musicPitch = Mathf.Lerp(_musicPitch, pitch, k);

            for (int i = 0; i < _music.Length; i++)
            {
                _music[i].volume = _layerVol[i] * _userMusic;
                _music[i].pitch = _musicPitch;
                _lpf[i].cutoffFrequency = _cutoff;
            }
        }

        void ReadPrefs()
        {
            _userMusic = PlayerPrefs.GetFloat(KMusic, 0.7f);
            _userSfx = PlayerPrefs.GetFloat(KSfx, 0.8f);
        }

        void TrackGap()
        {
            if (_rider == null || _mode == Mode.Dead) { _wasInGap = false; return; }
            var tile = _rider.CurrentTile;
            bool inGap = tile != null && tile.EndPoint != null
                && _rider.transform.position.z > tile.EndPoint.position.z + 0.05f;
            if (inGap && !_wasInGap) Play("sfx_whoosh", 0.22f, Random.Range(0.92f, 1.1f));
            _wasInGap = inGap;
        }

        // Clique em TODOS os botões de UI (inclusive os que aparecem depois).
        void ScanButtons(float dt)
        {
            _buttonScanTimer -= dt;
            if (_buttonScanTimer > 0f) return;
            _buttonScanTimer = 1.5f;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!_wiredButtons.Add(b.GetInstanceID())) continue;
                b.onClick.AddListener(() => Play("sfx_ui_click", 0.5f, Random.Range(0.97f, 1.03f)));
            }
        }
    }
}
