param(
    [ValidateRange(320,1200)][int]$Width = 480,
    [ValidateRange(640,2400)][int]$Height = 960,
    [switch]$Visible
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$player = Join-Path $project 'dist\unity\Verification\MoneyTown.exe'
if (-not (Test-Path -LiteralPath $player)) {
    throw '격리 UI 검증 빌드가 필요합니다: .\Build-Unity.ps1 -Target Verification'
}
$output = Join-Path $project ('build\ui-verification-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output | Out-Null
$previousDirectory = $env:MONEYTOWN_VERIFY_DIR
try {
    $env:MONEYTOWN_VERIFY_DIR = $output
    $arguments = @('-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0', '-logFile', ('"' + (Join-Path $output 'player.log') + '"'))
    $windowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
    $process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
} finally {
    $env:MONEYTOWN_VERIFY_DIR = $previousDirectory
}
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(1000)) {
    if ($timer.Elapsed.TotalSeconds -gt 90) {
        $process.Kill()
        throw "UI 검증 시간 초과: $output"
    }
}
$reportPath = Join-Path $output 'ui-verification.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw "UI 검증 결과가 없습니다: $output" }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (-not $report.passed) { throw "UI 검증 실패: $($report.error) / $output" }
Write-Output "Unity UI 검증 통과: $($report.checks.Count)개 / $output"
