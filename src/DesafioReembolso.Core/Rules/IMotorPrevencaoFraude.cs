using DesafioReembolso.Core.Models;

namespace DesafioReembolso.Core.Rules;

public interface IMotorPrevencaoFraude
{
    int DiasJanelaFraude { get; }
    int LimiteMaximoPedidosPorCliente { get; }
    decimal LimiteMaximoValorPorCliente { get; }

    bool PertenceAJanelaDeAnalise(DateOnly dataProcedimento, DateOnly dataReferencia);

    ResultadoAvaliacaoFraude AvaliarPedido(
        PedidoReembolso pedido,
        decimal valorReembolsado,
        DateOnly dataReferencia,
        ClienteHistoricoLote historicoCliente);
}
