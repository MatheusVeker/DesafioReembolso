using namespace System.Diagnostics
using namespace System.Text
using namespace System.IO

$dataRef = [System.DateOnly]::Parse("2024-05-10")
$totalRegistros = 10000
$rng = [System.Random]::new(42)

Write-Host "=== GERANDO MASSA DE DADOS COM $totalRegistros PEDIDOS ===" -ForegroundColor Cyan
$swGeracao = [Stopwatch]::StartNew()

$procedimentos = @("Consulta Médica", "Exame de Imagem", "Exame Laboratorial", "Outros")
$csvBuilder = [StringBuilder]::new()
[void]$csvBuilder.AppendLine("Id,TipoProcedimento,DataProcedimento,ValorPago,ClienteId")

# Criamos ~2500 clientes
# 10% dos clientes farão muitos pedidos ou pedidos com valores altos para testar regras de fraude
for ($i = 1; $i -le $totalRegistros; $i++) {
    $proc = $procedimentos[$rng.Next(0, $procedimentos.Length)]
    
    # 20% fora do prazo (mais de 90 dias atrás), 80% dentro do prazo
    $ehForaDoPrazo = ($rng.NextDouble() -lt 0.20)
    if ($ehForaDoPrazo) {
        # Entre 91 e 180 dias atrás
        $diasAtras = $rng.Next(91, 180)
    } else {
        # Entre 0 e 90 dias atrás
        $diasAtras = $rng.Next(0, 91)
    }
    
    $dataProc = $dataRef.AddDays(-$diasAtras).ToString("yyyy-MM-dd")
    
    # Valores de R$ 50 a R$ 2000
    $valorPago = [Math]::Round($rng.Next(50, 2000) + $rng.NextDouble(), 2)
    
    # Distribuição de clientes
    if ($i % 15 -eq 0) {
        # Cliente "recorrente" para disparar regras de fraude
        $clienteId = "CLI_SUSP_" + $rng.Next(1, 40)
    } else {
        $clienteId = "CLI_" + $rng.Next(100, 3000)
    }
    
    [void]$csvBuilder.AppendLine("$i,$proc,$dataProc,$valorPago,$clienteId")
}

$caminhoEntrada = "exemplos\pedidos_10000.csv"
[File]::WriteAllText($caminhoEntrada, $csvBuilder.ToString(), [Encoding]::UTF8)
$swGeracao.Stop()
$tamanhoKb = [Math]::Round((Get-Item $caminhoEntrada).Length / 1024, 2)
Write-Host "Arquivo gerado em: $caminhoEntrada ($tamanhoKb KB) em $($swGeracao.ElapsedMilliseconds) ms" -ForegroundColor Green

Write-Host "`n=== EXECUTANDO O PROCESSAMENTO VIA CLI (.NET 10) ===" -ForegroundColor Cyan
$swExecucao = [Stopwatch]::StartNew()

$psi = [ProcessStartInfo]::new()
$psi.FileName = "dotnet"
$psi.Arguments = "run --project src/DesafioReembolso.Cli -c Release -- -d 2024-05-10"
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.StandardInputEncoding = [Encoding]::UTF8
$psi.StandardOutputEncoding = [Encoding]::UTF8

$processo = [Process]::Start($psi)

# Escreve o CSV na entrada padrão do processo
$writer = $processo.StandardInput
$writer.Write($csvBuilder.ToString())
$writer.Close()

# Lê a saída gerada
$saidaCsv = $processo.StandardOutput.ReadToEnd()
$erros = $processo.StandardError.ReadToEnd()
$processo.WaitForExit()
$swExecucao.Stop()

if ($processo.ExitCode -ne 0) {
    Write-Host "Erro na execução da CLI: $erros" -ForegroundColor Red
    exit 1
}

$caminhoSaida = "exemplos\resultado_10000.csv"
[File]::WriteAllText($caminhoSaida, $saidaCsv, [Encoding]::UTF8)

Write-Host "`n=== ANALISANDO MÉTRICAS DO LOTE PROCESSADO ===" -ForegroundColor Cyan
$linhas = $saidaCsv.Split("`n", [System.StringSplitOptions]::RemoveEmptyEntries)
$totalRetornado = $linhas.Length - 1 # desconsidera cabeçalho

