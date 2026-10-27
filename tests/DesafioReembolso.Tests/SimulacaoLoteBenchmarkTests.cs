using System.Diagnostics;
using System.Text;
using DesafioReembolso.Application.DTOs;
using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Rules;
using DesafioReembolso.Core.Services;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DesafioReembolso.Tests;

public class SimulacaoLoteBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public SimulacaoLoteBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SimularEntrada10000Pedidos_ProcessarETrazerMetricasDetalhadas()
    {
        // 1. Inicializar serviços
        var calculadora = new CalculadoraReembolso();
        var regraPrazo = new RegraPrazo90Dias();
        var motorFraude = new MotorPrevencaoFraude();
        var processador = new ProcessadorLoteService(calculadora, regraPrazo, motorFraude);
        var csvService = new CsvService();

        var dataReferencia = new DateOnly(2024, 5, 10);
        const int totalPedidos = 10_000;
        var random = new Random(42); // Seed fixa para reprodutibilidade

        var tipos = new[]
        {
            TipoProcedimento.ConsultaMedica,
            TipoProcedimento.ExameImagem,
            TipoProcedimento.ExameLaboratorial,
            TipoProcedimento.Outros
        };

        // 2. Gerar massa de 10.000 pedidos com distribuição realista
        var swGeracao = Stopwatch.StartNew();
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId");

        for (int i = 1; i <= totalPedidos; i++)
        {
            var tipo = tipos[random.Next(tipos.Length)].ObterDescricao();
            
            // 75% dentro dos 90 dias, 25% mais velhos (> 90 dias)
            bool foraDoPrazo = random.NextDouble() < 0.25;
            int diasAtras = foraDoPrazo ? random.Next(91, 180) : random.Next(0, 91);
            var dataProcedimento = dataReferencia.AddDays(-diasAtras).ToString("yyyy-MM-dd");

            // Valores de R$ 50 a R$ 2.000
            decimal valorPago = Math.Round((decimal)(random.Next(50, 1950) + random.NextDouble()), 2);

            // Distribuição de clientes: alguns normais, outros concentrando pedidos para testar fraudes
            string clienteId;
            if (i % 12 == 0)
            {
                // Clientes concentrados (muitos pedidos na janela de 30 dias para disparar limite de 5 pedidos / R$ 1500)
                clienteId = $"CLIENTE_RECORRENTE_{random.Next(1, 35)}";
            }
            else
            {
                // Clientes dispersos (~2.500 clientes diferentes)
                clienteId = $"CLIENTE_{random.Next(100, 2600)}";
            }

            csvBuilder.AppendLine($"{i},{tipo},{dataProcedimento},{valorPago.ToString(System.Globalization.CultureInfo.InvariantCulture)},{clienteId}");
        }
        swGeracao.Stop();

        var csvEntrada = csvBuilder.ToString();
        var pastaExemplos = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "exemplos");
        if (Directory.Exists(pastaExemplos))
        {
            File.WriteAllText(Path.Combine(pastaExemplos, "pedidos_10000.csv"), csvEntrada, Encoding.UTF8);
        }

        // 3. Medição de Desempenho do Parse CSV
        var swParse = Stopwatch.StartNew();
        var (pedidosInput, errosParse) = csvService.FazerParseCsv(csvEntrada);
        swParse.Stop();

        errosParse.Should().BeEmpty();
        pedidosInput.Should().HaveCount(totalPedidos);

        // 4. Medição de Desempenho do Processamento de Regras do Core
        var swCore = Stopwatch.StartNew();
        var resultadoProcessado = processador.ProcessarLote(pedidosInput, dataReferencia);
        swCore.Stop();

        // 5. Medição de Desempenho da Escrita/Geração do CSV de Saída
        var swEscrita = Stopwatch.StartNew();
        var stringWriter = new StringWriter();
        csvService.EscreverCsv(resultadoProcessado, stringWriter);
        swEscrita.Stop();

        var tempoTotalMs = swParse.ElapsedMilliseconds + swCore.ElapsedMilliseconds + swEscrita.ElapsedMilliseconds;
        var throughputGeral = Math.Round(totalPedidos / (tempoTotalMs / 1000.0), 2);
        var throughputCorePuro = Math.Round(totalPedidos / (swCore.Elapsed.TotalSeconds), 2);

        // 6. Cálculo das Métricas de Negócio
        int totalAprovados = resultadoProcessado.Count(p => p.Status == StatusPedido.Aprovado);
        int totalAprovadosIntegral = resultadoProcessado.Count(p => p.Motivo == MotivoStatus.AprovadoComSucesso);
        int totalAprovadosComTeto = resultadoProcessado.Count(p => p.Motivo == MotivoStatus.AprovadoLimitadoPeloTeto);

        int totalRejeitados = resultadoProcessado.Count(p => p.Status == StatusPedido.Rejeitado);
        int totalSuspeitos = resultadoProcessado.Count(p => p.Status == StatusPedido.SuspeitoDeFraude);

        int suspeitoPorQtd = resultadoProcessado.Count(p => p.Motivo == MotivoStatus.LimitePedidosClienteExcedido);
        int suspeitoPorValor = resultadoProcessado.Count(p => p.Motivo == MotivoStatus.LimiteValorClienteExcedido);
        int suspeitoPorAmbos = resultadoProcessado.Count(p => p.Motivo == MotivoStatus.LimitePedidosEValorExcedidos);

        decimal valorTotalPago = resultadoProcessado.Sum(p => p.ValorPago);
        decimal valorTotalReembolsadoCalculado = resultadoProcessado.Sum(p => p.ValorReembolsado);
        decimal valorEfetivamenteAprovado = resultadoProcessado.Where(p => p.Status == StatusPedido.Aprovado).Sum(p => p.ValorReembolsado);
        decimal economiaRetida = valorTotalPago - valorEfetivamenteAprovado;

        var contagemPorTipo = resultadoProcessado
            .GroupBy(p => p.TipoProcedimento)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        // 7. Formatação do Relatório de Métricas
        var relatorio = new StringBuilder();
        relatorio.AppendLine("\n================================================================================");
        relatorio.AppendLine($"       RELATÓRIO DE SIMULAÇÃO EM LOTE: {totalPedidos:N0} PEDIDOS DE REEMBOLSO");
        relatorio.AppendLine("================================================================================");
        relatorio.AppendLine($"Data de Referência Utilizada: {dataReferencia:yyyy-MM-dd}");
        relatorio.AppendLine($"Tamanho do CSV Gerado:        {csvEntrada.Length / 1024.0:N1} KB\n");

        relatorio.AppendLine("⚡ 1. DESEMPENHO E BENCHMARK");
        relatorio.AppendLine("--------------------------------------------------------------------------------");
        relatorio.AppendLine($"  - Parse do CSV (10k linhas):          {swParse.ElapsedMilliseconds,6} ms");
        relatorio.AppendLine($"  - Processamento de Regras (Core):     {swCore.ElapsedMilliseconds,6} ms  ({throughputCorePuro:N0} pedidos/seg)");
        relatorio.AppendLine($"  - Serialização de Saída (CSV):        {swEscrita.ElapsedMilliseconds,6} ms");
        relatorio.AppendLine($"  - Tempo Total de Pipeline (E2E):      {tempoTotalMs,6} ms  ({throughputGeral:N0} pedidos/seg)");
        relatorio.AppendLine($"  - Tempo Médio por Pedido:             {(double)tempoTotalMs / totalPedidos:N4} ms/pedido\n");

        relatorio.AppendLine("📊 2. DISTRIBUIÇÃO DOS STATUS");
        relatorio.AppendLine("--------------------------------------------------------------------------------");
        relatorio.AppendLine($"  - ✅ Aprovados:                       {totalAprovados,6} ({(double)totalAprovados / totalPedidos:P2})");
        relatorio.AppendLine($"      ↳ Aprovados Integrais:            {totalAprovadosIntegral,6} ({(double)totalAprovadosIntegral / totalPedidos:P2})");
        relatorio.AppendLine($"      ↳ Limitados pelo Teto (R$ 500):   {totalAprovadosComTeto,6} ({(double)totalAprovadosComTeto / totalPedidos:P2})");
        relatorio.AppendLine($"  - ❌ Rejeitados (> 90 dias):          {totalRejeitados,6} ({(double)totalRejeitados / totalPedidos:P2})");
        relatorio.AppendLine($"  - ⚠️ Suspeitos de Fraude (30 dias):   {totalSuspeitos,6} ({(double)totalSuspeitos / totalPedidos:P2})");
        relatorio.AppendLine($"      ↳ Por Limite de Qtd (>5 ped):     {suspeitoPorQtd,6}");
        relatorio.AppendLine($"      ↳ Por Limite de Valor (>R$ 1.500):{suspeitoPorValor,6}");
        relatorio.AppendLine($"      ↳ Por Ambos os Limites:           {suspeitoPorAmbos,6}\n");

        relatorio.AppendLine("💰 3. MÉTRICAS FINANCEIRAS");
        relatorio.AppendLine("--------------------------------------------------------------------------------");
        relatorio.AppendLine($"  - Valor Total Pleiteado (Pago):       R$ {valorTotalPago,14:N2}");
        relatorio.AppendLine($"  - Valor Total Aprovado (Reembolso):   R$ {valorEfetivamenteAprovado,14:N2}");
        relatorio.AppendLine($"  - Economia / Risco Evitado:           R$ {economiaRetida,14:N2} ({(double)economiaRetida / (double)valorTotalPago:P2})");
        if (totalAprovados > 0)
        {
            relatorio.AppendLine($"  - Ticket Médio Aprovado:              R$ {valorEfetivamenteAprovado / totalAprovados,14:N2}");
        }

        relatorio.AppendLine("\n🏷️ 4. DISTRIBUIÇÃO POR TIPO DE PROCEDIMENTO");
        relatorio.AppendLine("--------------------------------------------------------------------------------");
        foreach (var (tipo, count) in contagemPorTipo)
        {
            relatorio.AppendLine($"  - {tipo,-22}: {count,6} pedidos ({(double)count / totalPedidos:P2})");
        }
        relatorio.AppendLine("================================================================================\n");

        _output.WriteLine(relatorio.ToString());

        // Asserções para garantir que a simulação é válida
        resultadoProcessado.Should().HaveCount(totalPedidos);
        totalAprovados.Should().BeGreaterThan(0);
        totalRejeitados.Should().BeGreaterThan(0);
        totalSuspeitos.Should().BeGreaterThan(0);
    }
}
