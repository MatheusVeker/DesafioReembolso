using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Rules;
using DesafioReembolso.Core.Services;
using FluentAssertions;
using Xunit;

namespace DesafioReembolso.Tests;

public class CincoRegrasDeNegocioTests
{
    private readonly ProcessadorLoteService _processador;
    private readonly CsvService _csvService;
    private readonly DateOnly _dataReferenciaPadrao = new(2024, 5, 10);

    public CincoRegrasDeNegocioTests()
    {
        var calculadora = new CalculadoraReembolso();
        var regraPrazo = new RegraPrazo90Dias();
        var motorFraude = new MotorPrevencaoFraude();

        _processador = new ProcessadorLoteService(calculadora, regraPrazo, motorFraude);
        _csvService = new CsvService();
    }

    [Fact]
    public void Regra1_DeveAplicarAliquotaCorretaParaCadaTipoDeProcedimento()
    {
        // Cenário: Consulta (80%), Imagem (90%), Laboratorial (70%), Outros (50%)
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-04-10,100,CLIENTE_01
2,Exame de Imagem,2024-04-10,200,CLIENTE_02
3,Exame Laboratorial,2024-04-10,100,CLIENTE_03
4,Outros,2024-04-10,200,CLIENTE_04";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);
        erros.Should().BeEmpty();

        var resultado = _processador.ProcessarLote(pedidos, _dataReferenciaPadrao);

        // Asserções
        resultado.Should().HaveCount(4);

        // 1. Consulta Médica: 100 * 80% = 80
        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[0].ValorReembolsado.Should().Be(80.00m);

        // 2. Exame de Imagem: 200 * 90% = 180
        resultado[1].Status.Should().Be(StatusPedido.Aprovado);
        resultado[1].ValorReembolsado.Should().Be(180.00m);

        // 3. Exame Laboratorial: 100 * 70% = 70
        resultado[2].Status.Should().Be(StatusPedido.Aprovado);
        resultado[2].ValorReembolsado.Should().Be(70.00m);

        // 4. Outros: 200 * 50% = 100
        resultado[3].Status.Should().Be(StatusPedido.Aprovado);
        resultado[3].ValorReembolsado.Should().Be(100.00m);
    }

    [Fact]
    public void Regra2_DeveLimitarReembolsoAoTetoDe500ReaisPorProcedimento()
    {
        // Cenário: Pedidos cujo valor calculado excede R$ 500,00
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Exame de Imagem,2024-04-10,1000,CLIENTE_TETO_1
2,Consulta Médica,2024-04-10,800,CLIENTE_TETO_2
3,Exame Laboratorial,2024-04-10,1000,CLIENTE_TETO_3
4,Outros,2024-04-10,1500,CLIENTE_TETO_4";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);
        erros.Should().BeEmpty();

        var resultado = _processador.ProcessarLote(pedidos, _dataReferenciaPadrao);

        resultado.Should().HaveCount(4);

        // 1. 1000 * 90% = 900 -> limitado a 500
        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[0].ValorReembolsado.Should().Be(500.00m);
        resultado[0].Motivo.Should().Be(MotivoStatus.AprovadoLimitadoPeloTeto);

        // 2. 800 * 80% = 640 -> limitado a 500
        resultado[1].Status.Should().Be(StatusPedido.Aprovado);
        resultado[1].ValorReembolsado.Should().Be(500.00m);
        resultado[1].Motivo.Should().Be(MotivoStatus.AprovadoLimitadoPeloTeto);

        // 3. 1000 * 70% = 700 -> limitado a 500
        resultado[2].Status.Should().Be(StatusPedido.Aprovado);
        resultado[2].ValorReembolsado.Should().Be(500.00m);
        resultado[2].Motivo.Should().Be(MotivoStatus.AprovadoLimitadoPeloTeto);

        // 4. 1500 * 50% = 750 -> limitado a 500
        resultado[3].Status.Should().Be(StatusPedido.Aprovado);
        resultado[3].ValorReembolsado.Should().Be(500.00m);
        resultado[3].Motivo.Should().Be(MotivoStatus.AprovadoLimitadoPeloTeto);
    }

    [Fact]
    public void Regra3_DeveRejeitarPedidosComMaisDe90DiasOuDatasFuturas()
    {
        // Cenário com data de referência: 2024-05-10
        // 90 dias exatos atrás: 2024-02-10 (Aprovado)
        // 91 dias atrás: 2024-02-09 (Rejeitado)
        // 126 dias atrás: 2024-01-05 (Rejeitado)
        // Data futura: 2024-05-20 (Rejeitado)
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-02-10,300,CLIENTE_PRAZO_1
2,Consulta Médica,2024-02-09,300,CLIENTE_PRAZO_2
3,Exame de Imagem,2024-01-05,500,CLIENTE_PRAZO_3
4,Exame Laboratorial,2024-05-20,200,CLIENTE_PRAZO_4";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);
        erros.Should().BeEmpty();

        var resultado = _processador.ProcessarLote(pedidos, _dataReferenciaPadrao);

        resultado.Should().HaveCount(4);

        // Pedido 1: 2024-02-10 está exatamente no limite de 90 dias -> Aprovado
        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[0].ValorReembolsado.Should().Be(240.00m);

        // Pedido 2: 2024-02-09 tem 91 dias -> Rejeitado
        resultado[1].Status.Should().Be(StatusPedido.Rejeitado);
        resultado[1].Motivo.Should().Be(MotivoStatus.ForaDoPrazo90Dias);
        resultado[1].ValorReembolsado.Should().Be(240.00m); // exibe o valor calculado para auditoria

        // Pedido 3: 2024-01-05 tem 126 dias -> Rejeitado
        resultado[2].Status.Should().Be(StatusPedido.Rejeitado);
        resultado[2].Motivo.Should().Be(MotivoStatus.ForaDoPrazo90Dias);

        // Pedido 4: 2024-05-20 é posterior a 2024-05-10 -> Rejeitado
        resultado[3].Status.Should().Be(StatusPedido.Rejeitado);
        resultado[3].Motivo.Should().Be(MotivoStatus.ForaDoPrazo90Dias);
    }

