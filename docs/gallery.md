# 실제 게임 화면

[첫 화면](../README.md) · [플레이 가이드](player-guide.md) · [자료실](README.md)

**2026-10-16 촬영.** 소리 설정은 v0.21.0, 가격 조정 내역은 v0.20.1, 기본 화면은 v0.20.0입니다. 각 항목을 펼치면 480×960 원본을 볼 수 있습니다. Windows 검증용 Unity Player에서 테스트 데이터로 실행한 화면이며 이미지 보정이나 UI 합성 없이 제공합니다.

<details open>
<summary><strong>v0.21.0 소리와 애니메이션 설정</strong></summary>

상단 설정에서 아래로 스크롤하면 효과음 켜기/끄기, 다섯 단계 음량, 동작 줄이기와 미리듣기를 사용할 수 있습니다. [원본 효과음 모음](../assets/audio/preview.wav)

<img src="../assets/screenshots/v0.21.0/feedback-settings.png" width="480" alt="효과음 켜짐, 음량 50%, 동작 줄이기 꺼짐과 미리듣기 버튼" />

원본은 `build/ui-verification-98d9b91da0c045e0a59b261dcbe27479/28-feedback-settings.png`입니다. UI 178개 검사를 통과한 실제 Player 캡처이며 공개 이미지의 SHA-256이 원본과 같습니다.

</details>

<details open>
<summary><strong>v0.20.1 가격 조정 — 기존 진행의 총자산 보존</strong></summary>

내 자산의 진행 관리에서 조정 당시 총자산과 정수 주식으로 바꾸고 남은 현금 정산액을 확인합니다. 실제 사용자 저장이 아닌 검증용 보유 주식 데이터입니다.

<img src="../assets/screenshots/v0.20.1/price-migration.png" width="480" alt="가격 조정 후 보존 총자산과 현금 환산액을 표시하는 진행 관리" />

원본은 `build/ui-verification-91f25d9eebd242b49f1f439251e88d6b/27-price-migration.png`입니다. 해당 실행은 UI 133개 검사를 통과했고 공개 이미지의 SHA-256이 원본과 같습니다.

</details>

<details open>
<summary><strong>거래소 — 차트와 주문</strong></summary>

최근 주가·다음 갱신까지 남은 시간·내 보유 주식·주문 영역을 한 화면에서 봅니다.

<img src="../assets/screenshots/v0.20.0/exchange.png" width="480" alt="거래소 전체 화면" />

</details>

<details>
<summary><strong>정보 가판대 — 봉인 정보를 고르는 순간</strong></summary>

기업과 사건을 알기 전에 등급·신뢰도·가격·정보의 특징으로 판단합니다.

<img src="../assets/screenshots/v0.20.0/information.png" width="480" alt="봉인 정보 카드 목록" />

</details>

<details>
<summary><strong>내 자산 — 포트폴리오와 다음 투자</strong></summary>

거래·결산 기록, 물류센터 알바, 정기 예금을 확인합니다.

<img src="../assets/screenshots/v0.20.0/portfolio.png" width="480" alt="내 자산 전체 화면" />

</details>

<details>
<summary><strong>기업 분석 — 구매한 기사 읽기</strong></summary>

매체를 닮은 지면에서 요약·현장 기록·사업 현황·검증 자료를 읽습니다. 아래는 스크롤 기사 상단입니다.

<img src="../assets/screenshots/v0.20.0/company-report.png" width="480" alt="기업 지정 분석서 상단" />

</details>

<details>
<summary><strong>다음 날 브리핑 — 원인과 주가 결과</strong></summary>

결산과 새날 개장 상황, 주요 변동 원인을 전체 화면 창으로 보여 줍니다.

<img src="../assets/screenshots/v0.20.0/daily-briefing.png" width="480" alt="다음 날 결산 및 개장 뉴스" />

</details>

<details>
<summary><strong>물류센터 — 하루의 노동으로 마련하는 자금</strong></summary>

상자를 옮기는 애니메이션의 한 장면입니다. 작업 후 다음 날 결산으로 넘어갑니다.

<img src="../assets/screenshots/v0.20.0/warehouse.png" width="480" alt="물류센터 알바 애니메이션 장면" />

</details>

<details>
<summary><strong>+10주 누적 — 수수료 포함 금액 확인</strong></summary>

기본 1주에서 ‘+10주’를 두 번 눌러 21주를 선택했습니다. 이 상태에서 매수를 누르면 21주를 구매합니다.

<img src="../assets/screenshots/v0.20.0/ten-shares.png" width="480" alt="+10주 두 번 선택 후 21주, 210210원의 매수 견적" />

</details>

<details open>
<summary><strong>가격대별 종목 — 저가 종목에서 고가 종목으로</strong></summary>

현재가가 낮은 순서로 기업을 비교합니다. 새 게임의 초기 100만원으로 저가·중가 6기업에 투자할 수 있습니다.

<img src="../assets/screenshots/v0.20.0/price-tiers.png" width="480" alt="1천원부터 30만원까지 가격순으로 정렬된 종목 카드" />

자금이 부족한 고가 종목은 수수료를 포함해 더 필요한 현금을 표시합니다.

<img src="../assets/screenshots/v0.20.0/expensive-stock.png" width="480" alt="600만원 노바테크와 매수에 더 필요한 5006000원 안내" />

</details>

## v0.20.0 기본 화면 촬영 정보

| 항목 | 값 |
| --- | --- |
| 버전 | 0.20.0 |
| 플랫폼 | Windows / 격리된 Unity 검증 Player |
| 해상도 | 480×960 PNG |
| 검사 결과 | 이번 촬영 실행의 UI 129개 검사 통과 |
| 저장 | 새 임시 데이터, 본게임 저장·설정 미사용 |
| 편집 | 없음. `Development Build` 표시를 포함한 원본 |
| 배너 | `assets/branding/banner.svg`는 별도 제작한 벡터 일러스트 |

원본 실행 결과는 로컬 `build/ui-verification-4a69cbd08c224dc18c98717699ffdf4f/`에 있습니다. README용 파일은 원본과 SHA-256이 같은지 확인해 복사했습니다. 캡처를 다시 만드는 명령은 [개발 가이드](development-guide.md#검사와-실제-화면-캡처)에 있습니다.

| 공개 이미지 | 원본 캡처 이름 |
| --- | --- |
| `exchange.png` | `03-intraday.png` |
| `information.png` | `23-clean-information.png` |
| `portfolio.png` | `22-clean-assets.png` |
| `company-report.png` | `10-new-company-report.png` |
| `daily-briefing.png` | `12-opening-news.png` |
| `warehouse.png` | `19-warehouse-shift.png` |
| `ten-shares.png` | `24-ten-shares.png` |
| `price-tiers.png` | `25-price-tiers.png` |
| `expensive-stock.png` | `26-expensive-stock.png` |

Android 실기기 실행은 아직 검증하지 않았습니다. 이 갤러리는 Android 기기에서 촬영한 것으로 표시하지 않습니다.
