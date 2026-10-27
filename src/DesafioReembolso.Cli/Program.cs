using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using DesafioReembolso.Application;
using DesafioReembolso.Application.Services;
using DesafioReembolso.Core.Services;

namespace DesafioReembolso.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Garante suporte adequado a caracteres UTF-8 no console
        Console.OutputEncoding = Encoding.UTF8;

        // Configuração do container de injeção de dependência compartilhada
        var services = new ServiceCollection();
        services.AddDesafioReembolso();
        using var serviceProvider = services.BuildServiceProvider();

        var csvService = serviceProvider.GetRequiredService<ICsvService>();
        var processadorService = serviceProvider.GetRequiredService<IProcessadorLoteService>();

        if (args.Contains("--help") || args.Contains("-h"))
        {
            ExibirAjuda();
            return 0;
        }

        var dataReferencia = ObterDataReferencia(args);

        // Leitura do fluxo de entrada padrão (stdin)
        var conteudoCsv = Console.In.ReadToEnd();

        if (string.IsNullOrWhiteSpace(conteudoCsv))
        {
            Console.Error.WriteLine("Erro: Nenhum dado foi recebido via stdin.");
            Console.Error.WriteLine("Exemplo de uso: Get-Content entrada.csv | dotnet run --project src/DesafioReembolso.Cli -- -d 2024-05-10");
            return 1;
        }

        // Parse do CSV de entrada
        var (pedidos, erros) = csvService.FazerParseCsv(conteudoCsv);

        if (erros.Count > 0)
        {
            Console.Error.WriteLine("Foram encontrados erros na validação do CSV de entrada:");
            foreach (var erro in erros)
            {
                Console.Error.WriteLine($" - {erro}");
            }
            return 1;
        }

        if (pedidos.Count == 0)
        {
            Console.Error.WriteLine("Aviso: Nenhum registro de pedido foi encontrado para processamento no CSV.");
            return 1;
        }

        // Processamento do lote utilizando o motor de regras compartilhado
        var resultado = processadorService.ProcessarLote(pedidos, dataReferencia);

        // Escrita do CSV gerado no fluxo de saída padrão (stdout)
        csvService.EscreverCsv(resultado, Console.Out);

        return 0;
    }

    private static DateOnly ObterDataReferencia(string[] args)
    {
        // Data padrão para reprodução dos exemplos do enunciado: 2024-05-10
        var dataPadrao = new DateOnly(2024, 5, 10);

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-d" or "--data-referencia" or "--referencia" or "-r")
            {
                if (i + 1 < args.Length && CsvService.TentarConverterData(args[i + 1], out var dataInformada))
                {
                    return dataInformada;
                }
            }

            // Suporte a formato --data-referencia=2024-05-10
            if (arg.StartsWith("--data-referencia=", StringComparison.OrdinalIgnoreCase))
            {
                var valor = arg["--data-referencia=".Length..];
                if (CsvService.TentarConverterData(valor, out var data))
                {
                    return data;
                }
            }

            // Suporte a argumento posicional direto (ex: dotnet run -- 2024-05-10)
            if (CsvService.TentarConverterData(arg, out var dataPosicional))
            {
                return dataPosicional;
            }
        }

        return dataPadrao;
    }

    private static void ExibirAjuda()
    {
        Console.WriteLine("===============================================================");
        Console.WriteLine(" DesafioReembolso CLI - Processamento de Pedidos em Lote");
        Console.WriteLine("===============================================================");
        Console.WriteLine("Uso:");
        Console.WriteLine("  stdin | dotnet run --project src/DesafioReembolso.Cli [opções]");
        Console.WriteLine();
        Console.WriteLine("Opções:");
        Console.WriteLine("  -d, --data-referencia <DATA>   Data de referência (formato yyyy-MM-dd)");
        Console.WriteLine("                                  Padrão: 2024-05-10 (data dos exemplos)");
        Console.WriteLine("  -h, --help                     Exibe esta mensagem de ajuda");
        Console.WriteLine();
        Console.WriteLine("Exemplo de Execução:");
        Console.WriteLine("  cat pedidos.csv | dotnet run -- -d 2024-05-10 > resultado.csv");
        Console.WriteLine("  Get-Content pedidos.csv | dotnet run --project src/DesafioReembolso.Cli -- -d 2024-05-10");
        Console.WriteLine();
    }
}