$totalAprovados = 0
$totalRejeitados = 0
$totalSuspeitos = 0

$valorTotalPago = 0.0
$valorTotalReembolsado = 0.0
$valorAprovadoEfetivo = 0.0

$procedimentosCount = @{}

for ($j = 1; $j -le $totalRetornado; $j++) {
    $colunas = $linhas[$j].Trim().Split(",")
    if ($colunas.Length -ge 7) {
        $proc = $colunas[1]
        $vPago = [double]::Parse($colunas[3], [System.Globalization.CultureInfo]::InvariantCulture)
        $vReemb = [double]::Parse($colunas[4], [System.Globalization.CultureInfo]::InvariantCulture)
        $status = $colunas[6].Trim()

        $valorTotalPago += $vPago
        $valorTotalReembolsado += $vReemb

        if (-not $procedimentosCount.ContainsKey($proc)) {
            $procedimentosCount[$proc] = 0
        }
        $procedimentosCount[$proc]++

        switch ($status) {
            "Aprovado" {
                $totalAprovados++
                $valorAprovadoEfetivo += $vReemb
            }
            "Rejeitado" {
                $totalRejeitados++
            }
            "Suspeito de Fraude" {
                $totalSuspeitos++
            }
        }
    }
}

$tempoTotalMs = $swExecucao.ElapsedMilliseconds
$throughput = [Math]::Round(($totalRetornado / ($tempoTotalMs / 1000.0)), 2)

Write-Host "--------------------------------------------------------" -ForegroundColor Yellow
Write-Host "MÉTRICAS DE DESEMPENHO E THROUGHPUT" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------" -ForegroundColor Yellow
Write-Host "Total de Pedidos Processados: $totalRetornado"
Write-Host "Tempo Total de Execução da CLI: $tempoTotalMs ms ($([Math]::Round($tempoTotalMs/1000, 2)) s)"
Write-Host "Throughput / Vazão: $throughput pedidos/segundo"
Write-Host "Tempo Médio por Pedido: $([Math]::Round($tempoTotalMs / $totalRetornado, 4)) ms/pedido"

Write-Host "`n--------------------------------------------------------" -ForegroundColor Yellow
Write-Host "MÉTRICAS DE STATUS DE PROCESSAMENTO" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------" -ForegroundColor Yellow
Write-Host ("Aprovados:          {0,6} ({1,5:P2})" -f $totalAprovados, ($totalAprovados/$totalRetornado)) -ForegroundColor Green
Write-Host ("Rejeitados (>90d):  {0,6} ({1,5:P2})" -f $totalRejeitados, ($totalRejeitados/$totalRetornado)) -ForegroundColor Red
Write-Host ("Suspeitos Fraude:   {0,6} ({1,5:P2})" -f $totalSuspeitos, ($totalSuspeitos/$totalRetornado)) -ForegroundColor Magenta

Write-Host "`n--------------------------------------------------------" -ForegroundColor Yellow
Write-Host "MÉTRICAS FINANCEIRAS (R$)" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------" -ForegroundColor Yellow
Write-Host ("Total Solicitado (Valor Pago):  R$ {0,12:N2}" -f $valorTotalPago)
Write-Host ("Total Aprovado para Reembolso:  R$ {0,12:N2}" -f $valorAprovadoEfetivo) -ForegroundColor Green
$economia = $valorTotalPago - $valorAprovadoEfetivo
Write-Host ("Economia / Retenção de Risco:   R$ {0,12:N2} ({1,5:P2})" -f $economia, ($economia / $valorTotalPago)) -ForegroundColor Cyan
if ($totalAprovados -gt 0) {
    Write-Host ("Ticket Médio Aprovado:          R$ {0,12:N2}" -f ($valorAprovadoEfetivo / $totalAprovados))
}

Write-Host "`n--------------------------------------------------------" -ForegroundColor Yellow
Write-Host "DISTRIBUIÇÃO POR PROCEDIMENTO" -ForegroundColor Yellow
Write-Host "--------------------------------------------------------" -ForegroundColor Yellow
foreach ($k in $procedimentosCount.Keys) {
    Write-Host ("{0,-20}: {1,5} pedidos ({2,5:P2})" -f $k, $procedimentosCount[$k], ($procedimentosCount[$k]/$totalRetornado))
}
