param([switch]$KeepRunning)

$ErrorActionPreference = 'Stop'
$testCompose = @('compose', '-p', 'btsa-integration', '-f', 'docker-compose.yaml', '-f', 'docker-compose.integration.yaml')
$previousConnection = $env:BTSA_TEST_CONNECTION
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & docker info --format '{{.ServerVersion}}'
    if ($LASTEXITCODE -ne 0) { throw 'Inicie o Docker Desktop antes de executar os testes.' }
    & docker @testCompose up --build -d
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar o Compose de integração.' }

    $env:BTSA_TEST_CONNECTION = 'Host=127.0.0.1;Port=25432;Database=btsa_integration_test;Username=btsa;Password=btsa-test'
    & dotnet test TesteTecnico.Api.Tests/TesteTecnico.Api.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'A suíte PostgreSQL falhou.' }
    & python -X utf8 scripts/integration_test.py
    if ($LASTEXITCODE -ne 0) { throw 'A suíte HTTP/Hangfire/RabbitMQ falhou.' }
}
finally {
    $env:BTSA_TEST_CONNECTION = $previousConnection
    if (-not $KeepRunning) {
        # Apenas recursos do projeto descartável, sem afetar o Compose de desenvolvimento.
        & docker @testCompose down --volumes --remove-orphans
    }
    Pop-Location
}
