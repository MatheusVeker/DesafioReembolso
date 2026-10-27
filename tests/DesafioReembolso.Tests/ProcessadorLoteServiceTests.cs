using FluentAssertions;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;
using DesafioReembolso.Core.Rules;
using DesafioReembolso.Core.Services;
using Xunit;

namespace DesafioReembolso.Tests;

public class ProcessadorLoteServiceTests
{
    private readonly ProcessadorLoteService _processador;
    private readonly DateOnly _dataReferencia = new(2024, 5, 10);

    public ProcessadorLoteServiceTests()
    {
        var calculadora = new CalculadoraReembolso();
        var regraPrazo = new RegraPrazo90Dias();
        var motorFraude = new MotorPrevencaoFraude();
        _processador = new ProcessadorLoteService(calculadora, regraPrazo, motorFraude);
    }

    [Fact]
    public void ProcessarLote_CenarioOficialDoEnunciado_DeveProduzirResultadoExato()
    {
        // Exemplo de entrada oficial:
        // 1,Consulta Médica,2024-02-10,400,123
        // 2,Exame de Imagem,2024-03-01,600,124
        // 3,Exame Laboratorial,2024-04-15,200,125
        // 4,Outros,2024-01-20,150,126
        var pedidos = new List<PedidoReembolso>
        {
            PedidoReembolso.Criar(1, "Consulta Médica", new DateOnly(2024, 2, 10), 400m, "123"),
            PedidoReembolso.Criar(2, "Exame de Imagem", new DateOnly(2024, 3, 1), 600m, "124"),
            PedidoReembolso.Criar(3, "Exame Laboratorial", new DateOnly(2024, 4, 15), 200m, "125"),
            PedidoReembolso.Criar(4, "Outros", new DateOnly(2024, 1, 20), 150m, "126")
        };

        var resultado = _processador.ProcessarLote(pedidos, _dataReferencia);

        resultado.Should().HaveCount(4);

        // Pedido 1: 400 * 80% = 320, Aprovado
        resultado[0].Id.Should().Be(1);
        resultado[0].TipoProcedimento.Should().Be("Consulta Médica");
        resultado[0].ValorPago.Should().Be(400m);
        resultado[0].ValorReembolsado.Should().Be(320m);
        resultado[0].ClienteId.Should().Be("123");
        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[0].Motivo.Should().Be(MotivoStatus.AprovadoComSucesso);

        // Pedido 2: 600 * 90% = 540 -> teto 500, Aprovado (limitado pelo teto)
        resultado[1].Id.Should().Be(2);
        resultado[1].TipoProcedimento.Should().Be("Exame de Imagem");
        resultado[1].ValorPago.Should().Be(600m);
        resultado[1].ValorReembolsado.Should().Be(500m);
        resultado[1].ClienteId.Should().Be("124");
        resultado[1].Status.Should().Be(StatusPedido.Aprovado);
        resultado[1].Motivo.Should().Be(MotivoStatus.AprovadoLimitadoPeloTeto);

        // Pedido 3: 200 * 70% = 140, Aprovado
        resultado[2].Id.Should().Be(3);
        resultado[2].TipoProcedimento.Should().Be("Exame Laboratorial");
        resultado[2].ValorPago.Should().Be(200m);
        resultado[2].ValorReembolsado.Should().Be(140m);
        resultado[2].ClienteId.Should().Be("125");
        resultado[2].Status.Should().Be(StatusPedido.Aprovado);
        resultado[2].Motivo.Should().Be(MotivoStatus.AprovadoComSucesso);

        // Pedido 4: 150 * 50% = 75, Rejeitado (fora do prazo de 90 dias)
        resultado[3].Id.Should().Be(4);
        resultado[3].TipoProcedimento.Should().Be("Outros");
        resultado[3].ValorPago.Should().Be(150m);
        resultado[3].ValorReembolsado.Should().Be(75m);
        resultado[3].ClienteId.Should().Be("126");
        resultado[3].Status.Should().Be(StatusPedido.Rejeitado);
        resultado[3].Motivo.Should().Be(MotivoStatus.ForaDoPrazo90Dias);
    }

