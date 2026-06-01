namespace RailSwitchMVP.Economy
{
    /// <summary>
    /// Tipos de moeda do jogo. Coins é a moeda base (gerida pelo PlayerDataManager,
    /// que o CurrencyManager apenas delega). Gems é premium (sincroniza com Supabase).
    /// EventTokens fica reservado pra eventos (Fase C) — sem uso ativo ainda.
    /// </summary>
    public enum CurrencyType
    {
        Coins = 0,
        Gems = 1,
        EventTokens = 2,
    }
}
