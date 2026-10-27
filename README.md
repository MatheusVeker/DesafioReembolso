# Desafio Reembolso - Processamento de Reembolsos em Lote

Sistema corporativo para processamento de pedidos de reembolso de despesas de saúde em lote, com aplicação de regras de negócio, limites por procedimento e motor de prevenção a fraudes.

A solução é composta por:
1. **Core / Application (.NET):** Regras de negócio puras, cálculos de reembolso e validações desacopladas de frameworks e I/O.
2. **CLI (.NET):** Aplicação de linha de comando para processamento via `stdin` e geração de CSV em `stdout`.
3. **API HTTP (ASP.NET Core):** Serviço web REST com suporte a importação de CSV via `multipart/form-data`, payloads JSON e exportação de resultados.
4. **Testes Automatizados (xUnit + FluentAssertions + WebApplicationFactory):** Cobertura abrangente de testes unitários e de integração.
5. **Frontend (React + TypeScript):** Interface web moderna, acessível e responsiva em React 19 com Vite e Vanilla CSS.

---

## 🏛️ Arquitetura e Decisões Técnicas

A solução foi estruturada seguindo os princípios de **Clean Architecture**, **SOLID** e **Clean Code**:

```
DesafioReembolso/
├── src/
│   ├── DesafioReembolso.Core/          # Domínio: Entidades, Enums, Regras de Negócio e Serviços de Domínio
│   │   ├── Enums/                    # TipoProcedimento, StatusPedido, MotivoStatus
│   │   ├── Models/                   # PedidoReembolso, PedidoProcessado, ClienteHistoricoLote
│   │   ├── Rules/                    # ICalculadoraReembolso, IRegraPrazo, IMotorPrevencaoFraude
│   │   └── Services/                 # IProcessadorLoteService, ProcessadorLoteService
│   │
│   ├── DesafioReembolso.Application/   # Aplicação: DTOs, Serviços de CSV e Injeção de Dependências
│   │   ├── DTOs/                     # PedidoInputDto, PedidoOutputDto, ProcessarLoteDto
│   │   └── Services/                 # ICsvService, CsvService (parse e geração tolerante a encoding/BOM)
│   │
│   ├── DesafioReembolso.Cli/           # Adaptador de Entrada/Saída: Console stdin/stdout
│   │   └── Program.cs
│   │
│   └── DesafioReembolso.Api/           # Adaptador Web: ASP.NET Core Web API com OpenAPI e CORS
│       ├── Controllers/              # ReembolsosController
│       └── Program.cs
│
├── frontend/                         # Interface Web em React + TypeScript (Vite + Vanilla CSS)
│   ├── src/
│   │   ├── types/                    # Tipos TypeScript compartilhados (reembolso.ts)
│   │   ├── App.tsx                   # Fluxo completo: seleção, envio, filtros, paginação e exportação
│   │   └── index.css                 # Design System moderno, responsivo e de alto contraste
│   └── package.json
│
├── tests/
│   └── DesafioReembolso.Tests/         # 54 Testes Unitários, de Integração e Benchmark
│       ├── CalculadoraReembolsoTests.cs
│       ├── RegraPrazo90DiasTests.cs
│       ├── MotorPrevencaoFraudeTests.cs
│       ├── ProcessadorLoteServiceTests.cs
│       ├── CsvServiceTests.cs
│       ├── TipoProcedimentoTests.cs
│       ├── ReembolsosApiTests.cs
│       ├── SimulacaoLoteBenchmarkTests.cs
│       └── CincoRegrasDeNegocioTests.cs
│
└── exemplos/
    ├── pedidos_exemplo.csv                     # Arquivo CSV de exemplo oficial (4 pedidos)
    ├── teste_regra1_aliquotas.csv              # Teste Regra 1: Alíquotas (80%, 90%, 70%, 50%)
    ├── teste_regra2_teto_500.csv               # Teste Regra 2: Limite máximo de R$ 500 por procedimento
    ├── teste_regra3_prazo_90dias.csv           # Teste Regra 3: Validade de 90 dias e datas futuras
    ├── teste_regra4_fraude_limite_pedidos.csv  # Teste Regra 4: Máximo de 5 pedidos em 30 dias
    ├── teste_regra5_fraude_teto_1500.csv       # Teste Regra 5: Teto de R$ 1.500 em 30 dias
    └── pedidos_10000.csv                       # Amostra de simulação com 10.000 pedidos
```

### Justificativa de Bibliotecas e Frameworks Utilizados

