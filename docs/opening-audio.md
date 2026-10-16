# 도입부 내레이션과 효과음

첫 화면의 문장 **“이건 내가 세계 최고의 부자가 되는 이야기이다”**를 읽는 무료 로컬 음원입니다. Windows에 설치된 `Microsoft Heami Desktop` 한국어 여성 음성과 Python 표준 라이브러리로 합성한 효과음을 사용합니다. 유료 API, 외부 음성 서비스, 음성 복제, 외부 효과음 샘플은 사용하지 않았습니다.

## 파일

- [게임용 완성 WAV](../unity/Assets/Resources/Audio/story-opening.wav): 모노 44,100Hz·16bit PCM, **5.731383초**, 252,754 프레임.
- [원본 내레이션 WAV](../assets/audio/opening-narration-source.wav): 모노 16,000Hz·16bit PCM, **4.931375초**, 78,902 프레임.
- [제작 메타 JSON](../assets/audio/opening-narration-source.json): 원문, 음성, 정확한 SSML, 엔진 bookmark, 최종 타이밍, 레벨과 SHA-256.
- [생성기](../scripts/generate-opening-audio.py): Windows SAPI 녹음과 결정적 믹싱, 읽기 전용 `--check`.

Windows 기본 음성의 음질과 발음을 사용하며, 전문 성우 녹음이나 유료 신경망 음성으로 제작한 파일은 아닙니다. 다른 PC에서 원본을 다시 녹음하려면 해당 한국어 음성이 설치되어 있어야 합니다. 게임 실행에는 음성 설치나 네트워크 연결이 필요하지 않습니다. 배포하는 WAV를 그대로 재생합니다.

## 음성 원문과 SSML

원문은 `이건 내가 세계 최고의 부자가 되는 이야기이다`입니다. `SpeechSynthesizer.Rate = -1`, `Volume = 100`으로 문장 전체를 한 번에 합성했습니다. 문장을 세 개의 별도 파일로 잘라 붙이지 않았습니다.

```xml
<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="ko-KR"><mark name="line-1"/>이건 내가<break time="180ms"/><mark name="line-2"/>세계 최고의 부자가<break time="140ms"/><mark name="line-3"/>되는 이야기이다</speak>
```

System.Speech의 `BookmarkReached.AudioPosition`을 녹음할 때 수집했습니다. 이 환경에서 Heami를 44.1kHz로 직접 출력하면 bookmark 시간이 음원 길이를 넘어서는 현상이 확인되어, 원본은 엔진 기본 16kHz로 녹음했습니다. 최종 믹싱에서 선형 보간으로 44.1kHz로 변환하며 원래 발화 길이와 문구 간격을 유지합니다. 변환이 원본 음성의 주파수 정보를 추가하는 것은 아닙니다.

## 화면 동기화

아래 시점은 **게임용 WAV 재생 시작 기준**입니다. 문구 시작은 엔진 bookmark에 선행 무음 0.2초를 더한 값이며 음소별 강제 정렬 결과는 아닙니다.

| 시점 | 문구 또는 효과음 | 길이 |
| ---: | --- | ---: |
| 0.200000초 | 이건 내가 | 다음 문구까지 |
| 1.110000초 | 세계 최고의 부자가 | 다음 문구까지 |
| 2.880375초 | 되는 이야기이다 | 문장 끝까지 |
| 1.110000초 | 부드러운 저음 울림 | 1.35초 |
| 1.170000초 | 금빛 차임 1 · 784Hz | 1.15초 |
| 1.280000초 | 금빛 차임 2 · 987.77Hz | 1.15초 |
| 1.390000초 | 금빛 차임 3 · 1174.66Hz | 1.15초 |
| 3.000375초 | 도시 공개 상승 스웰 | 1.65초 |

원본 발화가 끝난 뒤 0.6초의 여유가 있습니다. 음성에는 5ms 가장자리 페이드와 일정한 게인만 적용하고, 효과음에는 짧은 어택·릴리스를 적용했습니다. 울림은 98Hz와 147Hz 사인파, 차임은 감쇠하는 배음, 스웰은 저역 통과 노이즈와 얇은 상승음을 직접 합성합니다. 노이즈 난수 seed는 `20261016`입니다. 효과음은 목소리 아래에 작게 배치했습니다.

## 재현과 검증

```powershell
python .\scripts\generate-opening-audio.py --check
```

이 명령은 TTS를 다시 호출하거나 파일을 수정하지 않습니다. 저장된 원본 음성에서 최종 WAV를 다시 합성하여 바이트 일치, 원본·완성본 해시, 3개 bookmark 순서·범위, PCM 형식, 메타, 레벨과 시작·종료 경계를 확인합니다. 완성본의 부동소수점 믹스 측정값은 **Peak 0.690605, RMS 0.115603**이며 Peak 제한 0.85를 충족합니다.

일반 실행은 보관된 원본으로 WAV와 제작 메타를 다시 만듭니다. 원본을 처음 녹음하거나 명시적으로 교체할 때만 Windows 음성 엔진을 사용합니다.

```powershell
python .\scripts\generate-opening-audio.py
python .\scripts\generate-opening-audio.py --record-source
```

`--record-source`는 원본 녹음과 관련 메타를 교체합니다. Windows 음성 엔진 버전에 따라 새 녹음의 바이트나 시간이 달라질 수 있으므로 다시 검증하고 화면의 타이밍도 확인해야 합니다. 제한된 실행 환경에서 설치된 음성이 보이지 않으면 Windows 호스트 실행 환경이 필요합니다.

Unity 메타는 짧은 내레이션에 맞춰 Decompress On Load·PCM·Preload, 정규화 끄기, 2D로 설정했습니다. 실제 스피커·이어폰 청취와 모바일 오디오 중단 동작은 별도의 Player·실기기 검증 대상입니다.

| 파일 | SHA-256 |
| --- | --- |
| 원본 내레이션 | `da67d27d960bc7d33f2885f1d59fceb03235bee0d8f2a200cf4a5dbac305832f` |
| 게임용 완성본 | `8003ed288d794df07475f5a2399d3a0f5908721056ab52b5ab15388d2deb3960` |
