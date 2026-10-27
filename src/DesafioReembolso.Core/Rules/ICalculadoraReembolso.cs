using DesafioReembolso.Core.Enums;

namespace DesafioReembolso.Core.Rules;

public readonly record struct ResultadoCalculoReembolso(
    decimal ValorReembolsado,
    bool AtingiuTetoMaximo,
    decimal ValorCalculadoSemTeto);

public interface ICalculadoraReembolso
{
    decimal TetoMaximoReembolso { get; }
    decimal ObterPercentual(TipoProcedimento tipo);
    ResultadoCalculoReembolso Calcular(TipoProcedimento tipo, decimal valorPago);
    decimal CalcularValorReembolsado(TipoProcedimento tipo, decimal valorPago);
}