- **.NET (C#) e ASP.NET Core:** Escolhido por seu alto desempenho nativo, tipagem estática robusta, excelente ecossistema para APIs de alta confiabilidade e suporte cross-platform.
- **FluentAssertions (Testes):** Facilita a leitura e manutenibilidade dos testes com assertivas expressivas e relatórios claros de falhas.
- **Microsoft.AspNetCore.Mvc.Testing (Testes de Integração):** Permite simular o pipeline HTTP completo da API em memória, sem depender de portas de rede abertas.
- **System.Text.Json e System.Globalization (Nativos):** Sem dependência de bibliotecas de terceiros desnecessárias para JSON ou CSV, mantendo o backend enxuto e seguro.
- **React 19 com TypeScript e Vite (Frontend):** Alta produtividade, tipagem estática de ponta a ponta e inicialização em milissegundos.
- **Vanilla CSS (Sem bibliotecas visuais externas):** Atendendo à diretriz de evitar dependências desnecessárias, todo o design system (glassmorphism, responsividade móvel/desktop, estados de erro/loading e tokens de acessibilidade) foi construído em CSS puro, sem sobrecarga de Tailwind, Bootstrap ou UI kits de terceiros.
- **Web APIs Nativas (Fetch, FormData, Blob, URL):** Comunicação HTTP com a API e geração/download instantâneo de CSV sem necessidade de bibliotecas adicionais como Axios ou FileSaver.

---

## ⚖️ Decisões sobre Conflitos entre Regras e Valores Exibidos

Conforme exigido na especificação, detalhamos as decisões de design adotadas para a resolução de conflitos e exibição de valores:

### 1. Ordem de Avaliação das Regras
Para garantir que as regras sejam aplicadas de maneira determinística, segura e justa:
1. **Validação do Arquivo de Entrada:** Verifica a integridade do arquivo recebido. Se o arquivo estiver vazio, sem cabeçalho válido ou com linhas ausentes/corrompidas, a execução é interrompida com notificação clara ao usuário.
2. **Ordenação Cronológica:** Os pedidos são ordenados internamente por `DataProcedimento` crescente (e índice de entrada para desempate). Isso garante que os procedimentos realizados primeiro tenham preferência de consumo das cotas.
3. **Regra de Validade Temporal (90 dias):**
   - Se o procedimento ocorreu há mais de 90 dias em relação à data de referência (ou possui data futura), o pedido é marcado como **`Rejeitado`** com motivo `ForaDoPrazo90Dias`.
   - **Conflito resolvido:** Pedidos com prazo expirado são rejeitados de imediato e **não** são submetidos à análise de fraude, nem consomem as cotas de quantidade (5) ou valor (R$ 1.500) do cliente na janela de 30 dias.
4. **Pré-Cálculo de Reembolso e Teto Individual:**
   - Calcula o valor percentual com base na alíquota (`80%` Consulta Médica, `90%` Exame de Imagem, `70%` Exame Laboratorial, `50%` Outros).
   - Aplica o teto de **R$ 500,00** por procedimento e verifica se o valor calculado excedeu o teto.
5. **Regras de Fraude (Janela de 30 dias):**
   - Procedimentos realizados na janela dos últimos 30 dias (`[dataReferencia - 30 dias, dataReferencia]`) são avaliados cumulativamente:
     - **Limite de Pedidos:** Máximo de 5 pedidos aprovados na janela. O 6º pedido em diante é marcado como **`Suspeito de Fraude`**.
     - **Limite de Valor:** Total reembolsado acumulado não pode ultrapassar R$ 1.500,00. Qualquer pedido cujo valor de reembolso faça o total exceder R$ 1.500 é marcado como **`Suspeito de Fraude`**.
     - **Ambos os limites excedidos:** Marcado como **`Suspeito de Fraude`** com motivo `LimitePedidosEValorExcedidos`.
6. **Aprovação e Registro de Consumo:**
   - Se aprovado integralmente ($\le$ R$ 500): motivo `AprovadoComSucesso` (*"Pedido aprovado dentro das regras e limites vigentes."*).
   - Se aprovado com valor limitado pelo teto (> R$ 500): motivo `AprovadoLimitadoPeloTeto` (*"Pedido aprovado com valor limitado ao teto máximo de R$ 500,00 por procedimento."*).
   - Registra o consumo de cota (+1 pedido e valor aprovado) no histórico de 30 dias do cliente.

### 2. Ordenação Determinística do Lote
Em um lote com múltiplos pedidos do mesmo cliente, a ordem de processamento define quem consome as cotas antes de estourar os limites. O motor processa os pedidos internamente por **ordem cronológica** (`DataProcedimento` crescente) e desempate pelo `Id`/ordem de inserção. Isso garante que os procedimentos que ocorreram primeiro recebam a aprovação legítima.
Após o cálculo, o resultado é **reordenado para a ordem original de entrada do CSV**, garantindo que a resposta devolvida ao usuário corresponda exatamente à sequência do arquivo importado.

### 3. Valor Exibido para Pedidos Rejeitados ou Suspeitos
No exemplo oficial do enunciado:
```csv
Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status
4,Outros,2024-01-20,150,75,126,Rejeitado
```
Observa-se que o campo `ValorReembolsado` exibe **75** (isto é, `150 * 50%`), enquanto o status é marcado como `Rejeitado`.
Adotamos essa mesma diretriz consistente:
- **`ValorReembolsado`**: Reflete o valor calculado que teria direito pelas regras de alíquota e teto de R$ 500. Isso é fundamental para fins de **auditoria, rastreabilidade e transparência** (o usuário e o analista conseguem ver quanto foi pleiteado).
- **Impacto no Saldo/Cota:** Pedidos marcados como `Rejeitado` ou `Suspeito de Fraude` **NÃO** somam no total reembolsado efetivo da API e **NÃO** bloqueiam futuros reembolsos válidos de outros pedidos.

---

## 🚀 Como Compilar e Executar

### Pré-requisitos
- .NET SDK 8.0 ou superior (instalado e disponível no PATH).

### Compilar a Solução
```bash
dotnet build DesafioReembolso.slnx
```

---

## 💻 Executando a CLI

A aplicação CLI lê o CSV de `stdin` e cospe o CSV resultante em `stdout`.

```bash
# Executando no PowerShell com data de referência padrão (2024-05-10)
Get-Content exemplos/pedidos_exemplo.csv | dotnet run --project src/DesafioReembolso.Cli

# Especificando uma data de referência customizada
Get-Content exemplos/pedidos_exemplo.csv | dotnet run --project src/DesafioReembolso.Cli -- -d 2024-05-10

# Redirecionando a saída para um novo arquivo CSV
Get-Content exemplos/pedidos_exemplo.csv | dotnet run --project src/DesafioReembolso.Cli -- -d 2024-05-10 > resultado.csv
```

---

## 🌐 Executando a API HTTP

### Iniciar o servidor
```bash
dotnet run --project src/DesafioReembolso.Api
```

- **URL Padrão:** `http://localhost:5244`
- **Documentação OpenAPI:** `http://localhost:5244/openapi/v1.json`
- **CORS:** Liberado para requisições de origens locais (ex: `http://localhost:5173`, `http://localhost:3000`).

### Exemplos de Chamada à API

#### 1. Processar arquivo CSV via `multipart/form-data`:
```bash
curl -X POST "http://localhost:5244/api/reembolsos/processar-csv" \
  -F "arquivo=@exemplos/pedidos_exemplo.csv" \
  -F "dataReferencia=2024-05-10"
```

**Resposta JSON:**
```json
{
  "dataReferencia": "2024-05-10",
  "totalProcessados": 4,
  "totalAprovados": 3,
  "totalRejeitados": 1,
  "totalSuspeitos": 0,
  "valorTotalReembolsadoAprovado": 960.00,
  "pedidos": [
    {
      "id": 1,
      "tipoProcedimento": "Consulta Médica",
      "dataProcedimento": "2024-02-10",
      "valorPago": 400,
      "valorReembolsado": 320.00,
      "clienteId": "123",
      "status": "Aprovado",
      "motivo": "Pedido aprovado dentro das regras e limites vigentes."
    },
    {
      "id": 2,
      "tipoProcedimento": "Exame de Imagem",
      "dataProcedimento": "2024-03-01",
      "valorPago": 600,
      "valorReembolsado": 500.00,
      "clienteId": "124",
      "status": "Aprovado",
      "motivo": "Pedido aprovado com valor limitado ao teto máximo de R$ 500,00 por procedimento."
    },
    {
      "id": 3,
      "tipoProcedimento": "Exame Laboratorial",
      "dataProcedimento": "2024-04-15",
      "valorPago": 200,
      "valorReembolsado": 140.00,
      "clienteId": "125",
      "status": "Aprovado",
      "motivo": "Pedido aprovado dentro das regras e limites vigentes."
    },
    {
      "id": 4,
      "tipoProcedimento": "Outros",
      "dataProcedimento": "2024-01-20",
      "valorPago": 150,
      "valorReembolsado": 75.00,
      "clienteId": "126",
      "status": "Rejeitado",
      "motivo": "Pedido rejeitado: data do procedimento fora do limite de 90 dias em relação à data de referência."
    }
  ]
}
```

#### 2. Processar lote via JSON:
```bash
curl -X POST "http://localhost:5244/api/reembolsos/processar-lote" \
  -H "Content-Type: application/json" \
  -d '{
    "dataReferencia": "2024-05-10",
    "pedidos": [
      {
        "id": 1,
        "tipoProcedimento": "Consulta Médica",
        "dataProcedimento": "2024-02-10",
        "valorPago": 400,
        "clienteId": "123"
      }
    ]
  }'
```

---

## 💻 Executando o Frontend (React + TypeScript)

A interface web foi desenvolvida em **React 19** com **TypeScript** e **Vite**, sem dependência de bibliotecas de componentes externas pesadas.

### Pré-requisitos
- Node.js 18+ e npm instalados.

### Iniciar o Frontend em Modo de Desenvolvimento
```bash
# 1. Navegar até a pasta do frontend
cd frontend

# 2. Instalar dependências (apenas na primeira vez)
npm install

# 3. Iniciar o servidor de desenvolvimento
npm run dev
```

- **URL da Aplicação Web:** `http://localhost:5173`
- **Comunicação com a API:** O Vite está configurado para direcionar chamadas `/api/*` via proxy para `http://localhost:5244`. Além disso, a API possui política de CORS configurada caso seja acessada diretamente.

### Recursos e Fluxo da Interface:
1. **Seleção de Arquivo e Data:**
   - Dropzone interativa para selecionar ou arrastar o arquivo `.csv`.
   - Campo para informar a data de referência (padrão predefinido: `2024-05-10`).
   - Botão **"Exemplo Oficial"** para carregar em 1 clique o cenário do enunciado sem precisar buscar arquivos no computador.
2. **Envio Protegido:**
   - O botão **"Processar Lote"** desabilita automaticamente durante a requisição, exibindo um *spinner* de carregamento e evitando envios duplicados.
3. **Métricas e Cards de Resumo:**
   - Cards com contagem de Total Processados, Aprovados (com valor total em R$), Rejeitados e Suspeitos de Fraude.
4. **Tabela Acessível e Detalhada:**
   - Exibe `Id`, `Tipo de Procedimento` (com o motivo detalhado), `Data`, `Valor Pago`, `Valor Reembolsado`, `ID Cliente` e `Status`.
   - **Acessibilidade de Status:** Todos os status são diferenciados expressamente por **texto e ícones** (`✓ Aprovado`, `✕ Rejeitado`, `⚠️ Suspeito de Fraude`), sem depender exclusivamente de cor.
5. **Filtro de Visualização:**
   - Filtro por pílulas: `Todos`, `Aprovado`, `Rejeitado` e `Suspeito de Fraude`.
6. **Download do CSV Completo:**
   - O botão **"Baixar CSV Completo"** gera o arquivo no formato e cabeçalho oficiais (`Id,TipoProcedimento,DataProcedimento,ValorPago,ValorReembolsado,ClienteId,Status`), garantindo que **todos** os pedidos do lote sejam preservados no arquivo baixado, mesmo que a tabela na tela esteja filtrada.
7. **Tratamento de Estados e Erros:**
   - Estados de: inicial, carregando, sem resultados e erro detalhado (arquivo corrompido, campos ausentes ou API indisponível) com botão para tentar novamente sem necessidade de recarregar a página.
8. **Paginação Eficiente:**
   - Paginação fluida para suportar lotes massivos (como o teste de 10.000 pedidos) com alta performance no navegador.

---

## 🧪 Executando os Testes Automatizados

A suíte possui **48 testes unitários e de integração** cobrindo todas as regras de negócio, limites e contratos:

```bash
dotnet test tests/DesafioReembolso.Tests/DesafioReembolso.Tests.csproj
```

**Principais áreas testadas:**
- Alíquotas e teto de R$ 500 por tipo de procedimento (`CalculadoraReembolsoTests.cs`).
- Limites de 90 dias e tratamento de datas futuras (`RegraPrazo90DiasTests.cs`).
- Regras de fraude de 5 pedidos e teto de R$ 1.500 em 30 dias (`MotorPrevencaoFraudeTests.cs`).
- Casos de conflito e ordenação determinística (`ProcessadorLoteServiceTests.cs`).
- Tratamento de CSV com BOM, variações de acentuação e formatos de data (`CsvServiceTests.cs`).
- Integração ponta a ponta da API via `WebApplicationFactory` (`ReembolsosApiTests.cs`).
- Simulação de throughput e auditoria de lote massivo com 10.000 pedidos (`SimulacaoLoteBenchmarkTests.cs`).

---

## 📦 Preparação para Entrega (.zip)

Conforme a especificação do desafio, o arquivo para envio deve conter todo o código-fonte, manifests e lockfiles de dependências, **sem** as pastas de compilação e cache (`bin`, `obj`, `node_modules` e `dist`), ficando com tamanho muito inferior ao limite de 50 MB (geralmente abaixo de 2 MB).

Para gerar o arquivo `.zip` limpo de entrega via terminal PowerShell:

```powershell
# No diretório raiz do projeto:
$itensParaIncluir = Get-ChildItem -Exclude "bin", "obj", "node_modules", "dist", ".vs", "*.zip"
Compress-Archive -Path $itensParaIncluir -DestinationPath "DesafioReembolso_Entrega.zip" -Force
```

