<div align="center">
  <img src="assets/branding/banner.svg" alt="MoneyTown — 정보를 읽고, 당신의 판단으로 투자하세요" width="100%" />
  <p><strong>뉴스 속 단서를 해석하고, 가상 기업에 투자해 자산을 키우는 싱글 플레이 투자 게임.</strong></p>
  <p>
    <a href="#게임-화면">게임 화면</a> ·
    <a href="docs/player-guide.md">플레이 가이드</a> ·
    <a href="docs/development-guide.md">실행·빌드</a> ·
    <a href="docs/README.md">자료실</a> ·
    <a href="docs/roadmap.md">개발 계획</a>
  </p>
</div>

| 12개 가상 기업 | 3,000개 정보 상품 | 10단계 정보 등급 | 50,000원 일당 |
| :---: | :---: | :---: | :---: |
| 업종별 종목을 골라 거래 | 300개 사건 × 10등급 | 가격과 취재 깊이가 다른 단서 | 물류센터에서 다음 투자를 준비 |

## 게임 화면

<table>
  <tr><th align="center">거래소</th><th align="center">정보 가판대</th><th align="center">내 자산</th></tr>
  <tr>
    <td><img src="assets/screenshots/v0.20.0/exchange.png" alt="거래소: 주가 차트, 갱신 시간, 보유 주식과 주문 버튼" width="260" /></td>
    <td><img src="assets/screenshots/v0.20.0/information.png" alt="정보 가판대: 등급, 신뢰도와 가격을 표시한 봉인 정보" width="260" /></td>
    <td><img src="assets/screenshots/v0.20.0/portfolio.png" alt="내 자산: 포트폴리오, 물류센터 알바와 정기 예금" width="260" /></td>
  </tr>
  <tr><td align="center">차트를 보고 거래 수량 선택</td><td align="center">불확실한 단서의 가치를 판단</td><td align="center">자산을 관리하고 다음 투자를 준비</td></tr>
</table>

<table>
  <tr><th align="center">기업 분석</th><th align="center">다음 날 브리핑</th><th align="center">물류센터 알바</th></tr>
  <tr>
    <td><img src="assets/screenshots/v0.20.0/company-report.png" alt="신문 지면처럼 읽는 기업 지정 분석서" width="260" /></td>
    <td><img src="assets/screenshots/v0.20.0/daily-briefing.png" alt="뉴스 원인과 실제 주가 변동률을 함께 보여 주는 결산 창" width="260" /></td>
    <td><img src="assets/screenshots/v0.20.0/warehouse.png" alt="상자를 나르는 물류센터 작업 애니메이션의 한 장면" width="260" /></td>
  </tr>
  <tr><td align="center">근거와 조건을 읽는 투자</td><td align="center">왜 움직였는지 확인하는 시장</td><td align="center">하루를 써서 다시 마련하는 자금</td></tr>
</table>

2026-10-16, **v0.20.0 Windows 검증용 Unity Player**에서 새로 촬영한 실제 화면입니다. 테스트 데이터를 사용했으며 이미지 보정이나 UI 합성 없이 원본을 제공합니다. Android 실기기 캡처는 아닙니다. [원본 갤러리와 촬영 정보](docs/gallery.md)

## 어떻게 플레이하나요?

**단서를 읽고 → 종목을 고르고 → 거래하고 → 다음 날 결과를 확인합니다.**

‘게임박람회가 열린다’는 정보에서 게임 기업의 기회를 떠올릴 수 있습니다. 다만 정보의 신뢰도, 이미 반영된 가격, 거래 비용까지 판단해야 합니다. 더 비싼 정보는 기업명·사업 지표·조건을 구체적으로 보여 줍니다.

