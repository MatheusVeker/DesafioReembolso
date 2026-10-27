using System.Globalization;
using DesafioReembolso.Core.Enums;

namespace DesafioReembolso.Core.Models;

public sealed record PedidoProcessado
{
    public int Id { get; init; }
    public string TipoProcedimento { get; init; } = string.Empty;
    public DateOnly DataProcedimento { get; init; }
    public decimal ValorPago { get; init; }
    public decimal ValorReembolsado { get; init; }
    public string ClienteId { get; init; } = string.Empty;
    public StatusPedido Status { get; init; }
    public string StatusDescricao => Status.ObterDescricao();
    public MotivoStatus Motivo { get; init; }
    public string MotivoDetalhe => Motivo.ObterDescricao();

    public static string FormatarValor(decimal valor)
    {
        // Se for número inteiro, formata sem decimais (ex: 400, 320, 500, 140, 75) como no exemplo do enunciado.
        // Se contiver centavos, formata com duas casas decimais utilizando ponto como separador padrão invariante.
        if (valor % 1 == 0)
        {
            return ((long)valor).ToString(CultureInfo.InvariantCulture);
        }

        return valor.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
