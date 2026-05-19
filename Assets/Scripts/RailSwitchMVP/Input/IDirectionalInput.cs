namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Abstração de input lateral (esquerda/direita). MVP usa
    /// KeyboardDirectionalInput; futuro: TouchDirectionalInput.
    /// </summary>
    public interface IDirectionalInput
    {
        /// <summary>
        /// Retorna -1 se esquerda foi pressionada neste frame, +1 se direita, 0 caso contrário.
        /// Considera apenas KEY DOWN (não held), para que Nudge() rode 1x por pressão.
        /// </summary>
        int ConsumeDirection();
    }
}