    [Fact]
    public void Regra4_DeveMarcarComoSuspeitoDeFraudeApartirDoSextoPedidoEm30Dias()
    {
        // Cenário: 7 pedidos do mesmo cliente na janela de 30 dias (2024-04-10 a 2024-05-10)
        // Valores baixos para não estourar a regra de valor de R$ 1.500
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-04-12,50,CLIENTE_FRAUDE_QTD
2,Consulta Médica,2024-04-15,50,CLIENTE_FRAUDE_QTD
3,Consulta Médica,2024-04-18,50,CLIENTE_FRAUDE_QTD
4,Consulta Médica,2024-04-20,50,CLIENTE_FRAUDE_QTD
5,Consulta Médica,2024-04-25,50,CLIENTE_FRAUDE_QTD
6,Consulta Médica,2024-04-28,50,CLIENTE_FRAUDE_QTD
7,Consulta Médica,2024-05-02,50,CLIENTE_FRAUDE_QTD";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);
        erros.Should().BeEmpty();

        var resultado = _processador.ProcessarLote(pedidos, _dataReferenciaPadrao);

        resultado.Should().HaveCount(7);

        // Pedidos 1 a 5 devem ser APROVADOS (limite máximo de 5 dentro da janela)
        for (int i = 0; i < 5; i++)
        {
            resultado[i].Status.Should().Be(StatusPedido.Aprovado, $"Pedido #{i + 1} está dentro da cota de 5");
            resultado[i].ValorReembolsado.Should().Be(40.00m);
        }

        // Pedidos 6 e 7 devem ser marcados como SUSPEITO DE FRAUDE
        resultado[5].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[5].Motivo.Should().Be(MotivoStatus.LimitePedidosClienteExcedido);

        resultado[6].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[6].Motivo.Should().Be(MotivoStatus.LimitePedidosClienteExcedido);
    }

    [Fact]
    public void Regra5_DeveMarcarComoSuspeitoDeFraudeQuandoUltrapassarTetoDe1500ReaisEm30Dias()
    {
        // Cenário: Menos de 5 pedidos do mesmo cliente, mas que ultrapassam R$ 1.500,00 na janela de 30 dias
        // Pedido 1: Imagem R$ 600 -> teto R$ 500 (Aprovado, saldo acumulado: R$ 500)
        // Pedido 2: Consulta R$ 700 -> teto R$ 500 (Aprovado, saldo acumulado: R$ 1000)
        // Pedido 3: Consulta R$ 600 -> 80% = R$ 480 (Aprovado, saldo acumulado: R$ 1480)
        // Pedido 4: Laboratorial R$ 100 -> 70% = R$ 70 (1480 + 70 = 1550 > 1500 -> Suspeito de Fraude)
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Exame de Imagem,2024-04-12,600,CLIENTE_FRAUDE_VALOR
2,Consulta Médica,2024-04-18,700,CLIENTE_FRAUDE_VALOR
3,Consulta Médica,2024-04-25,600,CLIENTE_FRAUDE_VALOR
4,Exame Laboratorial,2024-05-02,100,CLIENTE_FRAUDE_VALOR";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);
        erros.Should().BeEmpty();

        var resultado = _processador.ProcessarLote(pedidos, _dataReferenciaPadrao);

        resultado.Should().HaveCount(4);

        // Pedido 1: Aprovado com R$ 500
        resultado[0].Status.Should().Be(StatusPedido.Aprovado);
        resultado[0].ValorReembolsado.Should().Be(500.00m);

        // Pedido 2: Aprovado com R$ 500
        resultado[1].Status.Should().Be(StatusPedido.Aprovado);
        resultado[1].ValorReembolsado.Should().Be(500.00m);

        // Pedido 3: Aprovado com R$ 480 (total acumulado R$ 1.480 <= R$ 1.500)
        resultado[2].Status.Should().Be(StatusPedido.Aprovado);
        resultado[2].ValorReembolsado.Should().Be(480.00m);

        // Pedido 4: Ultrapassa o teto de R$ 1.500 acumulado -> Suspeito de Fraude
        resultado[3].Status.Should().Be(StatusPedido.SuspeitoDeFraude);
        resultado[3].Motivo.Should().Be(MotivoStatus.LimiteValorClienteExcedido);
        resultado[3].ValorReembolsado.Should().Be(70.00m); // exibe o valor calculado para auditoria
    }
}
