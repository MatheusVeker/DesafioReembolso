using System.Text;
using Microsoft.AspNetCore.Mvc;
using DesafioReembolso.Application.DTOs;
using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Models;
using DesafioReembolso.Core.Services;

namespace DesafioReembolso.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ReembolsosController : ControllerBase
{
    private readonly IProcessadorLoteService _processadorService;
    private readonly ICsvService _csvService;
    private readonly ILogger<ReembolsosController> _logger;

    public ReembolsosController(
        IProcessadorLoteService processadorService,
        ICsvService csvService,
        ILogger<ReembolsosController> logger)
    {
        _processadorService = processadorService ?? throw new ArgumentNullException(nameof(processadorService));
        _csvService = csvService ?? throw new ArgumentNullException(nameof(csvService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retorna informações sobre as regras de negócio ativas no backend.
    /// </summary>
    [HttpGet("regras")]
    public IActionResult ObterRegras()
    {
        return Ok(new
        {
            Procedimentos = new[]
            {
                new { Tipo = "Consulta Médica", Percentual = "80%" },
                new { Tipo = "Exame de Imagem", Percentual = "90%" },
                new { Tipo = "Exame Laboratorial", Percentual = "70%" },
                new { Tipo = "Outros", Percentual = "50%" }
            },
            LimitePorProcedimento = "R$ 500,00",
            PrazoMaximoDias = 90,
            PrevencaoFraude = new
            {
                JanelaDias = 30,
                LimiteMaximoPedidos = 5,
                LimiteMaximoValorTotal = "R$ 1.500,00"
            },
            DataReferenciaPadrao = "2024-05-10"
        });
    }

    /// <summary>
    /// Processa um lote de pedidos enviado em formato JSON.
    /// </summary>
    [HttpPost("processar-lote")]
    [ProducesResponseType(typeof(ProcessarLoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ProcessarLote([FromBody] ProcessarLoteRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { Mensagem = "A requisição não pode ser nula." });
        }

        // Validação da data de referência
        var dataReferenciaTexto = string.IsNullOrWhiteSpace(request.DataReferencia) ? "2024-05-10" : request.DataReferencia;
        if (!CsvService.TentarConverterData(dataReferenciaTexto, out var dataReferencia))
        {
            return BadRequest(new
            {
                Mensagem = "Data de referência inválida.",
                Detalhes = new[] { $"A data informada '{request.DataReferencia}' não é válida. Formato esperado: yyyy-MM-dd." }
            });
        }

        if (request.Pedidos is null || request.Pedidos.Count == 0)
        {
            return BadRequest(new
            {
                Mensagem = "Nenhum pedido foi informado no lote.",
                Detalhes = new[] { "A lista 'pedidos' deve conter pelo menos um pedido de reembolso." }
            });
        }

        var errosValidacao = new List<string>();
        var pedidosDominio = new List<PedidoReembolso>();

        for (int i = 0; i < request.Pedidos.Count; i++)
        {
            var p = request.Pedidos[i];
            var indice = i + 1;

            if (p.Id < 0)
            {
                errosValidacao.Add($"Pedido #{indice}: O Id deve ser maior ou igual a zero.");
            }

            if (string.IsNullOrWhiteSpace(p.TipoProcedimento))
            {
                errosValidacao.Add($"Pedido #{indice} (Id {p.Id}): Tipo de procedimento não informado.");
            }

            if (!CsvService.TentarConverterData(p.DataProcedimento, out var dataProcedimento))
            {
                errosValidacao.Add($"Pedido #{indice} (Id {p.Id}): Data do procedimento '{p.DataProcedimento}' inválida. Formato esperado: yyyy-MM-dd.");
            }

            if (p.ValorPago < 0)
            {
                errosValidacao.Add($"Pedido #{indice} (Id {p.Id}): Valor pago não pode ser negativo.");
            }

            if (string.IsNullOrWhiteSpace(p.ClienteId))
            {
                errosValidacao.Add($"Pedido #{indice} (Id {p.Id}): ClienteId não informado.");
            }

            if (errosValidacao.Count == 0)
            {
                pedidosDominio.Add(PedidoReembolso.Criar(
                    p.Id,
                    p.TipoProcedimento,
                    dataProcedimento,
                    p.ValorPago,
                    p.ClienteId));
            }
        }

        if (errosValidacao.Count > 0)
        {
            return BadRequest(new
            {
                Mensagem = "Erros de validação encontrados no lote de pedidos.",
                Detalhes = errosValidacao
            });
        }

        var processados = _processadorService.ProcessarLote(pedidosDominio, dataReferencia);
        RegistrarLogsAuditoria(processados, dataReferencia);
        var response = MontarResposta(processados, dataReferencia);

        return Ok(response);
    }

    /// <summary>
    /// Processa um lote de pedidos a partir de um arquivo CSV enviado via formulário.
    /// </summary>
    [HttpPost("processar-csv")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProcessarLoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessarCsv(
        IFormFile arquivo,
        [FromForm] string? dataReferencia = null,
        [FromQuery] string? formato = "json")
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new
            {
                Mensagem = "Arquivo CSV não fornecido ou vazio.",
                Detalhes = new[] { "Por favor, selecione um arquivo .csv válido com os dados dos pedidos." }
            });
        }

        var dataRefTexto = string.IsNullOrWhiteSpace(dataReferencia) ? "2024-05-10" : dataReferencia;
        if (!CsvService.TentarConverterData(dataRefTexto, out var dataRef))
        {
            return BadRequest(new
            {
                Mensagem = "Data de referência inválida.",
                Detalhes = new[] { $"A data de referência '{dataReferencia}' é inválida. Formato esperado: yyyy-MM-dd." }
            });
        }

        string conteudoCsv;
        using (var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.UTF8))
        {
            conteudoCsv = await reader.ReadToEndAsync();
        }

        var (pedidos, erros) = _csvService.FazerParseCsv(conteudoCsv);

        if (erros.Count > 0)
        {
            return BadRequest(new
            {
                Mensagem = "Erros encontrados ao interpretar o arquivo CSV.",
                Detalhes = erros
            });
        }

        if (pedidos.Count == 0)
        {
            return BadRequest(new
            {
                Mensagem = "Nenhum pedido válido encontrado no arquivo CSV.",
                Detalhes = new[] { "Verifique se o arquivo contém linhas de dados além do cabeçalho." }
            });
        }

        var processados = _processadorService.ProcessarLote(pedidos, dataRef);
        RegistrarLogsAuditoria(processados, dataRef);

        // Se solicitado formato CSV de download direto
        if (string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = Encoding.UTF8.GetBytes(_csvService.GerarCsv(processados));
            return File(csvBytes, "text/csv; charset=utf-8", "pedidos_processados.csv");
        }

        var response = MontarResposta(processados, dataRef);
        return Ok(response);
    }

    /// <summary>
    /// Gera e baixa o arquivo CSV a partir de uma lista de pedidos processados enviada no corpo da requisição.
    /// </summary>
    [HttpPost("exportar-csv")]
    public IActionResult ExportarCsv([FromBody] List<PedidoOutputDto> pedidos)
    {
        if (pedidos is null || pedidos.Count == 0)
        {
            return BadRequest(new { Mensagem = "Nenhum pedido fornecido para exportação." });
        }

        var pedidosProcessados = pedidos.Select(p =>
        {
            _ = CsvService.TentarConverterData(p.DataProcedimento, out var data);
            _ = Enum.TryParse<StatusPedido>(p.Status.Replace(" ", string.Empty), true, out var status);

            return new PedidoProcessado
            {
                Id = p.Id,
                TipoProcedimento = p.TipoProcedimento,
                DataProcedimento = data,
                ValorPago = p.ValorPago,
                ValorReembolsado = p.ValorReembolsado,
                ClienteId = p.ClienteId,
                Status = status
            };
        }).ToList();

        var csvTexto = _csvService.GerarCsv(pedidosProcessados);
        var bytes = Encoding.UTF8.GetBytes(csvTexto);

        return File(bytes, "text/csv; charset=utf-8", "reembolsos_processados.csv");
    }

    private static ProcessarLoteResponse MontarResposta(
        IReadOnlyList<PedidoProcessado> processados,
        DateOnly dataReferencia)
    {
        var totalAprovados = processados.Count(p => p.Status == StatusPedido.Aprovado);
        var totalRejeitados = processados.Count(p => p.Status == StatusPedido.Rejeitado);
        var totalSuspeitos = processados.Count(p => p.Status == StatusPedido.SuspeitoDeFraude);
        var valorTotalAprovado = processados
            .Where(p => p.Status == StatusPedido.Aprovado)
            .Sum(p => p.ValorReembolsado);

        var pedidosDto = processados.Select(p => new PedidoOutputDto
        {
            Id = p.Id,
            TipoProcedimento = p.TipoProcedimento,
            DataProcedimento = p.DataProcedimento.ToString("yyyy-MM-dd"),
            ValorPago = p.ValorPago,
            ValorReembolsado = p.ValorReembolsado,
            ClienteId = p.ClienteId,
            Status = p.StatusDescricao,
            Motivo = p.MotivoDetalhe
        }).ToList();

        return new ProcessarLoteResponse
        {
            DataReferencia = dataReferencia.ToString("yyyy-MM-dd"),
            TotalProcessados = processados.Count,
            TotalAprovados = totalAprovados,
            TotalRejeitados = totalRejeitados,
            TotalSuspeitos = totalSuspeitos,
            ValorTotalReembolsadoAprovado = valorTotalAprovado,
            Pedidos = pedidosDto
        };
    }

    /// <summary>
    /// Emite logs de auditoria para rastreabilidade de pedidos rejeitados ou suspeitos de fraude.
    /// </summary>
    private void RegistrarLogsAuditoria(IReadOnlyList<PedidoProcessado> pedidos, DateOnly dataReferencia)
    {
        var rejeitados = 0;
        var suspeitos = 0;

        foreach (var pedido in pedidos)
        {
            if (pedido.Status == StatusPedido.Rejeitado)
            {
                rejeitados++;
                _logger.LogWarning(
                    "[AUDITORIA - REJEITADO] PedidoId: {PedidoId} | ClienteId: {ClienteId} | Procedimento: {TipoProcedimento} | Data: {DataProcedimento:yyyy-MM-dd} | ValorPago: {ValorPago:C} | Motivo: {Motivo}",
                    pedido.Id,
                    pedido.ClienteId,
                    pedido.TipoProcedimento,
                    pedido.DataProcedimento,
                    pedido.ValorPago,
                    pedido.MotivoDetalhe);
            }
            else if (pedido.Status == StatusPedido.SuspeitoDeFraude)
            {
                suspeitos++;
                _logger.LogWarning(
                    "[AUDITORIA - SUSPEITA DE FRAUDE] PedidoId: {PedidoId} | ClienteId: {ClienteId} | Procedimento: {TipoProcedimento} | Data: {DataProcedimento:yyyy-MM-dd} | ValorPago: {ValorPago:C} | ValorCalculado: {ValorReembolsado:C} | Motivo: {Motivo}",
                    pedido.Id,
                    pedido.ClienteId,
                    pedido.TipoProcedimento,
                    pedido.DataProcedimento,
                    pedido.ValorPago,
                    pedido.ValorReembolsado,
                    pedido.MotivoDetalhe);
            }
        }

        if (rejeitados > 0 || suspeitos > 0)
        {
            _logger.LogInformation(
                "[AUDITORIA - RESUMO DO LOTE] DataReferencia: {DataRef:yyyy-MM-dd} | Total: {Total} | Aprovados: {Aprovados} | Rejeitados: {Rejeitados} | Suspeitos: {Suspeitos}",
                dataReferencia,
                pedidos.Count,
                pedidos.Count - rejeitados - suspeitos,
                rejeitados,
                suspeitos);
        }
    }
}
