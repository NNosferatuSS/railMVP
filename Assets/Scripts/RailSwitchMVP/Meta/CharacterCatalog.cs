using UnityEngine;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Definição estática de um personagem. Visual-only por enquanto — só cor
    /// primária (aplicada via MaterialPropertyBlock no Renderer do Player).
    /// </summary>
    public struct CharacterDef
    {
        public int Index;
        public string Name;
        public Color Primary;
        public int Cost;        // 0 = free / default
    }

    /// <summary>
    /// Catálogo dos personagens disponíveis. Hardcoded — mesmo padrão das
    /// missões (consistência + zero fricção de Editor pra um pool pequeno).
    /// Spec §6.1.
    ///
    /// Index é a "chave" — usado em PlayerDataManager.OwnedChars /
    /// EquippedChar. Nunca renomear ou reciclar índices, ou saves antigos
    /// quebram.
    /// </summary>
    public static class CharacterCatalog
    {
        public static readonly CharacterDef[] All = new CharacterDef[]
        {
            new CharacterDef
            {
                Index = 0, Name = "Runner",
                Primary = new Color(0.95f, 0.95f, 0.98f), // branco/prata
                Cost = 0,
            },
            new CharacterDef
            {
                Index = 1, Name = "Neon",
                Primary = HexColor(0x00, 0xBF, 0xFF), // #00BFFF azul neon
                Cost = 2500,
            },
            new CharacterDef
            {
                Index = 2, Name = "Ember",
                Primary = HexColor(0xFF, 0x45, 0x00), // #FF4500 laranja/vermelho
                Cost = 5000,
            },
        };

        public static int Count => All.Length;

        public static CharacterDef Get(int index)
        {
            if (index < 0 || index >= All.Length) return All[0];
            return All[index];
        }

        static Color HexColor(byte r, byte g, byte b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
