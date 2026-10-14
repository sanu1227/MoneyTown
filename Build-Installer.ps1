param(
    [switch]$SkipUnityBuild,
    [string]$UnityPath,
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'

if (-not $CompilerPath) {
    $compilerCandidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $uninstallRoots = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )
    $installed = Get-ChildItem $uninstallRoots -ErrorAction SilentlyContinue |
        Get-ItemProperty | Where-Object { $_.DisplayName -like '*Inno Setup*' -and $_.InstallLocation }
    foreach ($entry in $installed) { $compilerCandidates += Join-Path $entry.InstallLocation 'ISCC.exe' }
    $CompilerPath = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Inno Setup 6 이상을 설치하거나 -CompilerPath로 ISCC.exe를 지정하세요. https://jrsoftware.org/isdl.php'
}
if (-not $SkipUnityBuild) {
    & (Join-Path $PSScriptRoot 'Build-Unity.ps1') -Target Windows -UnityPath $UnityPath
}

$player = Join-Path $PSScriptRoot 'dist\unity\Windows'
foreach ($required in @('MoneyTown.exe','UnityPlayer.dll','MoneyTown_Data\globalgamemanagers','build-version.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $player $required))) {
        throw "Windows 빌드 파일이 없습니다: $required. Unity에서 MoneyTown > Build Windows Preview를 실행하세요."
    }
}
$version = (Get-Content -LiteralPath (Join-Path $player 'build-version.txt') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "잘못된 Windows 빌드 버전: $version" }
$settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'unity\ProjectSettings\ProjectSettings.asset') -Raw
if ($settings -notmatch '(?m)^  bundleVersion: (.+)$' -or $Matches[1].Trim() -ne $version) {
    throw 'Windows 빌드와 프로젝트 버전이 다릅니다. Windows 빌드를 다시 실행하세요.'
}
& $CompilerPath '/Qp' "/DAppVersion=$version" (Join-Path $PSScriptRoot 'packaging\windows-installer.iss')
if ($LASTEXITCODE -ne 0) { throw "설치 프로그램 빌드 실패: $LASTEXITCODE" }
$installer = Join-Path $PSScriptRoot "dist\installer\MoneyTown-$version-Setup-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw '설치 프로그램이 생성되지 않았습니다.' }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
Set-Content -LiteralPath "$installer.sha256" -Value "$hash  $([IO.Path]::GetFileName($installer))" -Encoding ascii
Write-Output "설치 프로그램: $installer"
Write-Output "SHA-256: $hash"
