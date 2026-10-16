# 개발 환경과 빌드

[첫 화면](../README.md) · [코드 구조](architecture.md) · [자료실](README.md)

명령은 저장소 루트 `MoneyTown`에서 실행합니다. 주 프로젝트는 `unity/`입니다.

## 준비물

| 작업 | 필요한 도구 |
| --- | --- |
| 에디터 실행 | Unity Hub, 활성화된 Unity 라이선스, **Unity 6000.3.26f1** |
| Android APK | 에디터의 Android Build Support, Android SDK·NDK·OpenJDK |
| Windows 앱 | 에디터의 Windows 빌드 지원 |
| 설치 EXE | Windows 앱 빌드와 **Inno Setup 6 이상** |
| Unity 없이 코어 검사 | **.NET 10 SDK** |
| 정보 카탈로그 생성·검사 | Python 3 |
| 이전 웹 규칙 검사 | Node.js |

검증한 로컬 Android 도구 조합은 NDK r27c와 Unity 배포 OpenJDK 17입니다. 필요한 경로는 개발 환경에 맞춰 설정합니다. 에디터와 배치 빌드로 같은 Unity 프로젝트를 동시에 열지 않습니다.

## Unity에서 실행

1. Unity Hub에서 저장소의 `unity` 폴더를 추가합니다.
2. 프로젝트를 열고 패키지와 한국어 글꼴을 가져올 때까지 기다립니다.
3. 필요하면 `MoneyTown > Prepare Project`를 실행합니다.
4. `Assets/Scenes/Main.unity`를 열고 Play를 누릅니다. 세로 Game 뷰로 확인합니다.
5. `MoneyTown > Run Core Checks`로 게임 규칙을 검사합니다.

## Windows 앱과 설치 파일

```powershell
# Windows 실행 파일만 만들기
.\Build-Unity.ps1 -Target Windows

# Windows 빌드와 설치 EXE를 함께 만들기
.\Build-Installer.ps1

# 이미 Windows 빌드를 완료했다면 패키징만 실행
.\Build-Installer.ps1 -SkipUnityBuild
```

결과는 `dist/unity/Windows/MoneyTown.exe`와 `dist/installer/MoneyTown-0.20.1-Setup-x64.exe`입니다. 버전은 프로젝트 설정에 따라 달라집니다. 설치 파일 옆에 SHA-256 파일도 생성합니다.

설치 위치는 `%LOCALAPPDATA%\Programs\MoneyTown`입니다. 게임을 종료한 뒤 새 설치 EXE를 실행하면 기존 설치 위치와 저장·설정을 유지합니다. 인터넷 자동 다운로드 기능은 없습니다. 설치 EXE에는 코드 서명을 적용하지 않았습니다.

아래 검사는 **현재 Windows 계정에 실제 설치·재설치**합니다. 실행 중인 게임을 먼저 종료하세요.

```powershell
.\scripts\Test-Installer.ps1 -InstallerPath .\dist\installer\MoneyTown-0.20.1-Setup-x64.exe
```

## Android APK

```powershell
.\Build-Unity.ps1 -Target Android
```

결과는 `dist/unity/MoneyTown-Unity.apk`입니다. 패키지 ID는 `com.moneytown.unity`이며 Android 8.0 이상 ARM64를 대상으로 합니다. 현재 개발·테스트용 서명이며 실제 Android 기기 검증은 남아 있습니다.

별도 도구를 설치한 환경은 해당 경로를 지정할 수 있습니다.

```powershell
$env:MONEYTOWN_ANDROID_SDK = 'C:\Android\Sdk'
$env:MONEYTOWN_ANDROID_NDK = 'C:\Android\android-ndk-r27c'
$env:MONEYTOWN_ANDROID_JDK = 'C:\Android\OpenJDK'
.\Build-Unity.ps1 -Target Android
```

위 경로는 예시입니다. 에디터 자동 탐색에 실패하면 `-UnityPath '설치경로\Editor\Unity.exe'`를 추가합니다. Inno Setup을 찾지 못하면 `Build-Installer.ps1 -CompilerPath '설치경로\ISCC.exe'`로 지정합니다. Unity 라이선스 인식 실패 시 Hub를 정상 실행해 활성화 상태를 확인합니다.

## 검사와 실제 화면 캡처

```powershell
# Unity 없이 같은 C# 규칙 검사
dotnet run --project tests/unity-core/UnityCoreChecks.csproj

# Unity 컴파일과 코어 검사
.\Build-Unity.ps1 -Target Check

# 본게임 저장을 사용하지 않는 검증용 Player
.\Build-Unity.ps1 -Target Verification
.\scripts\Test-UnityUI.ps1 -Width 480 -Height 960 -Visible
.\scripts\Test-UnityUI.ps1 -Width 360 -Height 800 -Visible
```

