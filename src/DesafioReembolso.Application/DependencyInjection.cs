using Microsoft.Extensions.DependencyInjection;
using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Rules;
using DesafioReembolso.Core.Services;

namespace DesafioReembolso.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDesafioReembolso(this IServiceCollection services)
    {
        // Core Rules & Domain Services
        services.AddSingleton<ICalculadoraReembolso, CalculadoraReembolso>();
        services.AddSingleton<IRegraPrazo, RegraPrazo90Dias>();
        services.AddSingleton<IMotorPrevencaoFraude, MotorPrevencaoFraude>();
        services.AddSingleton<IProcessadorLoteService, ProcessadorLoteService>();

        // Application Services
        services.AddSingleton<ICsvService, CsvService>();

        return services;
    }
}
