using RailSwitchMVP.Core;
using RailSwitchMVP.Gameplay;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// COMETA: por N tiles o player vira um cometa (piloto automático, mais rápido,
    /// esmaga hazards em moedas, ímã gigante, onda de choque no fim). Sem stack: renova.
    /// Efeitos no CometController; parâmetros no GameplayLayerConfig.
    /// </summary>
    public class CometPickup : PowerUpBase
    {
        public int durationTiles = 0; // 0 = usa GameplayLayerConfig.cometTiles

        protected override void Activate()
        {
            var pm = PowerUpManager.Instance;
            if (pm == null) return;
            int tiles = durationTiles > 0 ? durationTiles
                : CometController.Instance != null ? CometController.Instance.DefaultTiles
                : pm.CometDefaultTiles;
            pm.GrantComet(tiles);
        }
    }
}
