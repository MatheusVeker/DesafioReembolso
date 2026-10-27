namespace DesafioReembolso.Application.DTOs;

public sealed record PedidoOutputDto
{
    public int Id { get; init; }
    public string TipoProcedimento { get; init; } = string.Empty;
    public string DataProcedimento { get; init; } = string.Empty;
    public decimal ValorPago { get; init; }
    public decimal ValorReembolsado { get; init; }
    public string ClienteId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Motivo { get; init; }
}
