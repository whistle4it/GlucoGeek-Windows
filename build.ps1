param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish GlucoGeek.csproj -c Release --self-contained false -o dist
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Test) {
        $result = Join-Path $PSScriptRoot 'test-results.txt'
        $process = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\GlucoGeek.exe') -ArgumentList @('--self-test', ('"' + $result + '"')) -WindowStyle Hidden -Wait -PassThru
        Get-Content -LiteralPath $result
        if ($process.ExitCode -ne 0) { throw 'Tests failed' }
    }
} finally { Pop-Location }
