using System.Globalization;
using System.Text;

namespace DesafioReembolso.Core.Enums;

public enum TipoProcedimento
{
    ConsultaMedica,
    ExameImagem,
    ExameLaboratorial,
    Outros
}

public static class TipoProcedimentoExtensions
{
    public const string DescricaoConsultaMedica = "Consulta Médica";
    public const string DescricaoExameImagem = "Exame de Imagem";
    public const string DescricaoExameLaboratorial = "Exame Laboratorial";
    public const string DescricaoOutros = "Outros";

    public static string ObterDescricao(this TipoProcedimento tipo) => tipo switch
    {
        TipoProcedimento.ConsultaMedica => DescricaoConsultaMedica,
        TipoProcedimento.ExameImagem => DescricaoExameImagem,
        TipoProcedimento.ExameLaboratorial => DescricaoExameLaboratorial,
        TipoProcedimento.Outros => DescricaoOutros,
        _ => DescricaoOutros
    };

    public static decimal ObterPercentualReembolso(this TipoProcedimento tipo) => tipo switch
    {
        TipoProcedimento.ConsultaMedica => 0.80m,
        TipoProcedimento.ExameImagem => 0.90m,
        TipoProcedimento.ExameLaboratorial => 0.70m,
        TipoProcedimento.Outros => 0.50m,
        _ => 0.50m
    };

    public static bool TentarConverter(string? texto, out TipoProcedimento tipo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            tipo = TipoProcedimento.Outros;
            return false;
        }

        var normalizado = RemoverAcentos(texto.Trim());

        // Correspondência exata e por termos-chave (tolerante a pipes ANSI/OEM do PowerShell)
        if (normalizado.Equals("Consulta Medica", StringComparison.OrdinalIgnoreCase) ||
            normalizado.StartsWith("Consulta", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoProcedimento.ConsultaMedica;
            return true;
        }

        if (normalizado.Equals("Exame de Imagem", StringComparison.OrdinalIgnoreCase) ||
            normalizado.Contains("Imagem", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoProcedimento.ExameImagem;
            return true;
        }

        if (normalizado.Equals("Exame Laboratorial", StringComparison.OrdinalIgnoreCase) ||
            normalizado.Contains("Laborat", StringComparison.OrdinalIgnoreCase) ||
            normalizado.Contains("Lab", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoProcedimento.ExameLaboratorial;
            return true;
        }

        if (normalizado.Equals("Outros", StringComparison.OrdinalIgnoreCase) ||
            normalizado.StartsWith("Outro", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoProcedimento.Outros;
            return true;
        }

        tipo = TipoProcedimento.Outros;
        return false;
    }

    public static TipoProcedimento Converter(string? texto)
    {
        if (TentarConverter(texto, out var tipo))
        {
            return tipo;
        }

        return TipoProcedimento.Outros;
    }

    public static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        var normalizedString = texto.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        for (int i = 0; i < normalizedString.Length; i++)
        {
            char c = normalizedString[i];
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}
