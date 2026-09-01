#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using RailSwitchMVP.UI.Theme;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Baixa Chakra Petch (Bold) e Barlow Semi Condensed (Bold/SemiBold/Medium)
    /// direto do repositório Google Fonts, gera TMP_FontAssets via
    /// TMP_FontAsset.CreateFontAsset (Dynamic, SDF), e popula os campos no UITheme.
    ///
    /// Menu: Tools -> RailSwitch -> Build UI -> 2. Import Fonts.
    ///
    /// Nota: as fontes ficam em modo Dynamic — TMP gera glyphs on-demand.
    /// Isso evita a UI de Font Asset Creator (que exige interação manual)
    /// e ainda mantém qualidade SDF. Se quiser atlas estático fixo pra shipping,
    /// abra o TMP Font Asset Creator manualmente depois.
    /// </summary>
    public static class UIFontImporter
    {
        const string FontsFolder = "Assets/UI/Fonts";

        // URLs raw dos TTF no repositório Google Fonts (Apache 2 / OFL).
        static readonly (string url, string filename, string field)[] Sources =
        {
            ("https://raw.githubusercontent.com/google/fonts/main/ofl/chakrapetch/ChakraPetch-Bold.ttf",
                "ChakraPetch-Bold.ttf", "chakraBold"),
            ("https://raw.githubusercontent.com/google/fonts/main/ofl/barlowsemicondensed/BarlowSemiCondensed-Bold.ttf",
                "BarlowSemiCondensed-Bold.ttf", "barlowBold"),
            ("https://raw.githubusercontent.com/google/fonts/main/ofl/barlowsemicondensed/BarlowSemiCondensed-SemiBold.ttf",
                "BarlowSemiCondensed-SemiBold.ttf", "barlowSemiBold"),
            ("https://raw.githubusercontent.com/google/fonts/main/ofl/barlowsemicondensed/BarlowSemiCondensed-Medium.ttf",
                "BarlowSemiCondensed-Medium.ttf", "barlowMedium"),
        };

        [MenuItem("Tools/RailSwitch/Build UI/2. Import Fonts", priority = 20)]
        public static void ImportAll()
        {
            EnsureFolder(FontsFolder);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            int downloaded = 0;
            foreach (var (url, filename, _) in Sources)
            {
                var target = FontsFolder + "/" + filename;
                var abs = Path.Combine(Directory.GetCurrentDirectory(), target);
                if (!File.Exists(abs))
                {
                    Debug.Log($"[RailSwitchUI] Baixando {filename}...");
                    try
                    {
                        using (var wc = new WebClient())
                            wc.DownloadFile(url, abs);
                        downloaded++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[RailSwitchUI] Falha ao baixar {filename}: {ex.Message}\nURL: {url}");
                    }
                }
            }
            AssetDatabase.Refresh();

            // Gera TMP Font Assets em modo Dynamic (SDF)
            var theme = UIAssetImporter.EnsureUITheme();
            int generated = 0;
            foreach (var (_, filename, field) in Sources)
            {
                var ttfPath = FontsFolder + "/" + filename;
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ttfPath))) continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (font == null) continue;

                var fontAssetPath = FontsFolder + "/" + Path.GetFileNameWithoutExtension(filename) + " SDF.asset";
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);

                // Se o asset existe mas está com atlas quebrado (bug do Dynamic
                // sem sub-asset persistente), apaga e recria.
                if (fontAsset != null && IsAtlasBroken(fontAsset))
                {
                    Debug.LogWarning($"[RailSwitchUI] {fontAssetPath}: atlas quebrado. Recriando.");
                    AssetDatabase.DeleteAsset(fontAssetPath);
                    fontAsset = null;
                }

                if (fontAsset == null)
                {
                    fontAsset = TMP_FontAsset.CreateFontAsset(
                        font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                        AtlasPopulationMode.Dynamic, true);
                    fontAsset.name = Path.GetFileNameWithoutExtension(filename) + " SDF";
                    AssetDatabase.CreateAsset(fontAsset, fontAssetPath);

                    // Pré-popula ASCII printable + acentos comuns.
                    var sb = new System.Text.StringBuilder();
                    for (int c = 32; c < 127; c++) sb.Append((char)c);
                    sb.Append("áéíóúãõâêîôûàçÁÉÍÓÚÃÕÂÊÎÔÛÀÇ");
                    fontAsset.TryAddCharacters(sb.ToString());

                    // Salva as atlasTextures + material como sub-assets do .asset
                    // (sem isso o Dynamic perde a textura em reimports/domain reloads
                    // e vira "Font Atlas Texture is missing").
                    PersistAtlasSubAssets(fontAsset, fontAssetPath);

                    // Depois de popular + persistir, muda pra Static — atlas
                    // vira "baked" e não é regenerado nem editado em runtime.
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

                    EditorUtility.SetDirty(fontAsset);
                    generated++;
                }

                // Popula campo do theme por reflexão
                var f = typeof(UITheme).GetField(field);
                if (f != null) f.SetValue(theme, fontAsset);
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RailSwitchUI] Fontes: {downloaded} baixadas, {generated} atlas gerados.");
            UIBuildContext.Info("Rail Switch UI",
                $"Fontes baixadas: {downloaded}\nAtlas TMP gerados: {generated}\nUITheme atualizado.");
        }

        static bool IsAtlasBroken(TMP_FontAsset fa)
        {
            if (fa == null) return true;
            var atlases = fa.atlasTextures;
            if (atlases == null || atlases.Length == 0) return true;
            for (int i = 0; i < atlases.Length; i++)
                if (atlases[i] == null) return true;
            if (fa.material == null) return true;
            if (fa.material.mainTexture == null) return true;
            return false;
        }

        // Sem isso, TMP com AtlasPopulationMode.Dynamic cria a atlas texture
        // em memória. Ela vive no arquivo enquanto a sessão está aberta, mas
        // reimports / domain reloads / package updates a perdem, e a runtime
        // fica com "Font Atlas Texture is missing". Adicionando as textures
        // + material como sub-assets do próprio .asset garante persistência.
        static void PersistAtlasSubAssets(TMP_FontAsset fa, string fontAssetPath)
        {
            // Material (compartilhado com o main atlas)
            if (fa.material != null && !AssetDatabase.IsSubAsset(fa.material)
                && AssetDatabase.GetAssetPath(fa.material) != fontAssetPath)
            {
                fa.material.name = fa.name + " Atlas Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            // Todas as atlas textures
            var atlases = fa.atlasTextures;
            if (atlases != null)
            {
                for (int i = 0; i < atlases.Length; i++)
                {
                    var tex = atlases[i];
                    if (tex == null) continue;
                    if (AssetDatabase.IsSubAsset(tex)) continue;
                    if (AssetDatabase.GetAssetPath(tex) == fontAssetPath) continue;
                    tex.name = i == 0 ? fa.name + " Atlas" : fa.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(fontAssetPath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string acc = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = acc + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(acc, parts[i]);
                acc = next;
            }
        }
    }
}
#endif
