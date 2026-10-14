param([Parameter(Mandatory=$true)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
if (Get-Process MoneyTown -ErrorAction SilentlyContinue) { throw 'MoneyTown을 종료한 뒤 검사하세요.' }
$logDir = Join-Path $projectRoot 'build\installer-test'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$saveDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\MoneyTown\MoneyTown'
$preferencePath = 'HKCU:\Software\MoneyTown\MoneyTown'
$uninstallPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MoneyTown.Unity.Windows_is1'

function Get-ProgressSnapshot {
    $snapshot = [ordered]@{ Files = @(); Preferences = [ordered]@{} }
    if (Test-Path -LiteralPath $saveDir) {
        $snapshot.Files = @(Get-ChildItem -LiteralPath $saveDir -Filter 'game-unity-v1.bin*' -File |
            Sort-Object Name | ForEach-Object {
                [ordered]@{ Name = $_.Name; Hash = (Get-FileHash -LiteralPath $_.FullName).Hash }
            })
    }
    if (Test-Path $preferencePath) {
        $key = Get-Item $preferencePath
        foreach ($name in ($key.GetValueNames() | Where-Object { $_ -like 'MoneyTown.*' } | Sort-Object)) {
            $snapshot.Preferences[$name] = $key.GetValue($name)
        }
    }
    ConvertTo-Json -InputObject $snapshot -Depth 5 -Compress
}

$before = Get-ProgressSnapshot
$firstLocation = $null
$staleFile = $null
foreach ($pass in 1..2) {
    $setupLog = Join-Path $logDir "install-$pass.log"
    $process = Start-Process -FilePath $InstallerPath -ArgumentList @(
        '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/NOCLOSEAPPLICATIONS',"/LOG=`"$setupLog`""
    ) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "설치 $pass 실패: $($process.ExitCode). $setupLog" }
    if ((Get-ProgressSnapshot) -cne $before) { throw "설치 $pass 이후 저장 또는 게임 설정이 달라졌습니다." }
    if ($staleFile -and (Test-Path -LiteralPath $staleFile)) { throw '이전 빌드의 불필요한 파일이 남았습니다.' }
    $entry = Get-ItemProperty $uninstallPath
    $location = $entry.InstallLocation.TrimEnd('\')
    if ($firstLocation -and $location -ne $firstLocation) { throw '재설치 경로가 달라졌습니다.' }
    $firstLocation = $location
    $player = Join-Path $projectRoot 'dist\unity\Windows'
    $version = (Get-Content -LiteralPath (Join-Path $player 'build-version.txt') -Raw).Trim()
    if ($entry.DisplayVersion -ne $version) { throw '등록된 설치 버전이 다릅니다.' }
    $fileCount = 0
    foreach ($file in (Get-ChildItem -LiteralPath $player -Recurse -File)) {
        $relative = $file.FullName.Substring($player.Length).TrimStart('\')
        $destination = Join-Path $location $relative
        if (-not (Test-Path -LiteralPath $destination) -or
            (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
            throw "설치된 파일 불일치: $relative"
        }
        $fileCount++
    }
    if (-not (Test-Path -LiteralPath (Join-Path $location 'unins000.exe'))) { throw '제거 프로그램이 없습니다.' }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'MoneyTown\MoneyTown.lnk'
    if (-not (Test-Path -LiteralPath $shortcut)) { throw '시작 메뉴 바로가기가 없습니다.' }
    Write-Output "설치 $pass 통과: 파일 $fileCount 개 일치, 버전 $version, 기존 저장 및 설정 유지."
    if ($pass -eq 1) {
        $staleFile = Join-Path $location 'MoneyTown_Data\installer-stale-file-check.txt'
        if (Test-Path -LiteralPath $staleFile) { throw '검증용 임시 파일이 이미 존재합니다.' }
        Set-Content -LiteralPath $staleFile -Value 'Obsolete payload from a previous build.' -Encoding ascii
    }
}
Write-Output "설치 위치: $firstLocation"
