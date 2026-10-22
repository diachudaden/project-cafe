param(
    [string] $Url = "http://localhost:8080"
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$envPath = Join-Path $repoRoot ".env"
$connectionKey = "ConnectionStrings__DefaultConnection"

if (-not (Test-Path -LiteralPath $envPath)) {
    throw "Missing .env file at repository root."
}

$connectionLine = Get-Content -LiteralPath $envPath |
    Where-Object { $_ -match "^\s*$([regex]::Escape($connectionKey))\s*=" } |
    Select-Object -First 1

if (-not $connectionLine) {
    throw "Missing $connectionKey in .env."
}

$connectionString = ($connectionLine -split "=", 2)[1].Trim()
if (($connectionString.StartsWith('"') -and $connectionString.EndsWith('"')) -or
    ($connectionString.StartsWith("'") -and $connectionString.EndsWith("'"))) {
    $connectionString = $connectionString.Substring(1, $connectionString.Length - 2)
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "$connectionKey in .env is empty."
}

Set-Item -Path "Env:$connectionKey" -Value $connectionString
$apiDirectory = Join-Path $repoRoot "backend\src\ChonRieng.Api"
Set-Location $apiDirectory

dotnet run --urls $Url
exit $LASTEXITCODE
