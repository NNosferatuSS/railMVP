using UnityEditor;
using UnityEngine;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Configura a importação dos clips gerados por Art/Source/Audio/aurora_synth.py
    /// (Assets/Audio/Resources/AuroraAudio). Mobile-friendly:
    /// - music_*: Vorbis comprimido em memória (5 camadas tocando juntas, pouca RAM).
    /// - sfx_*:   descomprimido no load (latência mínima pra coin/switch), mono forçado.
    /// </summary>
    public class AuroraAudioImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Audio/Resources/AuroraAudio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (AudioImporter)assetImporter;
            var file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool music = file.StartsWith("music_");

            var s = imp.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.55f : 0.75f;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;

            imp.forceToMono = !music && !IsStereoSfx(file);
            imp.loadInBackground = music;
            imp.ambisonic = false;
        }

        // SFX com reverb/espacialização estéreo que vale manter.
        static bool IsStereoSfx(string f) =>
            f == "sfx_gem" || f == "sfx_powerup_collect" || f == "sfx_powerup_activate" ||
            f == "sfx_tier_up" || f == "sfx_death" || f == "sfx_revive" || f == "sfx_go" ||
            f == "sfx_shield_break" || f == "sfx_impact" || f == "sfx_debuff" || f == "sfx_powerdown" ||
            f == "sfx_count";
    }
}
