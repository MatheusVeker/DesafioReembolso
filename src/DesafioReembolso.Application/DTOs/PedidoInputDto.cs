namespace DesafioReembolso.Application.DTOs;

public sealed record PedidoInputDto
{
    public int Id { get; init; }
    public string TipoProcedimento { get; init; } = string.Empty;
    public string DataProcedimento { get; init; } = string.Empty;
    public decimal ValorPago { get; init; }
    public string ClienteId { get; init; } = string.Empty;
}
