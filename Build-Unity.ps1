param(
    [ValidateSet('Check','Android','Windows','Verification','Editor')][string]$Target = 'Check',
    [string]$UnityPath
)
$ErrorActionPreference = 'Stop'
if (-not $UnityPath) {
    $candidates = @(
        (Join-Path $PSScriptRoot '..\Tools\Unity\6000.3.26f1\Editor\Unity.exe'),
        "$env:ProgramFiles\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
    )
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw 'Unity 6000.3.26f1을 설치하거나 -UnityPath로 Editor\Unity.exe 경로를 지정하세요.'
}
$method = switch ($Target) {
    'Android' { 'MoneyTown.Editor.ProjectSetup.BuildAndroid' }
    'Windows' { 'MoneyTown.Editor.ProjectSetup.BuildWindows' }
    'Verification' { 'MoneyTown.Editor.ProjectSetup.BuildVerification' }
    default { 'MoneyTown.Editor.ProjectSetup.Check' }
}
$logs = Join-Path $PSScriptRoot 'unity\Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$log = Join-Path $logs "build-$Target.log"
$project = Join-Path $PSScriptRoot 'unity'
if ($Target -eq 'Editor') {
    Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath',"`"$project`"")
    return
}
$args = @('-batchmode','-quit','-projectPath',"`"$project`"",'-executeMethod',$method,'-logFile',"`"$log`"")
if ($Target -eq 'Android') { $args += @('-buildTarget','Android') }
if ($Target -in @('Windows','Verification')) { $args += @('-buildTarget','Win64') }
$process = Start-Process -FilePath $UnityPath -ArgumentList $args -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -eq 198) { throw "Unity 라이선스를 인식하지 못했습니다. Unity Hub를 정상 실행해 기존 라이선스를 갱신한 뒤 다시 빌드하세요. 로그: $log" }
if ($process.ExitCode -ne 0) { throw "Unity 검증 또는 빌드 실패. 로그: $log" }
Write-Output "Unity $Target 성공. 로그: $log"
