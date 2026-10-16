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
| 정보 카탈로그·효과음·배경음악 생성 및 검사 | Python 3 |
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

빌드 결과 경로는 `dist/unity/Windows/MoneyTown.exe`와 `dist/installer/MoneyTown-0.27.1-Setup-x64.exe`입니다. 버전은 프로젝트 설정에 따라 달라집니다. 설치 파일 옆에 SHA-256 파일도 생성합니다. 경로 예시는 해당 버전의 빌드·설치 검증 완료를 뜻하지 않습니다.

설치 위치는 `%LOCALAPPDATA%\Programs\MoneyTown`입니다. 게임을 종료한 뒤 새 설치 EXE를 실행하면 기존 설치 위치와 저장·설정을 유지합니다. 인터넷 자동 다운로드 기능은 없습니다. 설치 EXE에는 코드 서명을 적용하지 않았습니다.

아래 검사는 **현재 Windows 계정에 실제 설치·재설치**합니다. 실행 중인 게임을 먼저 종료하세요.

```powershell
.\scripts\Test-Installer.ps1 -InstallerPath .\dist\installer\MoneyTown-0.27.1-Setup-x64.exe
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

## 이야기와 일러스트 수정

장별 제목·해금 안내·세 페이지 대사·이미지 이름은 [`StoryChapters.cs`](../unity/Assets/Scripts/StoryChapters.cs)에 있습니다. 실제 해금 금액과 순차 확인 규칙은 [`Core/Story.cs`](../unity/Assets/Scripts/Core/Story.cs), 표시 우선순위·저장 재시도·다시 보기·움직임은 [`StoryScreen.cs`](../unity/Assets/Scripts/StoryScreen.cs)에서 관리합니다. 장 배열의 순서와 `Story.Thresholds`의 순서를 맞춥니다. 전체 구성은 [성장 이야기](story.md)를 참고하세요.

이미지는 `unity/Assets/Resources/StoryArt/`의 PNG 6장과 `.meta`입니다. `ArtName`에는 확장자를 제외한 파일명을 넣으며 Unity가 `Resources.Load<Texture2D>`로 읽습니다. 한국어 문장은 이미지에 넣지 않고 UI 텍스트로 표시합니다. 원본·크기·해시·생성 프롬프트와 임포트 설정은 [일러스트 제작 기록](story-art.md)에 보관합니다.

[`Core/StoryChecks.cs`](../unity/Assets/Scripts/Core/StoryChecks.cs)는 해금 경계·총자산·자산 하락·확인 순서·이전 저장 호환성을 검사합니다. [`StoryVerificationScreen.cs`](../unity/Assets/Scripts/StoryVerificationScreen.cs)는 Unity Player에서 화면 우선순위·시세 차단·저장 실패 재시도·다시 보기·본문 배치·그림·동작 줄이기를 확인합니다. 위 코어·Verification 명령에 포함되며, 이미지 자체 확인과 실제 화면 캡처를 함께 점검합니다.

0.27.1은 첫 문장만 전체 화면 중앙의 세 줄 자막으로 표시합니다. 문구 순서·금빛 강조·암전 해제, 연출 중 첫 클릭의 즉시 완성, 완성 후 클릭의 두 번째 페이지 이동을 확인합니다. 나머지 17페이지는 기존 아이보리 지면 안의 두 컷·캡션·말풍선을 유지합니다. 기존 장 일러스트를 두 구도로 잘라 쓰며 대사는 이미지와 분리합니다. 컷 등장 후 정지와 한 번 클릭의 다음 페이지 이동도 확인합니다.

공통으로 Unity 시작 로고가 끝난 뒤 연출 시작, 동작 줄이기의 즉시 완성, 앱 중단 시 연출 정지, 중앙 자막과 만화의 저장 실패 안내·건너뛰기·재감상을 점검합니다. 내레이션 부재와 기존 배경음악·효과음 유지도 확인합니다. 검증 수치와 실제 화면 결과는 해당 버전의 [개발 노트](development-log.md)에 기록합니다.

내레이션과 전용 음원 제작 도구는 0.27.0에서 제거했습니다. 이전 음원 재현이 필요하면 현재 작업 트리의 명령 대신 [v0.26.1 제작 문서](https://github.com/sanu1227/MoneyTown/blob/v0.26.1/docs/opening-audio.md)와 해당 태그의 파일을 사용합니다. 배경음악·일반 효과음 제작 절차는 아래 설명과 [효과음 자료](audio-assets.md)를 따릅니다.

## 배경음악 수정

v0.25.0은 `unity/Assets/Resources/Audio/`의 `town-lounge.wav`, `market-morning.wav`, `newsroom-bossa.wav`, `city-evening.wav`를 PCM 스트리밍으로 불러옵니다. `GameMusic.cs`의 독립 2D `AudioSource`에서 한 곡씩 재생하고, 네 곡을 한 번씩 들은 뒤 다시 섞습니다. 한 바퀴의 마지막 곡과 다음 바퀴 첫 곡도 다르게 고릅니다. 효과음이 재생되거나 메뉴가 바뀌어도 음악을 다시 시작하지 않습니다.

`FeedbackScreen.cs`의 **설정 > 배경음악**은 현재 곡·다음 곡·켜기/끄기와 음량 0·25·50·75·100%(기본 켜짐·25%)를 제공합니다. 기기 설정 키는 `MoneyTown.Music.Enabled`, `MoneyTown.Music.Volume`입니다. 효과음·동작 줄이기와 독립적이며 게임 저장 형식은 바꾸지 않습니다. 꺼짐·0음량·앱 중단 때 `Pause`, 복귀 때 같은 위치에서 `UnPause`와 0.5초 페이드인을 사용합니다. 곡 종료 전에는 0.5초 페이드아웃을 적용합니다. `NextTrack()`과 `SelectTrack(int)`는 무음·중단 상태를 유지하며 선곡만 바꿉니다. 직접 선택한 곡이 끝나면 랜덤 순서로 복귀합니다. `MusicSelectionCard`는 네 곡의 버튼과 현재 선택 표시를 제공하며 현재 곡을 다시 눌러도 재생 위치를 유지합니다.

```powershell
python scripts/generate-bgm.py
python scripts/generate-bgm.py --check
```

생성 코드와 음악 원본을 함께 관리합니다. [음악 구성·파일 사양·루프 검사](music-assets.md)를 참고하세요. 파형 검증 외에 Unity Player에서 셔플 경계와 자동 곡 전환, 효과음과 동시 재생, 음소거·음량 변경, 메뉴 전환, 배경 복귀와 설정 재적용을 확인합니다. 새 버전의 실행·설치 결과는 검증 완료 후 개발 노트에 기록합니다.

## 효과음과 애니메이션 수정

v0.21.0의 효과음은 `unity/Assets/Resources/Audio/`에 있으며 `GameFeedback.cs`가 재생과 버튼·금액·지면의 움직임을 담당합니다. `FeedbackScreen.cs`는 화면 이벤트를 연결하고 **설정 > 소리와 애니메이션**을 제공합니다. 음량은 0·25·50·75·100%(기본 50%)이며 효과음과 동작 줄이기를 따로 켜고 끕니다. 설정은 `MoneyTown.Feedback.Sound`, `MoneyTown.Feedback.Volume`, `MoneyTown.Feedback.ReducedMotion` 기기 설정으로 저장합니다.

```powershell
python scripts/generate-sfx.py
python scripts/generate-sfx.py --check
```

표준 라이브러리만 사용합니다. 검사 명령은 파일을 수정하지 않고 8개 WAV·메타·미리듣기의 재현 여부와 PCM 포맷·샘플 수·볼륨·끝단을 확인합니다. [원본 효과음 사양과 미리듣기](audio-assets.md)

파형 검사와 게임 내 재생 검증은 구분합니다. Unity에서 각 이벤트·무음·음량·설정 복원·동작 줄이기·백그라운드 복귀를 확인합니다. 동작 줄이기에서도 알바는 정지 장면으로 약 4.2초를 진행하고 동일한 하루·보상을 적용해야 합니다. 새 버전의 빌드와 실제 Player 검사 결과는 검증이 끝난 뒤 개발 노트에 기록합니다.

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
- 이전 파일을 먼저 검증하고 `before-price-update` 원본 사본을 남긴 뒤 원자적 저장이 성공한 경우에만 조정 상태를 적용합니다. v0.20.1에서는 형식 11로 적용 상태를 저장해 재실행 시 중복 정산을 막았습니다. 현재는 같은 이전 내역을 형식 12에 보존합니다. 정산 금액·실현손익의 한도를 넘거나 저장이 실패하면 원본을 보존하며, 자산이나 손익을 잘라 한도에 맞추지 않습니다. 검증에서는 총자산 보존·정산 원가·중복 방지·원본 사본·저장 실패 경로를 함께 확인합니다.
- v0.25.0의 현재 저장 형식 **12**는 기존 형식 11 본문 뒤에 이야기 해금 수·확인 수를 추가합니다. 형식 1~11은 도입부 미확인으로 읽고 화면 시작·복구 시 현재 총자산으로 해금합니다. 형식 11은 가격을 다시 환산하지 않으며, 1~10의 기존 가격 이전 조건도 유지합니다. 해금·확인은 저장 성공 후에만 화면 상태에 반영합니다.
- Android·Unity 에디터·PC의 저장은 별개일 수 있습니다. 이전 Kotlin·웹 저장은 자동 이전하지 않습니다.
- 소스에는 Unity `Assets`·`.meta`·`Packages`·`ProjectSettings`와 콘텐츠·검증·문서를 포함합니다. 캐시·로그·개인 설정·서명 키·빌드 결과는 제외합니다.
- 기능별 검증과 개발 노트를 마친 뒤 한국어 메시지로 커밋하고 `origin`에 일반 push합니다. 자세한 규칙은 [AGENTS.md](../AGENTS.md)를 따릅니다.

## 이전 구현

`android/`는 Kotlin 앱, `prototype/`은 웹 프로토타입 보관용입니다. 이전 거래 규칙 검사는 `node --test tests/market.test.cjs`로 실행합니다. 현재 게임 기능은 Unity에서 개발합니다. 과거 환경과 이식 과정은 [개발 노트](development-log.md)에 남아 있습니다.
