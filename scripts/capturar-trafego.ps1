<#
.SYNOPSIS
    Captura o tráfego de rede de um microsserviço para abrir no Wireshark.

.DESCRIPTION
    O Wireshark do Windows não enxerga a rede interna do Docker. Este script
    roda o tcpdump "dentro" da rede do container escolhido (mesmo namespace de
    rede) e grava um arquivo .pcap na pasta capturas/, que depois é aberto no
    Wireshark.

    Exemplos:
      # Modo HTTP: cliente -> Gateway e Gateway -> serviços (tudo na porta 8080)
      .\scripts\capturar-trafego.ps1 -Arquivo http-sem-criptografia

      # Modo HTTPS: externo na 8443 (TLS) e interno na 8080 (HTTP) no mesmo arquivo
      .\scripts\capturar-trafego.ps1 -Filtro "tcp port 8443 or tcp port 8080" -Arquivo https-externo

    Enquanto a captura roda, execute .\scripts\demo-fluxo.ps1 em outro terminal.
#>
param(
    [string]$Servico = "gateway-service",
    [string]$Filtro = "tcp port 8080",
    [int]$Segundos = 40,
    [string]$Arquivo = "captura"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$pasta = Join-Path $raiz "capturas"
New-Item -ItemType Directory -Force -Path $pasta | Out-Null

$container = docker ps --filter "label=com.docker.compose.service=$Servico" --format "{{.ID}}" | Select-Object -First 1
if (-not $container) {
    throw "Container do serviço '$Servico' não encontrado. Suba o ambiente com docker compose antes."
}

Write-Host "Capturando [$Filtro] na rede do serviço $Servico por $Segundos segundos..."
Write-Host "Execute agora o demo-fluxo.ps1 em outro terminal."

docker run --rm `
    --network "container:$container" `
    -v "${pasta}:/capturas" `
    nicolaka/netshoot:v0.13 `
    timeout $Segundos tcpdump -i any -nn -s 0 -w "/capturas/$Arquivo.pcap" $Filtro

Write-Host "Arquivo gerado: $(Join-Path $pasta "$Arquivo.pcap")"
Write-Host "Abra no Wireshark e use o filtro: http  (ou tls para o modo seguro)"