| 선택 | 게임에서 할 수 있는 일 |
| --- | --- |
| 주식 거래 | 12기업의 차트를 보고 매수·매도. 수량 직접 입력, **+10주**, 최대 매수, 보유 수량 선택 |
| 투자 성장 | 새 게임은 **1천~600만원**의 종목 가격. 저가·중가 6기업에서 시작해 고가 종목에 필요한 자금을 모으기 |
| 정보 구매 | 매일 무작위 봉인 정보 9건, 기업 지정 분석, 구매한 자료 보관과 후속 보도 |
| 시장 진행 | 30초부터 1시간까지 자동 갱신 간격 선택. 다음 날에는 더 큰 개장 변동과 원인 뉴스 |
| 자산 관리 | 포트폴리오, 거래·결산 기록, 3일 정기 예금, 성장 목표 |
| 다시 시작할 자금 | 현금 제한 없이 물류센터 알바. **일당 50,000원**, 하루 소모 후 결산 |
| 성장 이야기 | 도입부와 총자산 구간별 5장, 일러스트·전환 애니메이션·건너뛰기·다시 보기 |
| 첫 투자 연습 | 본게임 자산과 분리된 10단계 튜토리얼 |
| 소리와 움직임 | 원곡 배경음악 4곡 직접 선택·랜덤 재생, 효과음 8종과 첫 문장의 한국어 내레이션·오프닝 효과음. **설정**에서 음악·효과음·내레이션 음량과 동작 줄이기 선택 |

[처음 플레이하는 사람을 위한 가이드](docs/player-guide.md)에서 화면별 조작과 첫 거래를 확인하세요.

## 실행하기

**Unity 6000.3.26f1 / C# / uGUI** 기반입니다. Android가 주 개발 대상이며 Windows 설치형 테스트 버전도 빌드합니다.

1. 저장소를 내려받습니다.

   ```bash
   git clone https://github.com/sanu1227/MoneyTown.git
   ```

2. Unity Hub에서 **`MoneyTown/unity` 폴더**를 프로젝트로 추가합니다.
3. `Assets/Scenes/Main.unity`를 열고 **Play**를 누릅니다. Game 뷰는 세로 비율로 설정합니다.

| 원하는 결과 | 프로젝트 루트의 PowerShell 명령 |
| --- | --- |
| Windows 설치 EXE | `.\Build-Installer.ps1` |
| Android APK | `.\Build-Unity.ps1 -Target Android` |
| Unity 없이 코어 검사 | `dotnet run --project tests/unity-core/UnityCoreChecks.csproj` |

Unity 라이선스·플랫폼 모듈과 각 빌드 도구가 필요합니다. 설치 EXE와 APK는 Git 소스에 포함하지 않습니다. [개발 환경·빌드·설치 가이드](docs/development-guide.md)에 준비물, 결과 경로와 저장 위치를 정리했습니다.

## 개발 자료

| 문서 | 담고 있는 내용 |
| --- | --- |
| [플레이 가이드](docs/player-guide.md) | 처음 시작하기, 거래, 정보 해석, 날짜 진행, 알바 |
| [게임 기획서](docs/game-design.md) | 경제 규칙, 콘텐츠 설계, 화면 기준, 확장 방향 |
| [구조 안내](docs/architecture.md) | Unity UI·게임 규칙·저장·정보 생성 흐름 |
| [정보 자료집](docs/information-catalog/README.md) | 등급별 3,000개 정보, [CSV](docs/information-catalog.csv), [매체별 문체 샘플](docs/information-catalog/style-samples.md) |
| [효과음 자료](docs/audio-assets.md) | 원본 효과음 8종, 미리듣기, 생성 코드와 파형 검사 |
| [스토리 자료](docs/story.md) | 장별 해금 기준·재감상·기존 진행 적용 |
| [내레이션 자료](docs/opening-audio.md) | 무료 한국어 기본 음성·오프닝 효과음·미리듣기·제작 기록 |
| [배경음악 자료](docs/music-assets.md) | 원곡 4곡, 음악 구성, 생성 코드와 파형 검사 |
| [참고 자료 조사](docs/references.md) | 참고한 게임과 엔진·플랫폼 자료, 조사 당시 적용 아이디어 |
| [개발 계획](docs/roadmap.md) | 구현 완료, 검증할 부분, 향후 확장 후보 |
| [개발 노트](docs/development-log.md) | 날짜별 변경, 결정 이유, 검증과 남은 작업 |

## 현재 상태