결과 JSON과 PNG는 `build/ui-verification-*`에 생성됩니다. 화면 캡처에는 `-Visible`을 사용합니다. 검증 빌드는 `MoneyTownVerification`이라는 별도 제품 설정과 임시 데이터 경로를 사용합니다. 화면의 `Development Build` 표시는 원본 그대로 보존합니다.

게임 소개 캡처를 갱신할 때는 통과한 실행의 PNG를 `assets/screenshots/버전/`에 복사하고 [갤러리의 촬영 정보](gallery.md#촬영-정보)를 함께 바꿉니다. 실제 게임 화면과 별도 브랜드 일러스트를 구분합니다.

## 정보 콘텐츠 수정

사건 원안은 `content/`의 PSV 파일에 있습니다. 취재 묘사와 구어체 표현은 별도 파일로 관리합니다.

```powershell
python scripts/build_information_catalog.py
python scripts/test_information_catalog.py
```

생성기는 Unity 카탈로그와 문서를 함께 갱신합니다. 이미 구매한 정보의 내용이 바뀌지 않도록 과거 발행판은 보존하고, 사건·수치·공개 범위를 바꿀 때는 발행판과 호환성을 검토합니다. [정보 목록](information-catalog/README.md)

다음 도구로 게임 코어의 사건 연관성과 전략별 결과를 비교할 수 있습니다.

```powershell
dotnet run --project tests/market-correlation/MarketCorrelation.csproj -- 2000 1
dotnet run --project tests/strategy-comparison/StrategyComparison.csproj
```

측정 해석과 조건은 각각 [연관성 측정 문서](../tests/market-correlation/README.md)와 [전략 비교 문서](../tests/strategy-comparison/README.md)를 확인합니다.

## 저장과 버전 관리

- PC 진행 파일: `%USERPROFILE%\AppData\LocalLow\MoneyTown\MoneyTown\game-unity-v1.bin`
- PC 설정: `HKEY_CURRENT_USER\Software\MoneyTown\MoneyTown`
- v0.19.0의 저장 형식 10은 현재 날짜의 가격 모델을 추가했습니다. 형식 1~9의 다음 개장 한 번은 모델 3으로 진행하고 새 날짜부터 모델 4로 전환합니다. 이 가격 반응 규칙과 아래 가격 기준 조정은 별개입니다.
- v0.20.0은 새 게임의 초기 가격을 1천~600만원으로 구분했습니다. v0.20.1은 저장 형식 **11**에서 지원하는 이전 형식 **1~10 전체**의 현재가를 새 기준가로 한 번 맞춥니다. v0.20.0에서 새로 생성한 형식 10 저장도 구별할 표식이 없어 동일하게 적용합니다.
- 기업별 `V = 기존 현재가 × 보유 수량`, `새 수량 = V / 새 기준가`의 내림값, `R = V % 새 기준가`로 계산합니다. `R`은 무수수료 현금 정산하고 `floor(기존 원가 × R / V)`만큼 원가를 차감합니다. 평가액 0은 별도로 처리합니다. 실현손익과 `DayStartRealized`에 정산 손익을 함께 반영해 총자산·총손익·당일 손익을 보존합니다. 중간 곱셈은 `BigInteger`를 사용합니다.
- 차트는 동일 비율로 새 가격 기준에 맞추며 기존 거래·결산·구매 가격 스냅샷은 원본으로 남깁니다. 과거 자료는 가격 기준 조정 이전임을 안내합니다. 날짜·예금 원금/만기·시장 난수는 유지하며 진행을 초기화하지 않습니다.
- 이전 파일을 먼저 검증하고 `before-price-update` 원본 사본을 남긴 뒤 원자적 저장이 성공한 경우에만 조정 상태를 적용합니다. 형식 11로 적용 상태를 저장해 재실행 시 중복 정산을 막습니다. 정산 금액·실현손익의 한도를 넘거나 저장이 실패하면 원본을 보존하며, 자산이나 손익을 잘라 한도에 맞추지 않습니다. 검증에서는 총자산 보존·정산 원가·중복 방지·원본 사본·저장 실패 경로를 함께 확인합니다.
- Android·Unity 에디터·PC의 저장은 별개일 수 있습니다. 이전 Kotlin·웹 저장은 자동 이전하지 않습니다.
- 소스에는 Unity `Assets`·`.meta`·`Packages`·`ProjectSettings`와 콘텐츠·검증·문서를 포함합니다. 캐시·로그·개인 설정·서명 키·빌드 결과는 제외합니다.
- 기능별 검증과 개발 노트를 마친 뒤 한국어 메시지로 커밋하고 `origin`에 일반 push합니다. 자세한 규칙은 [AGENTS.md](../AGENTS.md)를 따릅니다.

## 이전 구현

`android/`는 Kotlin 앱, `prototype/`은 웹 프로토타입 보관용입니다. 이전 거래 규칙 검사는 `node --test tests/market.test.cjs`로 실행합니다. 현재 게임 기능은 Unity에서 개발합니다. 과거 환경과 이식 과정은 [개발 노트](development-log.md)에 남아 있습니다.
