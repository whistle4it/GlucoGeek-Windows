param([string]$Iscc = 'ISCC.exe', [string]$OutputDirectory = (Join-Path $PSScriptRoot 'installer-output'))
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    [xml]$project = Get-Content -LiteralPath 'GlucoGeek.csproj'
    $version = [string]$project.Project.PropertyGroup.Version
    if (-not $version) { throw 'Missing project version' }
    dotnet publish GlucoGeek.csproj -c Release -r win-x64 --self-contained true -o dist-installer
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained build failed' }
    $publish = (Resolve-Path -LiteralPath 'dist-installer').Path
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $output = (Resolve-Path -LiteralPath $OutputDirectory).Path
    & $Iscc "/DAppVersion=$version" "/DPublishDir=$publish" "/DInstallerOutputDir=$output" 'installer\GlucoGeek.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $installer = Join-Path $output "GlucoGeek-v$version-Setup-x64.exe"
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii
    Get-Item -LiteralPath $installer
} finally { Pop-Location }
