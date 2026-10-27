namespace DesafioReembolso.Core.Models;

public sealed class ClienteHistoricoLote
{
    public const int LimiteMaximoPedidos30Dias = 5;
    public const decimal LimiteMaximoValor30Dias = 1500.00m;

    public int QuantidadePedidosJanela30Dias { get; private set; }
    public decimal TotalReembolsadoJanela30Dias { get; private set; }

    public bool ExcederiaLimitePedidos() => QuantidadePedidosJanela30Dias >= LimiteMaximoPedidos30Dias;

    public bool ExcederiaLimiteValor(decimal valorNovo) => 
        (TotalReembolsadoJanela30Dias + valorNovo) > LimiteMaximoValor30Dias;

    public void RegistrarAprovacao(decimal valorAprovado)
    {
        QuantidadePedidosJanela30Dias++;
        TotalReembolsadoJanela30Dias += valorAprovado;
    }
}
