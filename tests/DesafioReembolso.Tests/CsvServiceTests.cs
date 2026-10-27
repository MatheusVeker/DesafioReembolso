using FluentAssertions;
using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;
using Xunit;

namespace DesafioReembolso.Tests;

public class CsvServiceTests
{
    private readonly CsvService _csvService = new();

    [Fact]
    public void FazerParseCsv_ExemploOficial_DeveParsearComSucesso()
    {
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-02-10,400,123
2,Exame de Imagem,2024-03-01,600,124
3,Exame Laboratorial,2024-04-15,200,125
4,Outros,2024-01-20,150,126";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);

        erros.Should().BeEmpty();
        pedidos.Should().HaveCount(4);

        pedidos[0].Id.Should().Be(1);
        pedidos[0].TipoProcedimento.Should().Be(TipoProcedimento.ConsultaMedica);
        pedidos[0].DataProcedimento.Should().Be(new DateOnly(2024, 2, 10));
        pedidos[0].ValorPago.Should().Be(400m);
        pedidos[0].ClienteId.Should().Be("123");
    }

    [Fact]
    public void FazerParseCsv_ColunasEmOrdemDiferenteEComEspacos_DeveParsearComSucesso()
    {
        var csv = @"ClienteId, DataProcedimento, TipoProcedimento, ValorPago, Id
123, 2024-02-10, Consulta Médica, 400.50, 1";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);

        erros.Should().BeEmpty();
        pedidos.Should().HaveCount(1);
        pedidos[0].Id.Should().Be(1);
        pedidos[0].ValorPago.Should().Be(400.50m);
        pedidos[0].ClienteId.Should().Be("123");
    }

    [Fact]
    public void FazerParseCsv_CabecalhoSemColunaObrigatoria_DeveRetornarErroExplicativo()
    {
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago
1,Consulta Médica,2024-02-10,400";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);

        pedidos.Should().BeEmpty();
        erros.Should().ContainSingle()
            .Which.Should().Contain("clienteid");
    }

    [Fact]
    public void FazerParseCsv_LinhaComDataInvalida_DeveIdentificarNumeroDaLinhaEErro()
    {
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,data-invalida,400,123";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);

        pedidos.Should().BeEmpty();
        erros.Should().ContainSingle()
            .Which.Should().Contain("Linha 2").And.Contain("data-invalida");
    }

    [Fact]
    public void FazerParseCsv_LinhaComValorNegativo_DeveIdentificarErro()
    {
        var csv = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-02-10,-50,123";

        var (pedidos, erros) = _csvService.FazerParseCsv(csv);

        pedidos.Should().BeEmpty();
        erros.Should().ContainSingle()
            .Which.Should().Contain("Linha 2").And.Contain("-50");
    }

    [Fact]
    public void GerarCsv_ListaDeProcessados_DeveProduzirCabecalhoEFormatoEsperado()
    {
        var processados = new List<PedidoProcessado>
        {
            new()
            {
                Id = 1,
                TipoProcedimento = "Consulta Médica",
                DataProcedimento = new DateOnly(2024, 2, 10),
                ValorPago = 400m,
                ValorReembolsado = 320m,
                ClienteId = "123",
                Status = StatusPedido.Aprovado
            },
            new()
            {
                Id = 4,
                TipoProcedimento = "Outros",
                DataProcedimento = new DateOnly(2024, 1, 20),
                ValorPago = 150m,
                ValorReembolsado = 75m,
                ClienteId = "126",
                Status = StatusPedido.Rejeitado
            }
        };

        var resultadoCsv = _csvService.GerarCsv(processados);

        var linhas = resultadoCsv.Trim().Split(Environment.NewLine);
        linhas[0].Should().Be("Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status");
        linhas[1].Should().Be("1,Consulta Médica,2024-02-10,400,320,123,Aprovado");
        linhas[2].Should().Be("4,Outros,2024-01-20,150,75,126,Rejeitado");
    }
}
