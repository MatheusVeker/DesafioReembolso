using FluentAssertions;
using DesafioReembolso.Core.Rules;
using Xunit;

namespace DesafioReembolso.Tests;

public class RegraPrazo90DiasTests
{
    private readonly RegraPrazo90Dias _regraPrazo = new();
    private readonly DateOnly _dataReferencia = new(2024, 5, 10);

    [Fact]
    public void EstaDentroDoPrazo_Exatamente90DiasAtras_DeveAprovar()
    {
        // Em 2024 (ano bissexto), 2024-05-10 - 90 dias = 2024-02-10 (exemplo 1 do enunciado)
        var dataProcedimento = new DateOnly(2024, 2, 10);
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(dataProcedimento, _dataReferencia);

        dentroDoPrazo.Should().BeTrue();
    }

    [Fact]
    public void EstaDentroDoPrazo_MenosDe90DiasAtras_DeveAprovar()
    {
        // 2024-04-15 = 25 dias atrás
        var dataProcedimento = new DateOnly(2024, 4, 15);
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(dataProcedimento, _dataReferencia);

        dentroDoPrazo.Should().BeTrue();
    }

    [Fact]
    public void EstaDentroDoPrazo_MesmoDiaDaReferencia_DeveAprovar()
    {
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(_dataReferencia, _dataReferencia);
        dentroDoPrazo.Should().BeTrue();
    }

    [Fact]
    public void EstaDentroDoPrazo_91DiasAtras_DeveRejeitar()
    {
        // 1 dia antes do limite de 90 dias
        var dataProcedimento = new DateOnly(2024, 2, 9);
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(dataProcedimento, _dataReferencia);

        dentroDoPrazo.Should().BeFalse();
    }

    [Fact]
    public void EstaDentroDoPrazo_Exemplo4Enunciado_111DiasAtras_DeveRejeitar()
    {
        // 2024-01-20 (exemplo 4 do enunciado: Rejeitado)
        var dataProcedimento = new DateOnly(2024, 1, 20);
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(dataProcedimento, _dataReferencia);

        dentroDoPrazo.Should().BeFalse();
    }

    [Fact]
    public void EstaDentroDoPrazo_DataFuturaEmRelacaoAReferencia_DeveRejeitar()
    {
        var dataFutura = _dataReferencia.AddDays(1);
        var dentroDoPrazo = _regraPrazo.EstaDentroDoPrazo(dataFutura, _dataReferencia);

        dentroDoPrazo.Should().BeFalse();
    }
}
