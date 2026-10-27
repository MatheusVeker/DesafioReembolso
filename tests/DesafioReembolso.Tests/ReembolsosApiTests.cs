using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using DesafioReembolso.Application.DTOs;
using Xunit;

namespace DesafioReembolso.Tests;

public class ReembolsosApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ReembolsosApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRegras_DeveRetornar200ComInformacoesDasRegras()
    {
        var response = await _client.GetAsync("/api/reembolsos/regras");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Consulta Médica");
        content.Should().Contain("80%");
        content.Should().Contain("500");
    }

    [Fact]
    public async Task ProcessarLoteJson_ComLoteValido_DeveRetornar200ComResultados()
    {
        var request = new ProcessarLoteRequest
        {
            DataReferencia = "2024-05-10",
            Pedidos = new List<PedidoInputDto>
            {
                new() { Id = 1, TipoProcedimento = "Consulta Médica", DataProcedimento = "2024-02-10", ValorPago = 400m, ClienteId = "123" },
                new() { Id = 2, TipoProcedimento = "Exame de Imagem", DataProcedimento = "2024-03-01", ValorPago = 600m, ClienteId = "124" },
                new() { Id = 3, TipoProcedimento = "Exame Laboratorial", DataProcedimento = "2024-04-15", ValorPago = 200m, ClienteId = "125" },
                new() { Id = 4, TipoProcedimento = "Outros", DataProcedimento = "2024-01-20", ValorPago = 150m, ClienteId = "126" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/reembolsos/processar-lote", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessarLoteResponse>();

        body.Should().NotBeNull();
        body!.TotalProcessados.Should().Be(4);
        body.TotalAprovados.Should().Be(3);
        body.TotalRejeitados.Should().Be(1);
        body.TotalSuspeitos.Should().Be(0);
        body.ValorTotalReembolsadoAprovado.Should().Be(960.00m); // 320 + 500 + 140

        body.Pedidos[0].ValorReembolsado.Should().Be(320m);
        body.Pedidos[0].Status.Should().Be("Aprovado");

        body.Pedidos[1].ValorReembolsado.Should().Be(500m);
        body.Pedidos[1].Status.Should().Be("Aprovado");

        body.Pedidos[2].ValorReembolsado.Should().Be(140m);
        body.Pedidos[2].Status.Should().Be("Aprovado");

        body.Pedidos[3].ValorReembolsado.Should().Be(75m);
        body.Pedidos[3].Status.Should().Be("Rejeitado");
    }

    [Fact]
    public async Task ProcessarLoteJson_ComDadosInvalidos_DeveRetornar400ComDetalhes()
    {
        var request = new ProcessarLoteRequest
        {
            DataReferencia = "data-invalida",
            Pedidos = new List<PedidoInputDto>
            {
                new() { Id = -1, TipoProcedimento = "", DataProcedimento = "invalid", ValorPago = -10, ClienteId = "" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/reembolsos/processar-lote", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Data de referência inválida");
    }

    [Fact]
    public async Task ProcessarCsv_ComArquivoValido_DeveRetornar200()
    {
        var csvContent = @"Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-02-10,400,123
2,Exame de Imagem,2024-03-01,600,124";

        using var multipartContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        multipartContent.Add(fileContent, "arquivo", "pedidos.csv");
        multipartContent.Add(new StringContent("2024-05-10"), "dataReferencia");

        var response = await _client.PostAsync("/api/reembolsos/processar-csv", multipartContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessarLoteResponse>();

        body.Should().NotBeNull();
        body!.TotalProcessados.Should().Be(2);
        body.TotalAprovados.Should().Be(2);
    }

    [Fact]
    public async Task ProcessarCsv_ComCsvInvalido_DeveRetornar400()
    {
        var csvInvalido = @"Id,TipoProcedimento,ValorPago
1,Consulta Médica,400"; // colunas obrigatórias ausentes

        using var multipartContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvInvalido));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        multipartContent.Add(fileContent, "arquivo", "invalido.csv");

        var response = await _client.PostAsync("/api/reembolsos/processar-csv", multipartContent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Erros encontrados ao interpretar o arquivo CSV");
    }

    [Fact]
    public async Task ExportarCsv_ComListaProcessada_DeveRetornarArquivoCsv()
    {
        var pedidos = new List<PedidoOutputDto>
        {
            new()
            {
                Id = 1,
                TipoProcedimento = "Consulta Médica",
                DataProcedimento = "2024-02-10",
                ValorPago = 400m,
                ValorReembolsado = 320m,
                ClienteId = "123",
                Status = "Aprovado"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/reembolsos/exportar-csv", pedidos);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status");
        csv.Should().Contain("1,Consulta Médica,2024-02-10,400,320,123,Aprovado");
    }

    [Fact]
    public async Task ProcessarLote_ComPedidosRejeitadosESuspeitos_DeveProcessarERegistrarAuditoriaComSucesso()
    {
        var dataRef = "2024-05-10";
        var request = new ProcessarLoteRequest
        {
            DataReferencia = dataRef,
            Pedidos = new List<PedidoInputDto>
            {
                // Pedidos dentro da janela de 30 dias para mesmo cliente que acumulam > R$ 1.500
                new() { Id = 1, TipoProcedimento = "Exame de Imagem", DataProcedimento = "2024-04-20", ValorPago = 600m, ClienteId = "cliente-auditoria" },
                new() { Id = 2, TipoProcedimento = "Exame de Imagem", DataProcedimento = "2024-04-21", ValorPago = 600m, ClienteId = "cliente-auditoria" },
                new() { Id = 3, TipoProcedimento = "Exame de Imagem", DataProcedimento = "2024-04-22", ValorPago = 600m, ClienteId = "cliente-auditoria" },
                new() { Id = 4, TipoProcedimento = "Consulta Médica", DataProcedimento = "2024-04-23", ValorPago = 200m, ClienteId = "cliente-auditoria" },
                // Pedido fora do prazo (>90 dias)
                new() { Id = 5, TipoProcedimento = "Outros", DataProcedimento = "2023-10-15", ValorPago = 300m, ClienteId = "cliente-antigo" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/reembolsos/processar-lote", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProcessarLoteResponse>();

        body.Should().NotBeNull();
        body!.TotalProcessados.Should().Be(5);
        body.TotalAprovados.Should().Be(3);
        body.TotalSuspeitos.Should().Be(1);
        body.TotalRejeitados.Should().Be(1);

        body.Pedidos.First(p => p.Id == 4).Status.Should().Be("Suspeito de Fraude");
        body.Pedidos.First(p => p.Id == 5).Status.Should().Be("Rejeitado");
    }
}
