using RailSwitchMVP.Track;

namespace RailSwitchMVP.InputSys
{
    public enum SwitchCommandKind
    {
        /// <summary>Empurra o switch ±1 (tap na metade da tela, teclado).</summary>
        Relative,
        /// <summary>Aponta pro trilho tocado (row/lane globais) — resolvido no tile em que for aplicado.</summary>
        TargetTile,
        /// <summary>Swipe depois de um toque: estado = (estado no momento do toque) + dir, no MESMO tile.</summary>
        OverrideFromPress,
        /// <summary>Trilho 2+ linhas à frente tocado: o trilho da SUA rota logo antes dele passa a apontar pra ele.</summary>
        RouteTile,
    }

    /// <summary>Comando de troca de conector produzido pelo input.</summary>
    public struct SwitchCommand
    {
        public SwitchCommandKind kind;
        public int dir;
        public int targetRow;
        public int targetLane;
        public TrackTile pressTile;
        public int pressState;
        /// <summary>Time.unscaledTime em que o comando nasceu (buffer).</summary>
        public float time;
    }

    /// <summary>
    /// Input que produz comandos ricos (além do ±1 do IDirectionalInput).
    /// O PlayerRailRider consome e aplica com buffer / tolerância.
    /// </summary>
    public interface ISwitchCommandInput
    {
        bool TryConsumeCommand(out SwitchCommand cmd);
    }
}
