param([Parameter(Mandatory)][string]$Installer, [Parameter(Mandatory)][string]$LegacyInstaller)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Run only on a disposable GitHub Actions Windows runner; this test installs and uninstalls the app.' }
$root = Join-Path $env:RUNNER_TEMP 'glucogeek-installer-test'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$report = [Collections.Generic.List[string]]::new()
function Check([bool]$ok, [string]$label) {
    if (-not $ok) { throw $label }
    $report.Add("PASS $label")
}
function Run-Setup([string]$file, [string]$label) {
    $log = Join-Path $root ($label + '.log')
    $p = Start-Process -FilePath $file -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/TASKS="startup,desktopicon"', "/LOG=`"$log`"") -WindowStyle Hidden -Wait -PassThru
    Check ($p.ExitCode -eq 0) "$label installer completed"
}
function Stop-App {
    Get-Process -Name GlucoGeek,Librewin -ErrorAction SilentlyContinue | Stop-Process -Force
}
function Uninstall-App([string]$label) {
    Stop-App
    $uninstaller = Join-Path $install 'unins000.exe'
    Check (Test-Path -LiteralPath $uninstaller) "$label uninstaller exists"
    $p = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -WindowStyle Hidden -Wait -PassThru
    Check ($p.ExitCode -eq 0) "$label uninstall completed"
    Check (-not (Test-Path -LiteralPath (Join-Path $install 'GlucoGeek.exe'))) "$label executable removed"
    Check (-not (Test-Path -LiteralPath $newShortcut)) "$label Start menu shortcut removed"
    $key = Get-ItemProperty -LiteralPath $runKey
    Check (-not $key.GlucoGeek) "$label startup entry removed"
    Check ((Get-FileHash -LiteralPath $profile).Hash -eq $profileHash) "$label saved profile retained"
}
$install = Join-Path $env:LOCALAPPDATA 'Programs\Librewin'
$profile = Join-Path $env:LOCALAPPDATA 'Librewin\settings.json'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$newShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'GlucoGeek.lnk'
$oldShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Librewin.lnk'
$oldStartup = Join-Path ([Environment]::GetFolderPath('Startup')) 'Librewin.lnk'
$newDesktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'GlucoGeek.lnk'
Check (-not (Test-Path -LiteralPath $install)) 'runner has no previous installation'
Check (-not (Test-Path -LiteralPath $profile)) 'runner has no previous profile'
New-Item -ItemType Directory -Path (Split-Path $profile) -Force | Out-Null
Add-Type -AssemblyName System.Security
$encrypted = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes('synthetic-test-password'), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser))
# No LibreLinkUp password: launching the default source cannot contact its service.
@{Provider='LibreLinkUp';Email='test@example.invalid';PasswordProtected='';DexcomUsername='fixture';DexcomPasswordProtected=$encrypted;Unit='mmol/L';Minutes=5} | ConvertTo-Json | Set-Content -LiteralPath $profile
$profileHash = (Get-FileHash -LiteralPath $profile).Hash
try {
    Run-Setup $Installer 'fresh'
    $exe = Join-Path $install 'GlucoGeek.exe'
    Check (Test-Path -LiteralPath $exe) 'fresh executable installed'
    Check (Test-Path -LiteralPath (Join-Path $install 'LICENSE')) 'project MIT licence installed'
    Check (Test-Path -LiteralPath (Join-Path $install 'THIRD_PARTY_NOTICES.md')) 'upstream notices installed'
    Check (Test-Path -LiteralPath (Join-Path $install 'licenses\Microsoft.NETCore.App\THIRD-PARTY-NOTICES.TXT')) '.NET notices installed'
    Check (Test-Path -LiteralPath $newShortcut) 'Start menu shortcut installed'
    Check (Test-Path -LiteralPath $newDesktop) 'optional desktop shortcut installed'
    $wsh = New-Object -ComObject WScript.Shell
    Check ($wsh.CreateShortcut($newShortcut).TargetPath -eq $exe) 'shortcut targets the installed executable'
    $key = Get-ItemProperty -LiteralPath $runKey
    Check ($key.GlucoGeek -eq ('"' + $exe + '" --background')) 'startup uses the installed executable'
    Check (-not $key.Librewin) 'old startup value absent'
    Check ((Get-FileHash -LiteralPath $profile).Hash -eq $profileHash) 'fresh install preserves profile'
    $smoke = Join-Path $root 'installed-smoke.txt'
    $p = Start-Process -FilePath $exe -ArgumentList @('--smoke-test', ('"' + $smoke + '"')) -WindowStyle Hidden -Wait -PassThru
    Check ($p.ExitCode -eq 0 -and (Test-Path -LiteralPath $smoke)) 'installed self-contained app starts without a separate runtime'
    $p = Start-Process -FilePath $exe -ArgumentList '--background' -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $p.Refresh()
    Check (-not $p.HasExited) 'registered background startup command stays running'
    Check ((Get-Process -Id $p.Id).Path -eq $exe) 'background process runs from the installation'
    Stop-App
    Uninstall-App 'fresh'
    Check (-not (Test-Path -LiteralPath $newDesktop)) 'desktop shortcut removed'

    Run-Setup $LegacyInstaller 'legacy'
    $legacyExe = Join-Path $install 'Librewin.exe'
    Check (Test-Path -LiteralPath $legacyExe) 'legacy Librewin installed'
    Check (Test-Path -LiteralPath $oldShortcut) 'legacy shortcut exists before upgrade'
    # Reproduce an old Startup-folder launch as well as its registry registration.
    $link = $wsh.CreateShortcut($oldStartup)
    $link.TargetPath = $legacyExe
    $link.Save()
    Run-Setup $Installer 'upgrade'
    Check (-not (Test-Path -LiteralPath $legacyExe)) 'upgrade removes the old executable'
    Check (-not (Test-Path -LiteralPath $oldShortcut)) 'upgrade removes old Start menu shortcut'
    Check (-not (Test-Path -LiteralPath $oldStartup)) 'upgrade removes duplicate Startup shortcut'
    Check (Test-Path -LiteralPath $exe) 'upgrade installs GlucoGeek'
    Check ((Get-FileHash -LiteralPath $profile).Hash -eq $profileHash) 'upgrade preserves protected credentials and preferences'
    $saved = Get-Content -LiteralPath $profile -Raw | ConvertFrom-Json
    $plain = [Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($saved.DexcomPasswordProtected), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser))
    Check ($plain -eq 'synthetic-test-password') 'protected credential remains decryptable after upgrade'
    $key = Get-ItemProperty -LiteralPath $runKey
    Check (-not $key.Librewin -and $key.GlucoGeek -eq ('"' + $exe + '" --background')) 'upgrade leaves one canonical startup entry'
    Check ($wsh.CreateShortcut($newShortcut).TargetPath -eq $exe) 'upgrade shortcut targets GlucoGeek'
    Uninstall-App 'upgrade'
    Run-Setup $Installer 'reinstall'
    Check ((Get-FileHash -LiteralPath $profile).Hash -eq $profileHash) 'reinstall preserves saved profile'
    Uninstall-App 'reinstall'
} finally {
    Stop-App
    $report | Set-Content -LiteralPath (Join-Path $root 'results.txt')
    $report | Write-Output
}
