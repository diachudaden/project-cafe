param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Add", "Script", "Update")]
    [string] $Action,

    [string] $Name = "AddPayments",

    [string] $Output = ".\backend\src\ChonRieng.Infrastructure\Migrations\AddPayments.sql",

    [string] $ConfirmTargetDatabase,

    [switch] $ConfirmEmptyDevelopmentDatabase
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
Set-Location $repoRoot

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$project = ".\backend\src\ChonRieng.Infrastructure\ChonRieng.Infrastructure.csproj"
$arguments = @(
    "ef",
    "migrations"
)

if ($Action -eq "Add") {
    $arguments += @("add", $Name, "--project", $project, "--startup-project", $project)
} elseif ($Action -eq "Script") {
    $arguments += @(
        "script",
        "--idempotent",
        "--project", $project,
        "--startup-project", $project,
        "--output", $Output
    )
} else {
    if (-not $ConfirmEmptyDevelopmentDatabase -or [string]::IsNullOrWhiteSpace($ConfirmTargetDatabase)) {
        throw "Update requires -ConfirmEmptyDevelopmentDatabase and -ConfirmTargetDatabase after reviewing the SQL script and verifying the target."
    }

    $connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connectionBuilder.ConnectionString = $connectionString
    $databaseKey = if ($connectionBuilder.ContainsKey("Initial Catalog")) { "Initial Catalog" } else { "Database" }
    $targetDatabase = [string] $connectionBuilder[$databaseKey]

    if ($targetDatabase -cne $ConfirmTargetDatabase) {
        throw "The confirmed database name does not match the database configured in .env."
    }

    if ($targetDatabase -match "(?i)prod|production|live") {
        throw "Refusing to update a database whose name indicates production."
    }

    $arguments = @(
        "ef",
        "database",
        "update",
        "--project", $project,
        "--startup-project", $project
    )
}

dotnet @arguments
exit $LASTEXITCODE
