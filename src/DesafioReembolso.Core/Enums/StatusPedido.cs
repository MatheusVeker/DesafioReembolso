namespace DesafioReembolso.Core.Enums;

public enum StatusPedido
{
    Aprovado,
    Rejeitado,
    SuspeitoDeFraude
}

public enum MotivoStatus
{
    AprovadoComSucesso,
    AprovadoLimitadoPeloTeto,
    ForaDoPrazo90Dias,
    LimitePedidosClienteExcedido,
    LimiteValorClienteExcedido,
    LimitePedidosEValorExcedidos
}

public static class StatusPedidoExtensions
{
    public const string DescricaoAprovado = "Aprovado";
    public const string DescricaoRejeitado = "Rejeitado";
    public const string DescricaoSuspeitoDeFraude = "Suspeito de Fraude";

    public static string ObterDescricao(this StatusPedido status) => status switch
    {
        StatusPedido.Aprovado => DescricaoAprovado,
        StatusPedido.Rejeitado => DescricaoRejeitado,
        StatusPedido.SuspeitoDeFraude => DescricaoSuspeitoDeFraude,
        _ => DescricaoRejeitado
    };

    public static string ObterDescricao(this MotivoStatus motivo) => motivo switch
    {
        MotivoStatus.AprovadoComSucesso => "Pedido aprovado dentro das regras e limites vigentes.",
        MotivoStatus.AprovadoLimitadoPeloTeto => "Pedido aprovado com valor limitado ao teto máximo de R$ 500,00 por procedimento.",
        MotivoStatus.ForaDoPrazo90Dias => "Pedido rejeitado: data do procedimento fora do limite de 90 dias em relação à data de referência.",
        MotivoStatus.LimitePedidosClienteExcedido => "Suspeito de fraude: cliente ultrapassou o limite máximo de 5 pedidos na janela de 30 dias.",
        MotivoStatus.LimiteValorClienteExcedido => "Suspeito de fraude: total acumulado de reembolsos do cliente ultrapassou o teto de R$ 1.500,00 na janela de 30 dias.",
        MotivoStatus.LimitePedidosEValorExcedidos => "Suspeito de fraude: cliente excedeu tanto o limite de pedidos (máx. 5) quanto o valor acumulado (R$ 1.500,00) na janela de 30 dias.",
        _ => "Status definido conforme regras de negócio."
    };
}
