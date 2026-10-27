param(
    [string]$Password = "local-only"
)

$ErrorActionPreference = "Stop"
$certDir = Join-Path $PSScriptRoot "..\certs"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
$certPath = Join-Path $certDir "vinheria.pfx"

dotnet dev-certs https -ep $certPath -p $Password
Write-Host "Certificado criado em $certPath"
Write-Host "Use: docker compose -f docker-compose.yml -f docker-compose.secure.yml up --build"
