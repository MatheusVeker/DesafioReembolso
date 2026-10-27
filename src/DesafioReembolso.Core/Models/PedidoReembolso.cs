using DesafioReembolso.Core.Enums;

namespace DesafioReembolso.Core.Models;

public sealed record PedidoReembolso
{
    public int Id { get; init; }
    public string TipoProcedimentoOriginal { get; init; } = string.Empty;
    public TipoProcedimento TipoProcedimento { get; init; }
    public DateOnly DataProcedimento { get; init; }
    public decimal ValorPago { get; init; }
    public string ClienteId { get; init; } = string.Empty;

    public static PedidoReembolso Criar(
        int id,
        string tipoProcedimentoTexto,
        DateOnly dataProcedimento,
        decimal valorPago,
        string clienteId)
    {
        var tipo = TipoProcedimentoExtensions.Converter(tipoProcedimentoTexto);
        return new PedidoReembolso
        {
            Id = id,
            TipoProcedimentoOriginal = (tipoProcedimentoTexto ?? string.Empty).Trim(),
            TipoProcedimento = tipo,
            DataProcedimento = dataProcedimento,
            ValorPago = valorPago,
            ClienteId = (clienteId ?? string.Empty).Trim()
        };
    }
}
