<#
.SYNOPSIS
    Executa o fluxo completo de segurança da Vinheria para demonstração.

.DESCRIPTION
    1. Chamada protegida SEM token          -> espera 401
    2. Login com usuário e senha             -> recebe JWT
    3. Consulta de estoque COM token         -> 200
    4. Criação de pedido COM token           -> 201 (Pedidos chama Estoque com o mesmo JWT)
    5. Chamada com token adulterado          -> espera 401

    Exemplos:
      .\scripts\demo-fluxo.ps1                       # HTTP  (http://localhost:5000)
      .\scripts\demo-fluxo.ps1 -Https                # HTTPS (https://localhost:5440)
#>
param(
    [switch]$Https,
    [string]$Usuario = "demo",
    [string]$Senha = "vinheria-demo"
)

$ErrorActionPreference = "Stop"
$base = if ($Https) { "https://localhost:5440" } else { "http://localhost:5000" }

# Certificado de laboratório: aceito apenas neste script de demonstração.
$extra = @{}
if ($Https) {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $extra.SkipCertificateCheck = $true
    } else {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    }
}

function Chamar($titulo, $metodo, $url, $headers = @{}, $corpo = $null) {
    Write-Host ""
    Write-Host "== $titulo" -ForegroundColor Yellow
    Write-Host "   $metodo $url"
    try {
        $params = @{ Method = $metodo; Uri = $url; Headers = $headers } + $extra
        if ($corpo) { $params.Body = $corpo; $params.ContentType = "application/json" }
        $resposta = Invoke-RestMethod @params
        Write-Host "   -> sucesso" -ForegroundColor Green
        $resposta | ConvertTo-Json -Depth 5 | Write-Host
        return $resposta
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "   -> HTTP $status (bloqueado)" -ForegroundColor Red
        return $null
    }
}

Write-Host "Ambiente: $base" -ForegroundColor Cyan

Chamar "1. Estoque SEM token" GET "$base/estoque/v1/estoque" | Out-Null

$login = Chamar "2. Login (usuario e senha trafegam no corpo da requisicao)" POST "$base/auth/v1/auth/token" @{} (@{ username = $Usuario; password = $Senha } | ConvertTo-Json)
if (-not $login) { throw "Não foi possível obter o token." }
$auth = @{ Authorization = "Bearer $($login.access_token)" }

Chamar "3. Estoque COM token" GET "$base/estoque/v1/estoque" $auth | Out-Null
Chamar "4. Pedido COM token (Pedidos -> Estoque -> Mensageria)" POST "$base/pedidos/v1/pedidos" $auth (@{ sku = "vinho-tinto"; quantity = 1 } | ConvertTo-Json) | Out-Null

$partes = $login.access_token.Split(".")
$adulterado = "$($partes[0]).$($partes[1])x.$($partes[2])"
Chamar "5. Token ADULTERADO (assinatura nao confere)" GET "$base/estoque/v1/estoque" @{ Authorization = "Bearer $adulterado" } | Out-Null

Write-Host ""
Write-Host "Payload do JWT (Base64, NAO criptografado):" -ForegroundColor Cyan
$payload = $partes[1].Replace('-', '+').Replace('_', '/')
$payload += "=" * ((4 - $payload.Length % 4) % 4)
[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload)) | Write-Host