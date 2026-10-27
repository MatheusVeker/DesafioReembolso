using DesafioReembolso.Core.Models;

namespace DesafioReembolso.Core.Services;

public interface IProcessadorLoteService
{
    IReadOnlyList<PedidoProcessado> ProcessarLote(
        IEnumerable<PedidoReembolso> pedidos,
        DateOnly dataReferencia);
}
