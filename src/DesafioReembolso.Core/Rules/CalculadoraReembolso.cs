using DesafioReembolso.Core.Enums;

namespace DesafioReembolso.Core.Rules;

public sealed class CalculadoraReembolso : ICalculadoraReembolso
{
    public const decimal TetoPadrao = 500.00m;

    public decimal TetoMaximoReembolso { get; }

    public CalculadoraReembolso(decimal tetoMaximo = TetoPadrao)
    {
        TetoMaximoReembolso = tetoMaximo;
    }

    public decimal ObterPercentual(TipoProcedimento tipo) => tipo.ObterPercentualReembolso();

    public ResultadoCalculoReembolso Calcular(TipoProcedimento tipo, decimal valorPago)
    {
        if (valorPago <= 0)
        {
            return new ResultadoCalculoReembolso(0m, false, 0m);
        }

        var percentual = ObterPercentual(tipo);
        var valorCalculado = decimal.Round(valorPago * percentual, 2, MidpointRounding.AwayFromZero);
        var atingiuTeto = valorCalculado > TetoMaximoReembolso;
        var valorReembolsado = atingiuTeto ? TetoMaximoReembolso : valorCalculado;

        return new ResultadoCalculoReembolso(valorReembolsado, atingiuTeto, valorCalculado);
    }

    public decimal CalcularValorReembolsado(TipoProcedimento tipo, decimal valorPago)
    {
        return Calcular(tipo, valorPago).ValorReembolsado;
    }
}
