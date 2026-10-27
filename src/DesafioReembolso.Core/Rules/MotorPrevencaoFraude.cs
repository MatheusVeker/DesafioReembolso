using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;

namespace DesafioReembolso.Core.Rules;

public sealed class MotorPrevencaoFraude : IMotorPrevencaoFraude
{
    public const int JanelaPadraoDias = 30;
    public const int LimitePadraoPedidos = 5;
    public const decimal LimitePadraoValor = 1500.00m;

    public int DiasJanelaFraude { get; }
    public int LimiteMaximoPedidosPorCliente { get; }
    public decimal LimiteMaximoValorPorCliente { get; }

    public MotorPrevencaoFraude(
        int diasJanela = JanelaPadraoDias,
        int limitePedidos = LimitePadraoPedidos,
        decimal limiteValor = LimitePadraoValor)
    {
        DiasJanelaFraude = diasJanela;
        LimiteMaximoPedidosPorCliente = limitePedidos;
        LimiteMaximoValorPorCliente = limiteValor;
    }

    public bool PertenceAJanelaDeAnalise(DateOnly dataProcedimento, DateOnly dataReferencia)
    {
        var limiteInferior = dataReferencia.AddDays(-DiasJanelaFraude);
        return dataProcedimento >= limiteInferior && dataProcedimento <= dataReferencia;
    }

    public ResultadoAvaliacaoFraude AvaliarPedido(
        PedidoReembolso pedido,
        decimal valorReembolsado,
        DateOnly dataReferencia,
        ClienteHistoricoLote historicoCliente)
    {
        // Se o pedido não está na janela de 30 dias (ex: está entre 31 e 90 dias),
        // ele não consome nem viola as restrições da janela de 30 dias.
        if (!PertenceAJanelaDeAnalise(pedido.DataProcedimento, dataReferencia))
        {
            return ResultadoAvaliacaoFraude.Aprovado();
        }

        var excedeuPedidos = historicoCliente.QuantidadePedidosJanela30Dias >= LimiteMaximoPedidosPorCliente;
        var excedeuValor = (historicoCliente.TotalReembolsadoJanela30Dias + valorReembolsado) > LimiteMaximoValorPorCliente;

        if (excedeuPedidos && excedeuValor)
        {
            return ResultadoAvaliacaoFraude.Suspeito(MotivoStatus.LimitePedidosEValorExcedidos);
        }

        if (excedeuPedidos)
        {
            return ResultadoAvaliacaoFraude.Suspeito(MotivoStatus.LimitePedidosClienteExcedido);
        }

        if (excedeuValor)
        {
            return ResultadoAvaliacaoFraude.Suspeito(MotivoStatus.LimiteValorClienteExcedido);
        }

        return ResultadoAvaliacaoFraude.Aprovado();
    }
}
