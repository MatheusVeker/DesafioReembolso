using FluentAssertions;
using DesafioReembolso.Core.Enums;
using Xunit;

namespace DesafioReembolso.Tests;

public class TipoProcedimentoTests
{
    [Theory]
    [InlineData("Consulta Médica", TipoProcedimento.ConsultaMedica)]
    [InlineData("Consulta Medica", TipoProcedimento.ConsultaMedica)]
    [InlineData("consulta médica", TipoProcedimento.ConsultaMedica)]
    [InlineData("CONSULTA MEDICA", TipoProcedimento.ConsultaMedica)]
    [InlineData("Exame de Imagem", TipoProcedimento.ExameImagem)]
    [InlineData("Exame Laboratorial", TipoProcedimento.ExameLaboratorial)]
    [InlineData("Outros", TipoProcedimento.Outros)]
    public void Deve_Converter_Tipos_Corretamente(string texto, TipoProcedimento esperado)
    {
        var sucesso = TipoProcedimentoExtensions.TentarConverter(texto, out var tipo);

        sucesso.Should().BeTrue();
        tipo.Should().Be(esperado);
    }
}
