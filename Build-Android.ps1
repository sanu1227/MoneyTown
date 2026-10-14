param([string]$JavaHome = "$env:ProgramFiles\Android\Android Studio\jbr")
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath "$JavaHome\bin\java.exe")) {
    throw 'Android Studio의 JBR 경로를 -JavaHome 인자로 지정하세요.'
}
$previousJava = $env:JAVA_HOME
try {
    $env:JAVA_HOME = $JavaHome
    Push-Location (Join-Path $PSScriptRoot 'android')
    try {
        & .\gradlew.bat :app:testDebugUnitTest :app:assembleDebug :app:lintDebug --console=plain
        if ($LASTEXITCODE -ne 0) { throw 'Android 검증 또는 빌드에 실패했습니다.' }
    } finally { Pop-Location }
    $output = Join-Path $PSScriptRoot 'dist'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'android\app\build\outputs\apk\debug\app-debug.apk') -Destination (Join-Path $output 'MoneyTown-debug.apk')
    Write-Output "APK: $output\MoneyTown-debug.apk"
} finally { $env:JAVA_HOME = $previousJava }
