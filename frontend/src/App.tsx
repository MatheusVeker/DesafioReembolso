import React, { useState, useId } from 'react';
import type { PedidoProcessado, ProcessarLoteResponse, StatusFiltro, ApiErroResponse } from './types/reembolso';

const API_BASE_URL = 'http://localhost:5244';

export default function App() {
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [dataReferencia, setDataReferencia] = useState<string>('2024-05-10');
  const [processando, setProcessando] = useState<boolean>(false);
  const [resultado, setResultado] = useState<ProcessarLoteResponse | null>(null);
  const [erro, setErro] = useState<{ mensagem: string; detalhes?: string[] } | null>(null);
  const [filtroStatus, setFiltroStatus] = useState<StatusFiltro>('Todos');
  const [paginaAtual, setPaginaAtual] = useState<number>(1);
  const itensPorPagina = 25;

  const fileInputId = useId();
  const dateInputId = useId();

  // Manipulação de seleção do arquivo
  const handleArquivoChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setArquivo(e.target.files[0]);
      setErro(null);
    }
  };

  // Carregar CSV de exemplo oficial com 1 clique para facilidade de teste
  const handleCarregarExemploOficial = () => {
    const csvOficial = `Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId
1,Consulta Médica,2024-02-10,400,123
2,Exame de Imagem,2024-03-01,600,124
3,Exame Laboratorial,2024-04-15,200,125
4,Outros,2024-01-20,150,126`;

    const blob = new Blob([csvOficial], { type: 'text/csv;charset=utf-8;' });
    const file = new File([blob], 'pedidos_exemplo_oficial.csv', { type: 'text/csv' });
    setArquivo(file);
    setDataReferencia('2024-05-10');
    setErro(null);
  };

  // Processamento do lote via API
  const handleProcessar = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();

    if (!arquivo) {
      setErro({
        mensagem: 'Nenhum arquivo selecionado.',
        detalhes: ['Por favor, selecione um arquivo CSV para continuar.']
      });
      return;
    }

    if (!dataReferencia) {
      setErro({
        mensagem: 'Data de referência não informada.',
        detalhes: ['Informe uma data de referência válida no formato yyyy-MM-dd.']
      });
      return;
    }

    setProcessando(true);
    setErro(null);

    const formData = new FormData();
    formData.append('arquivo', arquivo);
    formData.append('dataReferencia', dataReferencia);

    try {
      // Chama o endpoint da API (usando proxy relativo /api ou URL absoluta se fallback)
      let response: Response;
      try {
        response = await fetch('/api/reembolsos/processar-csv', {
          method: 'POST',
          body: formData,
        });
      } catch {
        // Fallback direto para localhost:5244 caso o proxy não esteja em uso
        response = await fetch(`${API_BASE_URL}/api/reembolsos/processar-csv`, {
          method: 'POST',
          body: formData,
        });
      }

      if (!response.ok) {
        let mensagemErro = `Erro no processamento (${response.status} ${response.statusText})`;
        let detalhes: string[] | undefined;

        try {
          const dadosErro: ApiErroResponse = await response.json();
          if (dadosErro.mensagem) mensagemErro = dadosErro.mensagem;
          if (dadosErro.detalhes) detalhes = dadosErro.detalhes;
        } catch {
          // Resposta não era JSON
        }

        setErro({ mensagem: mensagemErro, detalhes });
        return;
      }

      const dados: ProcessarLoteResponse = await response.json();
      setResultado(dados);
      setPaginaAtual(1);
    } catch (err) {
      setErro({
        mensagem: 'Não foi possível conectar ao servidor da API.',
        detalhes: [
          'Verifique se o backend em ASP.NET Core está em execução (porta 5244).',
          'Comando para iniciar a API: dotnet run --project src/DesafioReembolso.Api',
          `Erro técnico: ${err instanceof Error ? err.message : String(err)}`
        ]
      });
    } finally {
      setProcessando(false);
    }
  };

  // Exportação / Download do CSV completo
  const handleBaixarCsv = () => {
    if (!resultado || !resultado.pedidos || resultado.pedidos.length === 0) return;

    // Cabeçalho oficial especificado no enunciado:
    // Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status
    const linhas = [
      'Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status'
    ];

    // O download sempre gera a totalidade dos registros, mesmo que a tabela esteja filtrada
    for (const p of resultado.pedidos) {
      linhas.push(
        `${p.id},"${p.tipoProcedimento}",${p.dataProcedimento},${p.valorPago},${p.valorReembolsado},${p.clienteId},"${p.status}"`
      );
    }

    const conteudoCsv = linhas.join('\r\n');
    const blob = new Blob([conteudoCsv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `resultado_reembolsos_${resultado.dataReferencia || dataReferencia}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  // Filtragem dos pedidos na interface
  const pedidosFiltrados = (resultado?.pedidos || []).filter((p) => {
    if (filtroStatus === 'Todos') return true;
    return p.status === filtroStatus;
  });

  // Paginação dos resultados
  const totalPaginas = Math.ceil(pedidosFiltrados.length / itensPorPagina) || 1;
  const pedidosPaginados = pedidosFiltrados.slice(
    (paginaAtual - 1) * itensPorPagina,
    paginaAtual * itensPorPagina
  );

  // Formatação monetária (BRL)
  const formatarMoeda = (valor: number) => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(valor);
  };

  return (
    <div className="app-container">
      {/* Cabeçalho */}
      <header className="app-header">
        <div className="brand-badge">Sistema Corporativo de Saúde</div>
        <h1 className="app-title">
          Processamento de <span className="highlight">Reembolsos em Lote</span>
        </h1>
        <p className="app-subtitle">
          Auditoria automatizada de despesas médicas com cálculo de alíquotas por procedimento,
          limite de R$ 500, prazo de 90 dias e motor de prevenção a fraudes em 30 dias.
        </p>
      </header>

      {/* Painel de Upload e Configuração */}
      <section className="panel" aria-labelledby="secao-upload-titulo">
        <h2 id="secao-upload-titulo" style={{ display: 'none' }}>Envio de Lote</h2>
        <form onSubmit={handleProcessar} className="form-grid">
          {/* Campo Arquivo CSV */}
          <div className="form-group">
            <label htmlFor={fileInputId} className="form-label">
              Arquivo de Pedidos (.csv)
              <span className="helper">Separação por vírgula</span>
            </label>
            <div
              className="file-dropzone"
              tabIndex={0}
              role="button"
              aria-label="Selecionar arquivo CSV"
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  document.getElementById(fileInputId)?.click();
                }
              }}
            >
              <input
                id={fileInputId}
                type="file"
                accept=".csv,text/csv"
                onChange={handleArquivoChange}
                disabled={processando}
              />
              <span className="dropzone-icon" aria-hidden="true">📁</span>
              <div className="dropzone-text">
                <span className="dropzone-filename">
                  {arquivo ? arquivo.name : 'Selecione ou arraste o arquivo CSV'}
                </span>
                <span className="dropzone-prompt">
                  {arquivo
                    ? `${(arquivo.size / 1024).toFixed(1)} KB selecionado`
                    : 'Clique aqui para navegar pelos seus arquivos'}
                </span>
              </div>
            </div>
          </div>

          {/* Campo Data de Referência */}
          <div className="form-group">
            <label htmlFor={dateInputId} className="form-label">
              Data de Referência
              <span className="helper">(Validação de 90 e 30 dias)</span>
            </label>
            <input
              id={dateInputId}
              type="date"
              className="input-control"
              value={dataReferencia}
              onChange={(e) => setDataReferencia(e.target.value)}
              disabled={processando}
              required
            />
          </div>

          {/* Botões de Ação */}
          <div className="form-group" style={{ display: 'flex', flexDirection: 'row', gap: '0.5rem' }}>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={processando || !arquivo}
              id="btn-processar-lote"
              aria-busy={processando}
            >
              {processando ? (
                <>
                  <span className="spinner" aria-hidden="true" />
                  <span>Processando...</span>
                </>
              ) : (
                <>
                  <span aria-hidden="true">⚡</span>
                  <span>Processar Lote</span>
                </>
              )}
            </button>

            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleCarregarExemploOficial}
              disabled={processando}
              title="Carrega o arquivo CSV de exemplo oficial (4 pedidos)"
            >
              Exemplo Oficial
            </button>
          </div>
        </form>
      </section>

      {/* Alerta de Erro */}
      {erro && (
        <section className="alert-card" role="alert" aria-live="assertive">
          <div className="alert-header">
            <span>⚠️ {erro.mensagem}</span>
            <button
              type="button"
              className="btn btn-outline-action"
              onClick={() => handleProcessar()}
            >
              Tentar Novamente
            </button>
          </div>
          {erro.detalhes && erro.detalhes.length > 0 && (
            <ul className="alert-details">
              {erro.detalhes.map((detalhe, idx) => (
                <li key={idx}>{detalhe}</li>
              ))}
            </ul>
          )}
        </section>
      )}

      {/* Resultados e Métricas */}
      {resultado && (
        <>
          {/* Métricas do Lote */}
          <section className="metrics-grid" aria-label="Métricas do lote processado">
            <div className="metric-card">
              <span className="metric-label">Total Processados</span>
              <span className="metric-value">{resultado.totalProcessados}</span>
              <span className="metric-sub">Pedidos analisados</span>
            </div>

            <div className="metric-card">
              <span className="metric-label">Aprovados</span>
              <span className="metric-value aprovado">{resultado.totalAprovados}</span>
              <span className="metric-sub">
                Total: {formatarMoeda(resultado.valorTotalReembolsadoAprovado)}
              </span>
            </div>

            <div className="metric-card">
              <span className="metric-label">Rejeitados</span>
              <span className="metric-value rejeitado">{resultado.totalRejeitados}</span>
              <span className="metric-sub">Fora do prazo (&gt;90 dias)</span>
            </div>

            <div className="metric-card">
              <span className="metric-label">Suspeitos de Fraude</span>
              <span className="metric-value suspeito">{resultado.totalSuspeitos}</span>
              <span className="metric-sub">Limite de cota em 30 dias</span>
            </div>
          </section>

          {/* Barra de Ferramentas da Tabela */}
          <div className="table-toolbar">
            {/* Filtros de Visualização */}
            <div className="filter-group" role="radiogroup" aria-label="Filtrar por status">
              {(['Todos', 'Aprovado', 'Rejeitado', 'Suspeito de Fraude'] as StatusFiltro[]).map(
                (status) => {
                  const isActive = filtroStatus === status;
                  let count = resultado.totalProcessados;
                  if (status === 'Aprovado') count = resultado.totalAprovados;
                  if (status === 'Rejeitado') count = resultado.totalRejeitados;
                  if (status === 'Suspeito de Fraude') count = resultado.totalSuspeitos;

                  return (
                    <button
                      key={status}
                      type="button"
                      className={`filter-pill ${isActive ? 'active' : ''}`}
                      onClick={() => {
                        setFiltroStatus(status);
                        setPaginaAtual(1);
                      }}
                      role="radio"
                      aria-checked={isActive}
                    >
                      {status} ({count})
                    </button>
                  );
                }
              )}
            </div>

            {/* Botão de Download do CSV Completo */}
            <button
              type="button"
              className="btn btn-secondary"
              onClick={handleBaixarCsv}
              id="btn-baixar-csv"
            >
              <span aria-hidden="true">📥</span>
              <span>Baixar CSV Completo ({resultado.totalProcessados})</span>
            </button>
          </div>

          {/* Tabela de Pedidos Processados */}
          <div className="table-container">
            {pedidosPaginados.length > 0 ? (
              <table className="data-table" aria-label="Lista de pedidos processados">
                <thead>
                  <tr>
                    <th scope="col">ID</th>
                    <th scope="col">Tipo de Procedimento</th>
                    <th scope="col">Data</th>
                    <th scope="col">Valor Pago</th>
                    <th scope="col">Valor Reembolsado</th>
                    <th scope="col">ID Cliente</th>
                    <th scope="col">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {pedidosPaginados.map((pedido: PedidoProcessado) => {
                    const statusClass =
                      pedido.status === 'Aprovado'
                        ? 'status-aprovado'
                        : pedido.status === 'Rejeitado'
                        ? 'status-rejeitado'
                        : 'status-suspeito';

                    const statusIcon =
                      pedido.status === 'Aprovado'
                        ? '✓'
                        : pedido.status === 'Rejeitado'
                        ? '✕'
                        : '⚠️';

                    return (
                      <tr key={pedido.id}>
                        <td className="mono-cell">#{pedido.id}</td>
                        <td>
                          <strong>{pedido.tipoProcedimento}</strong>
                          {pedido.motivo && (
                            <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)', marginTop: '2px' }}>
                              {pedido.motivo}
                            </div>
                          )}
                        </td>
                        <td className="mono-cell">{pedido.dataProcedimento}</td>
                        <td className="val-cell">{formatarMoeda(pedido.valorPago)}</td>
                        <td className="val-cell val-reembolso">
                          {formatarMoeda(pedido.valorReembolsado)}
                        </td>
                        <td className="mono-cell">{pedido.clienteId}</td>
                        <td>
                          {/* Status diferenciado expressamente por TEXTO além de cores */}
                          <span className={`status-badge ${statusClass}`}>
                            <span aria-hidden="true">{statusIcon}</span>
                            <span>{pedido.status}</span>
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            ) : (
              <div className="empty-state">
                <span className="empty-icon">🔍</span>
                <div className="empty-title">Nenhum pedido encontrado</div>
                <div className="empty-desc">
                  Não há pedidos com o status <strong>{filtroStatus}</strong> neste lote.
                </div>
              </div>
            )}

            {/* Barra de Paginação */}
            {pedidosFiltrados.length > itensPorPagina && (
              <div className="pagination-bar">
                <span>
                  Mostrando {((paginaAtual - 1) * itensPorPagina) + 1} a{' '}
                  {Math.min(paginaAtual * itensPorPagina, pedidosFiltrados.length)} de{' '}
                  {pedidosFiltrados.length} pedidos
                </span>
                <div className="pagination-controls">
                  <button
                    type="button"
                    className="btn-page"
                    disabled={paginaAtual === 1}
                    onClick={() => setPaginaAtual((p) => Math.max(1, p - 1))}
                  >
                    Anterior
                  </button>
                  <span>
                    Página {paginaAtual} de {totalPaginas}
                  </span>
                  <button
                    type="button"
                    className="btn-page"
                    disabled={paginaAtual === totalPaginas}
                    onClick={() => setPaginaAtual((p) => Math.min(totalPaginas, p + 1))}
                  >
                    Próxima
                  </button>
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {/* Estado Inicial (quando ainda não houve processamento) */}
      {!resultado && !processando && !erro && (
        <section className="panel empty-state">
          <span className="empty-icon" aria-hidden="true">📋</span>
          <h2 className="empty-title">Pronto para processar seu primeiro lote</h2>
          <p className="empty-desc">
            Selecione um arquivo CSV com os pedidos de reembolso ou clique no botão{' '}
            <strong>"Exemplo Oficial"</strong> acima para carregar instantaneamente a amostra da especificação.
          </p>
        </section>
      )}

      {/* Rodapé Informativo */}
      <footer className="app-footer">
        <div>Desafio Reembolso • Backend ASP.NET Core (.NET 10) &amp; Frontend React (TypeScript)</div>
        <div>Clean Architecture • Prevenção a Fraudes • Auditoria em Lote</div>
      </footer>
    </div>
  );
}
