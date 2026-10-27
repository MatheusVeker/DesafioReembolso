using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;
using DesafioReembolso.Core.Rules;

namespace DesafioReembolso.Core.Services;

public sealed class ProcessadorLoteService : IProcessadorLoteService
{
    private readonly ICalculadoraReembolso _calculadora;
    private readonly IRegraPrazo _regraPrazo;
    private readonly IMotorPrevencaoFraude _motorFraude;

    public ProcessadorLoteService(
        ICalculadoraReembolso calculadora,
        IRegraPrazo regraPrazo,
        IMotorPrevencaoFraude motorFraude)
    {
        _calculadora = calculadora ?? throw new ArgumentNullException(nameof(calculadora));
        _regraPrazo = regraPrazo ?? throw new ArgumentNullException(nameof(regraPrazo));
        _motorFraude = motorFraude ?? throw new ArgumentNullException(nameof(motorFraude));
    }

    public IReadOnlyList<PedidoProcessado> ProcessarLote(
        IEnumerable<PedidoReembolso> pedidos,
        DateOnly dataReferencia)
    {
        if (pedidos is null)
        {
            return Array.Empty<PedidoProcessado>();
        }

        // Preserva o índice original de entrada para restaurar a ordem no resultado final
        var pedidosComIndice = pedidos
            .Select((pedido, index) => (Pedido: pedido, IndiceOriginal: index))
            .ToList();

        if (pedidosComIndice.Count == 0)
        {
            return Array.Empty<PedidoProcessado>();
        }

        // Ordenação determinística para avaliação de regras de fraude cumulativas:
        // Primeiro por data do procedimento (cronológico), depois pelo índice original / Id
        var pedidosOrdenadosParaAnalise = pedidosComIndice
            .OrderBy(p => p.Pedido.DataProcedimento)
            .ThenBy(p => p.IndiceOriginal)
            .ToList();

        var historicosClientes = new Dictionary<string, ClienteHistoricoLote>(StringComparer.OrdinalIgnoreCase);
        var resultadosPorIndice = new (int IndiceOriginal, PedidoProcessado Resultado)[pedidosComIndice.Count];

        foreach (var item in pedidosOrdenadosParaAnalise)
        {
            var pedido = item.Pedido;
            PedidoProcessado processado;

            // 1. Verificação de prazo de 90 dias (Fail-Fast)
            if (!_regraPrazo.EstaDentroDoPrazo(pedido.DataProcedimento, dataReferencia))
            {
                // Fora do prazo: Rejeitado.
                // O valor calculado é mantido para fins de auditoria e conformidade com o exemplo oficial,
                // mas não é submetido à fraude nem consome cotas do cliente.
                var valorReembolsoTeorico = _calculadora.CalcularValorReembolsado(pedido.TipoProcedimento, pedido.ValorPago);

                processado = new PedidoProcessado
                {
                    Id = pedido.Id,
                    TipoProcedimento = pedido.TipoProcedimento.ObterDescricao(),
                    DataProcedimento = pedido.DataProcedimento,
                    ValorPago = pedido.ValorPago,
                    ValorReembolsado = valorReembolsoTeorico,
                    ClienteId = pedido.ClienteId,
                    Status = StatusPedido.Rejeitado,
                    Motivo = MotivoStatus.ForaDoPrazo90Dias
                };
            }
            else
            {
                // 2. Pré-cálculo da alíquota e verificação de teto para alimentar o motor antifraude
                var calculo = _calculadora.Calcular(pedido.TipoProcedimento, pedido.ValorPago);
                var valorReembolsado = calculo.ValorReembolsado;

                // 3. Obter histórico acumulado do cliente para o lote atual
                if (!historicosClientes.TryGetValue(pedido.ClienteId, out var historico))
                {
                    historico = new ClienteHistoricoLote();
                    historicosClientes[pedido.ClienteId] = historico;
                }

                // 4. Avaliação do Motor de Prevenção a Fraudes (janela de 30 dias: >5 pedidos OU >R$ 1.500)
                var resultadoFraude = _motorFraude.AvaliarPedido(
                    pedido,
                    valorReembolsado,
                    dataReferencia,
                    historico);

                if (resultadoFraude.EhSuspeito)
                {
                    processado = new PedidoProcessado
                    {
                        Id = pedido.Id,
                        TipoProcedimento = pedido.TipoProcedimento.ObterDescricao(),
                        DataProcedimento = pedido.DataProcedimento,
                        ValorPago = pedido.ValorPago,
                        ValorReembolsado = valorReembolsado,
                        ClienteId = pedido.ClienteId,
                        Status = StatusPedido.SuspeitoDeFraude,
                        Motivo = resultadoFraude.Motivo
                    };
                }
                else
                {
                    // 5. Aprovação: discrimina entre aprovação integral ou limitada pelo teto de R$ 500
                    var motivoAprovacao = calculo.AtingiuTetoMaximo
                        ? MotivoStatus.AprovadoLimitadoPeloTeto
                        : MotivoStatus.AprovadoComSucesso;

                    processado = new PedidoProcessado
                    {
                        Id = pedido.Id,
                        TipoProcedimento = pedido.TipoProcedimento.ObterDescricao(),
                        DataProcedimento = pedido.DataProcedimento,
                        ValorPago = pedido.ValorPago,
                        ValorReembolsado = valorReembolsado,
                        ClienteId = pedido.ClienteId,
                        Status = StatusPedido.Aprovado,
                        Motivo = motivoAprovacao
                    };

                    // Se o procedimento ocorreu dentro da janela de 30 dias, registra no histórico do cliente
                    if (_motorFraude.PertenceAJanelaDeAnalise(pedido.DataProcedimento, dataReferencia))
                    {
                        historico.RegistrarAprovacao(valorReembolsado);
                    }
                }
            }

            resultadosPorIndice[item.IndiceOriginal] = (item.IndiceOriginal, processado);
        }

        // Restaura a ordem exata de entrada dos dados para a saída
        return resultadosPorIndice
            .OrderBy(r => r.IndiceOriginal)
            .Select(r => r.Resultado)
            .ToList();
    }
}
