# MoneyTown 참고 게임과 개발 자료 조사

조사일: 2026-10-02

주식 판단의 재미와 자산 성장 구조를 검토하기 위해 공식 게임 소개와 개발 문서를 조사했습니다. 아래 기능은 소개 페이지에서 확인한 내용이며 직접 플레이한 평가가 아닙니다. 적용 아이디어는 MoneyTown을 위한 제안입니다. 가격, 평점, 다운로드 수처럼 변하는 수치는 비교 기준으로 사용하지 않았습니다.

## 참고 게임

### STONKS-9800 Stock Market Simulator

[개발사 제공 Steam 소개](https://store.steampowered.com/app/1539140/STONKS9800_Stock_Market_Simulator/?l=english)

확인: 일본의 1980년대 주식시장 분위기를 다루는 텍스트 기반 게임입니다. 주식 거래와 배당, 건강 관리, 부동산과 기업 운영을 소개합니다.

적용 제안: 총자산 성장과 생활 변화를 연결해 투자 성공을 체감하게 합니다. 초기에는 건강이나 생활 관리까지 확장하지 않고 거래의 재미부터 검증합니다.

### Business Empire RichMan

[개발사 제공 Google Play 소개](https://play.google.com/store/apps/details?id=com.ttterbagames.businesssimulator&hl=en)

확인: 사업을 열고 성장시키며 가상 주식, 부동산과 암호화폐 등에 투자하는 게임입니다. 부동산의 수동적 수익과 고급 자산 구매도 소개합니다.

적용 제안: 주식 수익을 사업과 부동산으로 옮기는 장기 성장 동선을 참고합니다. 우리 게임은 뉴스 해석과 비용이 있는 정보 구매를 핵심 차이로 둡니다. 비슷한 이름의 다른 Business Empire 게임과 혼동하지 않도록 위 앱 ID를 기준으로 조사했습니다.

### The Invisible Hand

[배급사 공식 소개](https://www.fellowtraveller.games/the-invisible-hand)

확인: FERIOS의 중간급 주식 중개인으로 부와 성공을 추구하는 설정을 소개합니다. 소개는 성공에 따르는 대가를 강조합니다.

적용 제안: 투자에 이야기와 선택의 결과를 붙이는 방향을 참고합니다. 정보 거래의 구체적 규칙은 이번 공식 소개만으로 확인하지 못했으므로 동일 기능이 있다고 가정하지 않습니다.

## 개발 자료

### Godot Android 내보내기

[Godot 공식 문서](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_android.html)

용도: Android 개발 후보를 평가하고 내보내기 준비 사항을 확인하는 자료입니다. 엔진 선정과 Android 패키징 단계에서 읽습니다. 현재 프로젝트의 엔진을 Godot로 확정한 것은 아닙니다.

### 브라우저 저장

[MDN localStorage 문서](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage)

확인: localStorage는 실행 출처에 연결된 브라우저 저장 공간입니다. `file:` 주소에서의 동작은 정의되지 않았으며 브라우저마다 다를 수 있습니다.

적용: 첫 프로토타입은 localStorage를 사용하되 저장 실패를 안내합니다. 저장과 재접속 검증은 동일한 로컬 HTTP 주소에서 수행합니다. 출시 버전의 저장 구조는 모바일 플랫폼에 맞춰 다시 결정합니다.

## 적용 우선순위

1. 첨부 화면의 단순한 종목·차트·거래 동선을 유지합니다.
2. 뉴스, 다음 가격 변화와 손익을 연결합니다.
3. 보고서 비용과 예금의 기회비용을 확인합니다.
4. 자산 성장과 해금 목표를 추가합니다.
5. 핵심 재미가 확인되면 사업과 부동산으로 확장합니다.

## 다음 조사와 직접 플레이 항목

- STONKS-9800: 거래 정보 밀도, 하루 진행 방식과 생활 성장 동기
- Business Empire RichMan: 투자처 진입 조건, 자산 화면의 이해도와 성장 속도
- The Invisible Hand: 공개 영상 또는 직접 플레이로 뉴스·거래·이야기의 연결 확인
- 공통 기록: 첫 거래까지 걸린 시간, 이해하기 어려운 용어, 손실 후 재도전 방식, 반복 플레이 이유

위 게임의 이미지·브랜드·문구를 그대로 가져오지 않고 기능과 조작 흐름을 분석합니다. 이번 조사에는 직접 플레이, 장기 밸런스 측정, 시장 규모나 매출 분석이 포함되지 않습니다.

## Android 전환 자료

확인일: 2026-10-09. Android를 우선 개발하며 Kotlin과 Jetpack Compose를 사용하기로 결정했습니다.

- [Compose 그래픽 공식 문서](https://developer.android.com/develop/ui/compose/graphics/draw/overview): Canvas와 DrawScope 기반 차트 그리기에 참고했습니다.
- [Compose 상태 공식 문서](https://developer.android.com/develop/ui/compose/state): 관찰 가능한 화면 상태와 재구성 방식에 참고했습니다.
- [AtomicFile 공식 문서](https://developer.android.com/reference/android/util/AtomicFile): 쓰기를 완료하거나 실패를 복구하는 앱 내부 저장 구현에 참고했습니다.

이는 기술 구현 자료이며 게임 경제 수치의 근거는 아닙니다.

## Unity 전환 자료

확인일: 2026-10-09. 후속 사용자 요청으로 Unity 6.3 LTS를 채택했습니다.

- [Unity 6 릴리스 안내](https://unity.com/releases/unity-6): LTS 버전 계열 확인.
- [Unity 릴리스 API](https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=6000.3.26f1&platform=WINDOWS&architecture=X86_64): 6000.3.26f1, revision de8167e7440c 및 공식 설치 파일 확인. 응답은 프로젝트 루트의 `unity-release-info.json`에 기록했습니다.
- [Android 개발 환경](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html): Android Build Support, SDK, NDK와 JDK 필요 사항.
- [Android 지원 도구 버전](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/sdksetup/supported-dependency-versions): NDK r27c와 OpenJDK 17 호환성 확인.
- [uGUI 문서](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html): Canvas 기반 게임 UI 구성.
- [Noto CJK 글꼴](https://github.com/notofonts/noto-cjk): 한국어 표시를 위한 Noto Sans CJK KR Regular를 포함했습니다. 라이선스는 `unity/Assets/Resources/OFL.txt`에 보관합니다.
