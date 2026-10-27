using System.Globalization;
using System.Text;
using DesafioReembolso.Core.Models;

namespace DesafioReembolso.Application.Services;

public sealed class CsvService : ICsvService
{
    private const string CabecalhoEsperadoSaida = "Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status";

    public (IReadOnlyList<PedidoReembolso> Pedidos, IReadOnlyList<string> Erros) FazerParseCsv(string conteudoCsv)
    {
        using var stringReader = new StringReader(conteudoCsv ?? string.Empty);
        return FazerParseCsv(stringReader);
    }

    public (IReadOnlyList<PedidoReembolso> Pedidos, IReadOnlyList<string> Erros) FazerParseCsv(TextReader reader)
    {
        var pedidos = new List<PedidoReembolso>();
        var erros = new List<string>();

        if (reader is null)
        {
            erros.Add("Leitor CSV não informado.");
            return (pedidos, erros);
        }

        string? linha;
        var numeroLinha = 0;
        Dictionary<string, int>? mapaColunas = null;

        while ((linha = reader.ReadLine()) is not null)
        {
            numeroLinha++;

            // Remove BOM e espaços em branco
            linha = linha.Trim('\uFEFF', '\u200B', ' ', '\t', '\r', '\n');

            if (string.IsNullOrWhiteSpace(linha))
            {
                continue;
            }

            // Ignora comentários iniciados por '#'
            if (linha.StartsWith('#'))
            {
                continue;
            }

            var colunas = DividirColunasCsv(linha);

            if (mapaColunas is null)
            {
                mapaColunas = IdentificarCabecalho(colunas, numeroLinha, erros);
                if (mapaColunas is null)
                {
                    // Não foi possível validar o cabeçalho
                    return (pedidos, erros);
                }
                continue;
            }

            if (!ProcessarLinhaPedido(colunas, mapaColunas, numeroLinha, pedidos, erros))
            {
                // Erro adicionado na coleção de erros
            }
        }

        if (mapaColunas is null)
        {
            erros.Add("O arquivo CSV está vazio ou não possui cabeçalho válido.");
        }

        return (pedidos, erros);
    }

    public string GerarCsv(IReadOnlyList<PedidoProcessado> pedidos)
    {
        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        EscreverCsv(pedidos, stringWriter);
        return sb.ToString();
    }

    public void EscreverCsv(IReadOnlyList<PedidoProcessado> pedidos, TextWriter writer)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.WriteLine(CabecalhoEsperadoSaida);

        if (pedidos is null || pedidos.Count == 0)
        {
            return;
        }

        foreach (var pedido in pedidos)
        {
            var valorPagoStr = PedidoProcessado.FormatarValor(pedido.ValorPago);
            var valorReembolsadoStr = PedidoProcessado.FormatarValor(pedido.ValorReembolsado);
            var dataFormatada = pedido.DataProcedimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            writer.WriteLine(
                $"{pedido.Id},{pedido.TipoProcedimento},{dataFormatada},{valorPagoStr},{valorReembolsadoStr},{pedido.ClienteId},{pedido.StatusDescricao}");
        }
    }

    private static string[] DividirColunasCsv(string linha)
    {
        var partes = linha.Split(',');
        for (int i = 0; i < partes.Length; i++)
        {
            partes[i] = partes[i].Trim('\uFEFF', ' ', '\t');
        }
        return partes;
    }

    private static Dictionary<string, int>? IdentificarCabecalho(string[] colunas, int numeroLinha, List<string> erros)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < colunas.Length; i++)
        {
            var nomeNormalizado = NormalizarNomeColuna(colunas[i]);
            if (!string.IsNullOrEmpty(nomeNormalizado) && !mapa.ContainsKey(nomeNormalizado))
            {
                mapa[nomeNormalizado] = i;
            }
        }

        var colunasObrigatorias = new[] { "id", "tipoprocedimento", "dataprocedimento", "valorpago", "clienteid" };
        var colunasAusentes = colunasObrigatorias.Where(c => !mapa.ContainsKey(c)).ToList();

        if (colunasAusentes.Count > 0)
        {
            erros.Add($"Linha {numeroLinha}: Cabeçalho inválido. Colunas obrigatórias ausentes: {string.Join(", ", colunasAusentes)}.");
            return null;
        }

        return mapa;
    }

    private static string NormalizarNomeColuna(string nome)
    {
        return nome.Trim('\uFEFF', ' ', '\t')
                   .Replace(" ", string.Empty)
                   .Replace("_", string.Empty)
                   .Replace("-", string.Empty)
                   .ToLowerInvariant();
    }

    private static bool ProcessarLinhaPedido(
        string[] colunas,
        Dictionary<string, int> mapa,
        int numeroLinha,
        List<PedidoReembolso> pedidos,
        List<string> erros)
    {
        var indiceId = mapa["id"];
        var indiceTipo = mapa["tipoprocedimento"];
        var indiceData = mapa["dataprocedimento"];
        var indiceValor = mapa["valorpago"];
        var indiceCliente = mapa["clienteid"];

        var maxIndice = Math.Max(Math.Max(Math.Max(indiceId, indiceTipo), Math.Max(indiceData, indiceValor)), indiceCliente);

        if (colunas.Length <= maxIndice)
        {
            erros.Add($"Linha {numeroLinha}: Quantidade insuficiente de colunas (esperado no mínimo {maxIndice + 1}, recebido {colunas.Length}).");
            return false;
        }

        // 1. Id
        if (!int.TryParse(colunas[indiceId], CultureInfo.InvariantCulture, out var id) || id < 0)
        {
            erros.Add($"Linha {numeroLinha}: Id inválido '{colunas[indiceId]}'. Deve ser um número inteiro maior ou igual a zero.");
            return false;
        }

        // 2. TipoProcedimento
        var tipoProcedimentoTexto = colunas[indiceTipo];
        if (string.IsNullOrWhiteSpace(tipoProcedimentoTexto))
        {
            erros.Add($"Linha {numeroLinha}: Tipo de procedimento não pode estar vazio.");
            return false;
        }

        // 3. DataProcedimento
        var dataTexto = colunas[indiceData];
        if (!TentarConverterData(dataTexto, out var dataProcedimento))
        {
            erros.Add($"Linha {numeroLinha}: Data de procedimento inválida '{dataTexto}'. Formato esperado: yyyy-MM-dd (ex: 2024-02-10).");
            return false;
        }

        // 4. ValorPago
        var valorTexto = colunas[indiceValor].Replace(',', '.');
        if (!decimal.TryParse(valorTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorPago) || valorPago < 0)
        {
            erros.Add($"Linha {numeroLinha}: Valor pago inválido '{colunas[indiceValor]}'. Deve ser um valor numérico positivo.");
            return false;
        }

        // 5. ClienteId
        var clienteId = colunas[indiceCliente];
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            erros.Add($"Linha {numeroLinha}: ClienteId não pode estar vazio.");
            return false;
        }

        var pedido = PedidoReembolso.Criar(id, tipoProcedimentoTexto, dataProcedimento, valorPago, clienteId);
        pedidos.Add(pedido);
        return true;
    }

    public static bool TentarConverterData(string? dataTexto, out DateOnly data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(dataTexto)) return false;

        var formatosAceitos = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "yyyy/MM/dd",
            "dd-MM-yyyy"
        };

        return DateOnly.TryParseExact(
            dataTexto.Trim('\uFEFF', ' '),
            formatosAceitos,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out data);
    }
}
