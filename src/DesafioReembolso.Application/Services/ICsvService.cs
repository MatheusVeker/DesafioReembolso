using DesafioReembolso.Core.Models;

namespace DesafioReembolso.Application.Services;

public interface ICsvService
{
    (IReadOnlyList<PedidoReembolso> Pedidos, IReadOnlyList<string> Erros) FazerParseCsv(TextReader reader);
    (IReadOnlyList<PedidoReembolso> Pedidos, IReadOnlyList<string> Erros) FazerParseCsv(string conteudoCsv);
    string GerarCsv(IReadOnlyList<PedidoProcessado> pedidos);
    void EscreverCsv(IReadOnlyList<PedidoProcessado> pedidos, TextWriter writer);
}