    [Fact]
    public void ProcessarLote_MaisDeCincoPedidosParaMesmoClienteNaJanela_DeveMarcarExcedentesComoSuspeitoDeFraude()
    {
        var pedidos = new List<PedidoReembolso>();
        var dataJanela = new DateOnly(2024, 4, 25);

        for (int i = 1; i <= 7; i++)
        {
            pedidos.Add(PedidoReembolso.Criar(i, "Consulta Médica", dataJanela, 100m, "cliente-vip"));
        }

        var resultado = _processador.ProcessarLote(pedidos, _dataReferencia);

        resultado.Should().HaveCount(7);

        // Os primeiros 5 pedidos devem ser Aprovados
        for (int i = 0; i < 5; i++)
        {
            resultado[i].Status.Should().Be(StatusPedido.Aprovado);
            resultado[i].ValorReembolsado.Should().Be(80m);
        }

        // Os pedidos 6 e 7 devem ser marcados como Suspeito de Fraude
        resultado[5].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[5].Motivo.Should().Be(MotivoStatus.LimitePedidosClienteExcedido);

        resultado[6].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[6].Motivo.Should().Be(MotivoStatus.LimitePedidosClienteExcedido);
    }

    [Fact]
    public void ProcessarLote_TotalAcumuladoExcede1500_DeveMarcarComoSuspeitoDeFraude()
    {
        // 3 pedidos de Exame de Imagem no valor de 600 cada (reembolso teto 500 cada)
        // Pedido 1: reembolso 500 (total acumulado 500) -> Aprovado
        // Pedido 2: reembolso 500 (total acumulado 1000) -> Aprovado
        // Pedido 3: reembolso 500 (total acumulado 1500) -> Aprovado
        // Pedido 4: reembolso 100 (totalizaria 1600 > 1500) -> Suspeito de Fraude
        var dataJanela = new DateOnly(2024, 4, 20);
        var pedidos = new List<PedidoReembolso>
        {
            PedidoReembolso.Criar(1, "Exame de Imagem", dataJanela, 600m, "cliente-teto"),
            PedidoReembolso.Criar(2, "Exame de Imagem", dataJanela.AddDays(1), 600m, "cliente-teto"),
            PedidoReembolso.Criar(3, "Exame de Imagem", dataJanela.AddDays(2), 600m, "cliente-teto"),
            PedidoReembolso.Criar(4, "Consulta Médica", dataJanela.AddDays(3), 125m, "cliente-teto") // 100 reembolso
        };

        var resultado = _processador.ProcessarLote(pedidos, _dataReferencia);

        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[1].Status.Should().Be(StatusPedido.Aprovado);
        resultado[2].Status.Should().Be(StatusPedido.Aprovado);

        resultado[3].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[3].Motivo.Should().Be(MotivoStatus.LimiteValorClienteExcedido);
    }

    [Fact]
    public void ProcessarLote_PreservaOrdemOriginalDeEntrada_MesmoComDatasDiferentes()
    {
        // Entrada com pedido mais recente antes do mais antigo
        var pedidos = new List<PedidoReembolso>
        {
            PedidoReembolso.Criar(10, "Consulta Médica", new DateOnly(2024, 4, 30), 200m, "cliente-a"),
            PedidoReembolso.Criar(20, "Consulta Médica", new DateOnly(2024, 4, 10), 200m, "cliente-a"),
            PedidoReembolso.Criar(30, "Consulta Médica", new DateOnly(2024, 4, 20), 200m, "cliente-a")
        };

        var resultado = _processador.ProcessarLote(pedidos, _dataReferencia);

        // A ordem final de retorno deve seguir a ordem original da lista
        resultado[0].Id.Should().Be(10);
        resultado[1].Id.Should().Be(20);
        resultado[2].Id.Should().Be(30);
    }
}
