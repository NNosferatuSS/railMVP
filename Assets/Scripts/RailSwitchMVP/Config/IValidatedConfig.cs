namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Implementado por SOs de configuração que querem mostrar warnings
    /// no topo do Inspector (via ValidatedConfigInspector no Editor/).
    /// Retorne null ou string vazia se tudo OK.
    /// </summary>
    public interface IValidatedConfig
    {
        string GetValidationWarnings();
    }
}
