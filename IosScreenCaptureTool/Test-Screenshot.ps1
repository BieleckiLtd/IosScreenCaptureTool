param(
    [string]$OutputPath = ".\screenshots\stream-smoke-$(Get-Date -Format 'yyyyMMdd-HHmmss').png"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet run --project IosScreenCaptureTool -- --self-test $OutputPath

    if (-not (Test-Path $OutputPath)) {
        throw "Output file was not created: $OutputPath"
    }

    $file = Get-Item $OutputPath
    if ($file.Length -le 0) {
        throw "Output file is empty: $OutputPath"
    }

    Write-Host "PASS: Frame saved to $($file.FullName) ($($file.Length) bytes)."
}
finally {
    Pop-Location
}
