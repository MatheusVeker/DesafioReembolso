using FluentAssertions;
using DesafioReembolso.Core.Enums;
using DesafioReembolso.Core.Rules;
using Xunit;

namespace DesafioReembolso.Tests;

public class CalculadoraReembolsoTests
{
    private readonly CalculadoraReembolso _calculadora = new();

    [Theory]
    [InlineData(TipoProcedimento.ConsultaMedica, 0.80)]
    [InlineData(TipoProcedimento.ExameImagem, 0.90)]
    [InlineData(TipoProcedimento.ExameLaboratorial, 0.70)]
    [InlineData(TipoProcedimento.Outros, 0.50)]
    public void ObterPercentual_DeveRetornarPercentualCorretoPorTipo(TipoProcedimento tipo, decimal percentualEsperado)
    {
        var percentual = _calculadora.ObterPercentual(tipo);
        percentual.Should().Be(percentualEsperado);
    }

    [Fact]
    public void CalcularValorReembolsado_ConsultaMedica400_DeveRetornar320()
    {
        // 400 * 80% = 320 <= 500
        var resultado = _calculadora.CalcularValorReembolsado(TipoProcedimento.ConsultaMedica, 400.00m);
        resultado.Should().Be(320.00m);
    }

    [Fact]
    public void CalcularValorReembolsado_ExameImagem600_DeveAplicarTetoDe500()
    {
        // 600 * 90% = 540 -> Teto máximo R$ 500,00
        var resultado = _calculadora.CalcularValorReembolsado(TipoProcedimento.ExameImagem, 600.00m);
        resultado.Should().Be(500.00m);
    }

    [Fact]
    public void CalcularValorReembolsado_ExameLaboratorial200_DeveRetornar140()
    {
        // 200 * 70% = 140 <= 500
        var resultado = _calculadora.CalcularValorReembolsado(TipoProcedimento.ExameLaboratorial, 200.00m);
        resultado.Should().Be(140.00m);
    }

    [Fact]
    public void CalcularValorReembolsado_Outros150_DeveRetornar75()
    {
        // 150 * 50% = 75 <= 500
        var resultado = _calculadora.CalcularValorReembolsado(TipoProcedimento.Outros, 150.00m);
        resultado.Should().Be(75.00m);
    }

    [Fact]
    public void CalcularValorReembolsado_ValorZeroOuNegativo_DeveRetornarZero()
    {
        _calculadora.CalcularValorReembolsado(TipoProcedimento.ConsultaMedica, 0m).Should().Be(0m);
        _calculadora.CalcularValorReembolsado(TipoProcedimento.ConsultaMedica, -100m).Should().Be(0m);
    }

    [Fact]
    public void CalcularValorReembolsado_ValoresComCentavos_DeveArredondarCorretamente()
    {
        // 133.33 * 80% = 106.664 -> 106.66
        var resultado = _calculadora.CalcularValorReembolsado(TipoProcedimento.ConsultaMedica, 133.33m);
        resultado.Should().Be(106.66m);
    }

    [Fact]
    public void Calcular_ExameImagem600_DeveRetornarAtingiuTetoComoTrue()
    {
        // 600 * 90% = 540 -> excede 500
        var resultado = _calculadora.Calcular(TipoProcedimento.ExameImagem, 600.00m);
        resultado.ValorReembolsado.Should().Be(500.00m);
        resultado.AtingiuTetoMaximo.Should().BeTrue();
        resultado.ValorCalculadoSemTeto.Should().Be(540.00m);
    }

    [Fact]
    public void Calcular_ConsultaMedica400_DeveRetornarAtingiuTetoComoFalse()
    {
        // 400 * 80% = 320 <= 500
        var resultado = _calculadora.Calcular(TipoProcedimento.ConsultaMedica, 400.00m);
        resultado.ValorReembolsado.Should().Be(320.00m);
        resultado.AtingiuTetoMaximo.Should().BeFalse();
        resultado.ValorCalculadoSemTeto.Should().Be(320.00m);
    }
}
