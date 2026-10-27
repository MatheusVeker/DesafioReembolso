namespace DesafioReembolso.Application.DTOs;

public sealed record ProcessarLoteRequest
{
    public string DataReferencia { get; init; } = string.Empty;
    public List<PedidoInputDto> Pedidos { get; init; } = new();
}

public sealed record ProcessarLoteResponse
{
    public string DataReferencia { get; init; } = string.Empty;
    public int TotalProcessados { get; init; }
    public int TotalAprovados { get; init; }
    public int TotalRejeitados { get; init; }
    public int TotalSuspeitos { get; init; }
    public decimal ValorTotalReembolsadoAprovado { get; init; }
    public List<PedidoOutputDto> Pedidos { get; init; } = new();
}
