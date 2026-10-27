namespace DesafioReembolso.Core.Rules;

public interface IRegraPrazo
{
    int DiasLimitePrazo { get; }
    bool EstaDentroDoPrazo(DateOnly dataProcedimento, DateOnly dataReferencia);
}
