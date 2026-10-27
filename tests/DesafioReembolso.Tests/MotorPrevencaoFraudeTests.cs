using FluentAssertions;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;
using DesafioReembolso.Core.Rules;
using Xunit;

namespace DesafioReembolso.Tests;

public class MotorPrevencaoFraudeTests
{
    private readonly MotorPrevencaoFraude _motor = new();
    private readonly DateOnly _dataReferencia = new(2024, 5, 10);

    [Fact]
    public void PertenceAJanelaDeAnalise_DentroDosUltimos30Dias_DeveRetornarTrue()
    {
        // 2024-04-15 = 25 dias antes de 2024-05-10
        var data = new DateOnly(2024, 4, 15);
        _motor.PertenceAJanelaDeAnalise(data, _dataReferencia).Should().BeTrue();
    }

    [Fact]
    public void PertenceAJanelaDeAnalise_MaisDe30DiasAtras_DeveRetornarFalse()
    {
        // 2024-03-01 = 70 dias antes de 2024-05-10 (dentro dos 90 dias, mas fora da janela de 30 dias de fraude)
        var data = new DateOnly(2024, 3, 1);
        _motor.PertenceAJanelaDeAnalise(data, _dataReferencia).Should().BeFalse();
    }

    [Fact]
    public void AvaliarPedido_Ate5PedidosNaJanela30Dias_DeveAprovar()
    {
        var historico = new ClienteHistoricoLote();
        var dataDentro = new DateOnly(2024, 4, 20);

        for (int i = 1; i <= 5; i++)
        {
            var pedido = PedidoReembolso.Criar(i, "Consulta Médica", dataDentro, 100m, "cliente-1");
            var resultado = _motor.AvaliarPedido(pedido, 80m, _dataReferencia, historico);

            resultado.EhSuspeito.Should().BeFalse();
            resultado.Motivo.Should().Be(MotivoStatus.AprovadoComSucesso);

            historico.RegistrarAprovacao(80m);
        }

        historico.QuantidadePedidosJanela30Dias.Should().Be(5);
        historico.TotalReembolsadoJanela30Dias.Should().Be(400m);
    }

    [Fact]
    public void AvaliarPedido_SextoPedidoNaJanela30Dias_DeveMarcarComoSuspeitoDeFraude()
    {
        var historico = new ClienteHistoricoLote();
        var dataDentro = new DateOnly(2024, 4, 20);

        // Registra 5 pedidos prévios
        for (int i = 1; i <= 5; i++)
        {
            historico.RegistrarAprovacao(100m);
        }

        var sextoPedido = PedidoReembolso.Criar(6, "Consulta Médica", dataDentro, 100m, "cliente-1");
        var resultado = _motor.AvaliarPedido(sextoPedido, 80m, _dataReferencia, historico);

        resultado.EhSuspeito.Should().BeTrue();
        resultado.Motivo.Should().Be(MotivoStatus.LimitePedidosClienteExcedido);
    }

    [Fact]
    public void AvaliarPedido_ValorTotalUltrapassa1500_DeveMarcarComoSuspeitoDeFraude()
    {
        var historico = new ClienteHistoricoLote();
        var dataDentro = new DateOnly(2024, 4, 20);

        // Cliente já recebeu R$ 1.300 em reembolsos na janela
        historico.RegistrarAprovacao(1300m);

        // Novo pedido solicita R$ 300 de reembolso (totalizaria R$ 1.600 > R$ 1.500)
        var novoPedido = PedidoReembolso.Criar(2, "Consulta Médica", dataDentro, 375m, "cliente-1");
        var resultado = _motor.AvaliarPedido(novoPedido, 300m, _dataReferencia, historico);

        resultado.EhSuspeito.Should().BeTrue();
        resultado.Motivo.Should().Be(MotivoStatus.LimiteValorClienteExcedido);
    }

    [Fact]
    public void AvaliarPedido_UltrapassaLimiteDePedidosEValorSimultaneamente_DeveIndicarAmbos()
    {
        var historico = new ClienteHistoricoLote();
        var dataDentro = new DateOnly(2024, 4, 20);

        // Cliente já tem 5 pedidos e R$ 1.450
        for (int i = 1; i <= 5; i++)
        {
            historico.RegistrarAprovacao(290m);
        }

        // Sexto pedido pedindo R$ 100 (ultrapassa 5 pedidos E ultrapassa R$ 1.500)
        var sextoPedido = PedidoReembolso.Criar(6, "Consulta Médica", dataDentro, 125m, "cliente-1");
        var resultado = _motor.AvaliarPedido(sextoPedido, 100m, _dataReferencia, historico);

        resultado.EhSuspeito.Should().BeTrue();
        resultado.Motivo.Should().Be(MotivoStatus.LimitePedidosEValorExcedidos);
    }

    [Fact]
    public void AvaliarPedido_ForaDaJanela30Dias_NaoDeveSofrerRestricoesDeFraude()
    {
        var historico = new ClienteHistoricoLote();
        // Cliente já atingiu o limite de pedidos na janela de 30 dias
        for (int i = 1; i <= 5; i++)
        {
            historico.RegistrarAprovacao(300m);
        }

        // Pedido realizado há 60 dias (dentro dos 90 dias, mas fora dos 30 dias)
        var dataForaJanela30 = new DateOnly(2024, 3, 10);
        var pedido = PedidoReembolso.Criar(10, "Exame de Imagem", dataForaJanela30, 400m, "cliente-1");

        var resultado = _motor.AvaliarPedido(pedido, 360m, _dataReferencia, historico);

        resultado.EhSuspeito.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoStatus.AprovadoComSucesso);
    }
}
