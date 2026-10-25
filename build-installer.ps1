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
    # Self-contained publishing omits package-root legal notices. Ship the notices
    # from the exact restored runtime versions alongside the runtime binaries.
    $runtime = Get-Content -LiteralPath (Join-Path $publish 'GlucoGeek.runtimeconfig.json') -Raw | ConvertFrom-Json
    $restore = Get-Content -LiteralPath 'obj\project.assets.json' -Raw | ConvertFrom-Json
    $packageRoots = @($restore.packageFolders.PSObject.Properties.Name)
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $package = ($framework.name + '.Runtime.win-x64').ToLowerInvariant()
        $packageDir = $null
        foreach ($packageRoot in $packageRoots) {
            $candidate = Join-Path $packageRoot ($package + '\' + $framework.version)
            if (Test-Path -LiteralPath $candidate) { $packageDir = $candidate; break }
        }
        if (-not $packageDir) { throw "Cannot find legal notices for $package $($framework.version)" }
        $noticeDir = Join-Path $publish ('licenses\' + $framework.name)
        New-Item -ItemType Directory -Path $noticeDir -Force | Out-Null
        $notices = @(Get-ChildItem -LiteralPath $packageDir -File | Where-Object { $_.Name -match '^(LICENSE(?:\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if ($notices.Count -eq 0) { throw "Runtime licence is missing: $package" }
        foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $noticeDir -Force }
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $output = (Resolve-Path -LiteralPath $OutputDirectory).Path
    & $Iscc "/DAppVersion=$version" "/DPublishDir=$publish" "/DInstallerOutputDir=$output" 'installer\GlucoGeek.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $installer = Join-Path $output "GlucoGeek-v$version-Setup-x64.exe"
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii
    Get-Item -LiteralPath $installer
} finally { Pop-Location }
