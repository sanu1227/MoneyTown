# MoneyTown 효과음

외부 음원·샘플·추가 패키지를 사용하지 않고 Python 표준 라이브러리의 사인파, 고정 시드 잡음과 볼륨 곡선으로 만든 원본 효과음입니다. 생성 코드는 [generate-sfx.py](../scripts/generate-sfx.py)에 보관합니다.

모든 클립은 **44,100Hz · 모노 · 16bit PCM WAV**입니다. 시작과 끝 샘플은 0이며 각 소리에 짧은 페이드 인·아웃을 적용합니다. 볼륨을 갑자기 끊지 않고, 오류도 낮은 두 음으로 안내합니다. Unity 메타는 고정 GUID, PCM, 2D, 미리 로드 설정을 사용하며 자동 정규화를 끕니다.

| 파일 | 용도와 소리 | 길이 | Peak | RMS |
| --- | --- | ---: | ---: | ---: |
| [tap.wav](../unity/Assets/Resources/Audio/tap.wav) | 버튼: 가벼운 짧은 클릭 | 0.120초 | 0.4200 | 0.0771 |
| [buy.wav](../unity/Assets/Resources/Audio/buy.wav) | 매수 체결: 올라가는 두 음 | 0.320초 | 0.5000 | 0.1398 |
| [sell.wav](../unity/Assets/Resources/Audio/sell.wav) | 매도 정산: 따뜻하게 내려오는 세 음 | 0.430초 | 0.4800 | 0.1489 |
| [paper.wav](../unity/Assets/Resources/Audio/paper.wav) | 정보 구매: 짧은 바스락 소리와 봉인 해제 | 0.420초 | 0.4000 | 0.0694 |
| [settlement.wav](../unity/Assets/Resources/Audio/settlement.wav) | 개장 브리핑: 차분하게 겹치는 차임 | 0.640초 | 0.5000 | 0.1273 |
| [work.wav](../unity/Assets/Resources/Audio/work.wav) | 물류센터: 상자를 놓는 낮은 나무 타격음 | 0.220초 | 0.4600 | 0.0758 |
| [success.wav](../unity/Assets/Resources/Audio/success.wav) | 예금·보상: 짧게 올라가는 세 음 | 0.520초 | 0.5200 | 0.1492 |
| [error.wav](../unity/Assets/Resources/Audio/error.wav) | 실행 불가 안내: 부드러운 낮은 두 음 | 0.380초 | 0.4200 | 0.1416 |

Peak와 RMS는 정수 PCM을 32,768로 나눈 값입니다. 원본 파일 기준이며 게임 내 볼륨에 따라 최종 출력 크기가 달라집니다. 게임은 단일 AudioSource에서 최신 효과음만 재생합니다. 동작별 음높이·리듬·질감을 구분했습니다.

## 미리듣기

[8개 효과음 모음 듣기](../assets/audio/preview.wav) · **5.500초**, Peak 0.5200, RMS 0.0947.

재생 순서는 `tap → buy → sell → paper → settlement → work → success → error`이며 소리 사이에 0.35초 무음을 넣었습니다. 이 모음은 게임 외 청취 자료로만 사용합니다. 각 원본은 Unity에서 `Resources.Load<AudioClip>("Audio/buy")`처럼 확장자 없이 불러옵니다.

## 재생성과 검사

저장소 루트에서 실행합니다. 산출 경로는 실행 위치 대신 스크립트 위치를 기준으로 정합니다.

```powershell
# WAV 8개, Unity 메타, 미리듣기 모음 생성
python scripts/generate-sfx.py

# 파일을 바꾸지 않고 재생성 결과와 검사
python scripts/generate-sfx.py --check
```

`--check`는 각 생성 파일의 SHA-256을 메모리에서 다시 만든 결과와 비교합니다. WAV의 포맷·정확한 샘플 수·끝단 0을 검사하고, 클립 길이 0.1~0.8초, 모음 길이 4~8초, Peak 0.65 이하, RMS 0.02~0.22 범위를 확인합니다. CSV로 샘플 수·길이·Peak·RMS·SHA-256을 출력하며 누락이나 불일치가 있으면 실패합니다. 같은 생성기와 실행 환경에서 재현할 수 있습니다.

2026-10-16 생성 및 `--check`의 자동 검사를 통과했습니다. 위 수치는 원본 파형 측정값입니다. v0.21.0 Windows Player의 480×960·360×800에서 각각 UI 178개 검사를 통과했습니다. 8개 AudioClip의 로드·샘플과 AudioSource 재생, 이벤트 연결, 무음·음량·중단, 설정 재적용과 모션 복원을 검사했습니다. 설정 검사는 격리된 메모리 저장소를 사용합니다. 실제 휴대폰 스피커·이어폰에서의 청취 밸런스와 Android 실행은 미검증입니다.
