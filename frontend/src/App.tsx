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
  const [filtroClienteId, setFiltroClienteId] = useState<string>('');
  const [paginaAtual, setPaginaAtual] = useState<number>(1);
  const itensPorPagina = 25;

  const fileInputId = useId();
  const dateInputId = useId();
  const clientFilterInputId = useId();

  // Manipulação de seleção do arquivo
  const handleArquivoChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setArquivo(e.target.files[0]);
      setErro(null);
    }
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
    const atendeStatus = filtroStatus === 'Todos' || p.status === filtroStatus;
    const termoCliente = filtroClienteId.trim().toLowerCase();
    const atendeCliente = !termoCliente || String(p.clienteId).toLowerCase().includes(termoCliente);
    return atendeStatus && atendeCliente;
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
        <h1 className="app-title">
          Processamento de Reembolsos em Lote
        </h1>
      </header>

      {/* Painel de Upload e Configuração */}
      <section className="panel" aria-labelledby="secao-upload-titulo">
        <h2 id="secao-upload-titulo" style={{ display: 'none' }}>Envio de Lote</h2>
        <form onSubmit={handleProcessar} className="form-grid">
          {/* Campo Arquivo CSV */}
          <div className="form-group">
            <label htmlFor={fileInputId} className="form-label">
              Arquivo de Pedidos (.csv)
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
              <svg className="dropzone-icon" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
                <polyline points="10 9 9 9 8 9" />
              </svg>
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
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" stroke="none" aria-hidden="true">
                    <polygon points="5 3 19 12 5 21 5 3" />
                  </svg>
                  <span>Processar Lote</span>
                </>
              )}
            </button>
          </div>
        </form>
      </section>

      {/* Alerta de Erro */}
      {erro && (
        <section className="alert-card" role="alert" aria-live="assertive">
          <div className="alert-header">
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem' }}>
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="12" />
                <line x1="12" y1="16" x2="12.01" y2="16" />
              </svg>
              <span>{erro.mensagem}</span>
            </span>
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
              <span className="metric-sub">Retenção preventiva</span>
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

            {/* Ações e Busca por Cliente */}
            <div className="toolbar-actions">
              <div className="search-box">
                <label htmlFor={clientFilterInputId} className="visually-hidden">
                  Filtrar por ID do Cliente
                </label>
                <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <input
                  id={clientFilterInputId}
                  type="text"
                  className="search-input"
                  placeholder="Filtrar por ID do cliente..."
                  value={filtroClienteId}
                  onChange={(e) => {
                    setFiltroClienteId(e.target.value);
                    setPaginaAtual(1);
                  }}
                  aria-label="Filtrar pedidos por ID do cliente"
                />
                {filtroClienteId && (
                  <button
                    type="button"
                    className="clear-search-btn"
                    onClick={() => {
                      setFiltroClienteId('');
                      setPaginaAtual(1);
                    }}
                    title="Limpar filtro de cliente"
                    aria-label="Limpar filtro de cliente"
                  >
                    ×
                  </button>
                )}
              </div>

              {/* Botão de Download do CSV Completo */}
              <button
                type="button"
                className="btn btn-secondary"
                onClick={handleBaixarCsv}
                id="btn-baixar-csv"
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                  <polyline points="7 10 12 15 17 10" />
                  <line x1="12" y1="15" x2="12" y2="3" />
                </svg>
                <span>Baixar CSV Completo ({resultado.totalProcessados})</span>
              </button>
            </div>
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
                      pedido.status === 'Aprovado' ? (
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <polyline points="20 6 9 17 4 12" />
                        </svg>
                      ) : pedido.status === 'Rejeitado' ? (
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <line x1="18" y1="6" x2="6" y2="18" />
                          <line x1="6" y1="6" x2="18" y2="18" />
                        </svg>
                      ) : (
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
                          <line x1="12" y1="9" x2="12" y2="13" />
                          <line x1="12" y1="17" x2="12.01" y2="17" />
                        </svg>
                      );

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
                            {statusIcon}
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
                <svg className="empty-icon" width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <div className="empty-title">Nenhum pedido encontrado</div>
                <div className="empty-desc">
                  Nenhum pedido corresponde aos filtros aplicados
                  {filtroStatus !== 'Todos' && <> (Status: <strong>{filtroStatus}</strong>)</>}
                  {filtroClienteId.trim() && <> (Cliente ID: <strong>"{filtroClienteId.trim()}"</strong>)</>}.
                </div>
                {(filtroStatus !== 'Todos' || filtroClienteId.trim()) && (
                  <button
                    type="button"
                    className="btn btn-secondary btn-outline-action"
                    style={{ marginTop: '0.75rem' }}
                    onClick={() => {
                      setFiltroStatus('Todos');
                      setFiltroClienteId('');
                      setPaginaAtual(1);
                    }}
                  >
                    Limpar Filtros
                  </button>
                )}
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
          <svg className="empty-icon" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
          </svg>
          <h2 className="empty-title">Pronto para processar seu primeiro lote</h2>
          <p className="empty-desc">
            Selecione um arquivo CSV com os pedidos de reembolso e informe a data de referência para iniciar a auditoria do lote.
          </p>
        </section>
      )}
    </div>
  );
}
