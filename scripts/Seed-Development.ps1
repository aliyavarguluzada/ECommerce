# Run from any folder. Credentials are supplied through process environment variables.
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:SEED_POSTGRES_CONNECTION)) {
    throw 'Set SEED_POSTGRES_CONNECTION to your local PostgreSQL maintenance database connection first.'
}

$taskRoot = Split-Path -Parent $PSScriptRoot

Push-Location -LiteralPath $taskRoot

try {
    dotnet run --project tools/ECommerce.Seed/ECommerce.Seed.csproj '-p:Platform=AnyCPU'

    if ($LASTEXITCODE -ne 0) {
        throw "Seed verification failed (exit code $LASTEXITCODE). Completed databases can be reused on the next run."
    }
} finally {
    Pop-Location
}
