using DesafioReembolso.Core.Enums;

namespace DesafioReembolso.Core.Models;

public readonly record struct ResultadoAvaliacaoFraude
{
    public bool EhSuspeito { get; init; }
    public MotivoStatus Motivo { get; init; }

    public static ResultadoAvaliacaoFraude Aprovado() => new()
    {
        EhSuspeito = false,
        Motivo = MotivoStatus.AprovadoComSucesso
    };

    public static ResultadoAvaliacaoFraude Suspeito(MotivoStatus motivo) => new()
    {
        EhSuspeito = true,
        Motivo = motivo
    };
}