**v0.26.1** — **“이건 내가 세계 최고의 부자가 되는 이야기이다”**의 목소리를 무료 로컬 신경망 모델 Supertonic 3의 낮은 남성 음성으로 바꿨습니다. 약 5.2초 동안 문구·목소리·도시 배경을 함께 보여 주고, 배경음악을 잠시 낮춰 목소리를 또렷하게 들려줍니다. **도움말 > 나의 이야기 > 아직 작은 시작**에서 다시 볼 수 있습니다. [미리듣기·제작 자료](docs/opening-audio.md)

효과음 8종과 짧은 화면 움직임도 유지합니다. 배경음악 카드 아래의 **소리와 애니메이션**에서 효과음 음량(기본 50%)과 동작 줄이기를 별도로 조절합니다. 동작 줄이기에서는 알바도 정지 장면으로 표시하며 작업 시간과 보상은 같습니다. [효과음 미리듣기와 원본](docs/audio-assets.md)

v0.20.1의 가격표 적용과 자산 보존도 유지합니다. 보유 평가액을 새 가격의 정수 주식과 나머지 현금으로 환산해 **총자산과 날짜를 보존**합니다. 초기 100만원으로 6기업에 투자할 수 있고 나머지는 자금을 모아 구매합니다.

기존 저장은 원본 사본을 남긴 뒤 한 번만 변환합니다. 과거 거래·결산·구매 정보는 당시 가격을 유지하고 전환 전 자료임을 표시합니다. 새 게임으로 초기화할 필요가 없습니다. [가격표와 적용 안내](docs/player-guide.md#저가-종목에서-고가-종목으로)

아래 표는 **v0.26.1**의 검증 결과입니다. [스토리 화면](docs/gallery.md) · [도입부 내레이션 듣기](unity/Assets/Resources/Audio/story-opening.wav) · [배경음악 듣기](docs/music-assets.md) · [효과음 미리듣기](assets/audio/preview.wav)

| 검증 | 확인 범위 |
| --- | --- |
| Unity 코어 **84개** | 이야기 해금·순서·저장 형식 12와 형식 1~11 호환, 기존 자산·가격 이전·거래 규칙 검사 통과 |
| 기존 수익 규칙 비교 | 0.20.0 가격표로 1,536건 가격 비교·구매 가능한 768건 왕복 거래 검사. [측정 조건과 결과](tests/investment-growth/README.md) |
| UI **274개 × 2개 화면 크기** | 480×960·360×800 실제 Windows Player 검사 통과. 내레이션 재생·일시정지·음소거·재감상·배경음악 조절과 기존 이야기·게임 UI 확인 |
| Windows 설치·업데이트 | 0.26.1 설치·재설치의 배포 파일 146개 일치, 기존 저장·설정 보존 확인 |
| Android APK | v0.26.1 ARM64 빌드 완료. **실제 Android 기기 검증은 진행 전** |

다음 단계는 Android 기기의 터치·안전 영역 확인과 장기 플레이 밸런스 점검입니다. 사업·부동산·채권 등은 [확장 후보](docs/roadmap.md)이며 아직 구현하지 않았습니다.

## 저장소 둘러보기

```text
unity/       현재 Unity 게임 · 씬 · 리소스 · 프로젝트 설정
content/     사건 원안과 매체별 정보 본문
tests/       코어 검사 · 사건 연관성 측정 · 전략 비교
scripts/     정보·효과음·배경음악 생성 · UI 검사 · 설치 검증
packaging/   Windows 설치 프로그램 설정
docs/        기획 · 플레이/개발 가이드 · 개발 노트
assets/      브랜드 배너 · 실제 게임 캡처 · 오디오 미리듣기
android/     이전 Kotlin 구현 보관
prototype/   이전 웹 프로토타입 보관
```

작업은 **변경 → 검증 → 개발 노트 → 한국어 커밋 → GitHub 업로드** 순서로 기록합니다. `main`에서 개발하며 첫 기준 태그는 [`v0.18.2`](https://github.com/sanu1227/MoneyTown/tree/v0.18.2)입니다. [개발 규칙](AGENTS.md)

---

모든 기업과 뉴스는 게임용 창작입니다. 실제 주식 거래·실시간 금융 데이터는 연결하지 않습니다. 포함된 한국어 글꼴의 라이선스는 [SIL Open Font License](unity/Assets/Resources/OFL.txt)를 확인하세요.
