export type StatusPedidoTipo = 'Aprovado' | 'Rejeitado' | 'Suspeito de Fraude';

export interface PedidoProcessado {
  id: number;
  tipoProcedimento: string;
  dataProcedimento: string;
  valorPago: number;
  valorReembolsado: number;
  clienteId: string;
  status: StatusPedidoTipo | string;
  motivo?: string;
}

export interface ProcessarLoteResponse {
  dataReferencia: string;
  totalProcessados: number;
  totalAprovados: number;
  totalRejeitados: number;
  totalSuspeitos: number;
  valorTotalReembolsadoAprovado: number;
  pedidos: PedidoProcessado[];
}

export interface ApiErroResponse {
  mensagem?: string;
  detalhes?: string[];
}

export type StatusFiltro = 'Todos' | 'Aprovado' | 'Rejeitado' | 'Suspeito de Fraude';
