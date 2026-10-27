namespace DesafioReembolso.Core.Rules;

public sealed class RegraPrazo90Dias : IRegraPrazo
{
    public const int LimitePadraoDias = 90;

    public int DiasLimitePrazo { get; }

    public RegraPrazo90Dias(int diasLimite = LimitePadraoDias)
    {
        DiasLimitePrazo = diasLimite;
    }

    public bool EstaDentroDoPrazo(DateOnly dataProcedimento, DateOnly dataReferencia)
    {
        // Só podem ser processados os pedidos realizados nos últimos 90 dias em relação à data de referência.
        // Pedidos futuros (posteriores à data de referência) ou anteriores ao limite são considerados fora do prazo.
        var dataLimiteInferior = dataReferencia.AddDays(-DiasLimitePrazo);
        return dataProcedimento >= dataLimiteInferior && dataProcedimento <= dataReferencia;
    }
}
