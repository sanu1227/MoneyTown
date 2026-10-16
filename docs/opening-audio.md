# 도입부 내레이션과 효과음

**0.26.1 / 2026-10-16.** 첫 화면의 문장 **“이건 내가 세계 최고의 부자가 되는 이야기이다”**를 무료 로컬 신경망 음성으로 제작했습니다. Windows 기본 음성을 사용했던 0.26.0에서 **Supertonic 3의 남성 프리셋 M2**로 교체했습니다. 문장의 억양과 호흡은 모델이 합성하고, 낮은 울림·차임·상승 효과음을 작은 음량으로 더합니다. 유료 API, 외부 음성 생성 서비스, 음성 복제, 외부 효과음 샘플은 사용하지 않았습니다.

모델과 도구를 처음 준비할 때 인터넷 다운로드가 필요합니다. 이후 합성은 로컬 CPU에서 실행합니다. 게임에는 완성된 WAV만 넣으므로 플레이 중 모델 설치·인터넷 연결·음성 생성 요금이 없습니다. 이 파일의 자연스러움에 대한 실제 스피커·이어폰 청취 평가는 아직 하지 않았습니다.

## 파일과 제작 설정

| 파일 | 내용 |
| --- | --- |
| [게임용 완성 WAV](../unity/Assets/Resources/Audio/story-opening.wav) | 모노 44,100Hz·16bit PCM, **5.207664초**, 229,658 프레임 |
| [원본 내레이션 WAV](../assets/audio/opening-narration-source.wav) | 모노 44,100Hz·16bit PCM, **4.407664초**, 194,378 프레임 |
| [제작 메타 JSON](../assets/audio/opening-narration-source.json) | 문장·프리셋·합성 설정·구간 시점·도구 버전·파일 해시·믹스 측정값 |
| [원본 재생성 스크립트](../scripts/record-opening-neural.py) | 내려받은 Supertonic 모델과 공식 helper로 원본을 명시적으로 교체 |
| [효과음 믹싱 스크립트](../scripts/generate-opening-audio.py) | 저장된 원본과 구간 정보로 결정적 믹싱, 읽기 전용 `--check` |

합성 입력은 `이건 내가, 세계 최고의 부자가 되는 이야기이다.`입니다. 쉼표로 짧은 호흡을 주되 화면의 문장은 유지합니다. `lang="ko"`, 프리셋 `M2`, 속도 `0.9`, denoising 12단계, 난수 seed `20261016`으로 문장 전체를 한 번에 합성했습니다. 별도 발화를 잘라 붙이거나 음높이를 낮추지 않았습니다. 원본부터 44.1kHz이며 믹싱 중 리샘플링하지 않습니다.

## 화면 동기화와 효과음

아래 시점은 **게임용 WAV 재생 시작 기준**입니다. 구간 시점은 로컬 `faster-whisper`의 `small` 모델로 확인한 단어 시작을 참고한 근삿값입니다. 음소별 강제 정렬이나 음성 엔진 bookmark가 아닙니다. 원본 기준 0·1.1·2.7초에 선행 무음 0.2초를 더했습니다.

| 시점 | 문구 또는 효과음 | 길이 |
| ---: | --- | ---: |
| 0.200000초 | 이건 내가 | 0.7초 동안 표시 |
| 1.300000초 | 세계 최고의 부자가 | 0.7초 동안 표시 |
| 2.900000초 | 되는 이야기이다 | 0.7초 동안 표시 |
| 1.300000초 | 부드러운 저음 울림 | 1.35초 |
| 1.360000초 | 금빛 차임 1 · 784Hz | 1.15초 |
| 1.470000초 | 금빛 차임 2 · 987.77Hz | 1.15초 |
| 1.580000초 | 금빛 차임 3 · 1174.66Hz | 1.15초 |
| 3.020000초 | 도시 공개 상승 스웰 | 1.65초 |

도시는 3~5.25초에 드러나며 전체 화면 연출은 5.25초입니다. 원본 뒤에 0.6초의 여유를 둡니다. 음성에는 5ms 가장자리 페이드와 일정한 게인만 적용합니다. 저음 울림은 98Hz·147Hz 사인파, 차임은 감쇠하는 배음, 스웰은 필터링한 노이즈와 상승음을 직접 합성합니다. 노이즈 seed는 `20261016`입니다. 차임의 합성 진폭을 0.018에서 0.014로 낮춰 말소리가 덜 가려지게 했습니다.

Unity 시작 화면이 끝난 뒤 한 번 재생하며, 내레이션 동안 배경음악은 현재 음량의 25%로 낮아졌다가 복원됩니다. 다음 페이지·닫기·건너뛰기는 재생을 중지하고 다시 보기는 처음부터 시작합니다. 앱 중단·복귀는 같은 위치에서 이어집니다. 효과음 켜기/끄기·음량을 공유하고 ‘동작 줄이기’에서는 목소리를 유지합니다.

## 저장된 음원의 재현과 검사

이 두 명령은 **Python 표준 라이브러리만** 사용합니다. 모델이나 음성 합성 패키지가 필요하지 않습니다.

```powershell
# 파일을 바꾸지 않고 저장된 원본과 완성본 검사
python .\scripts\generate-opening-audio.py --check

# 저장된 원본으로 완성 WAV와 믹스 메타 다시 만들기
python .\scripts\generate-opening-audio.py
```

`--check`는 원본 해시·PCM 형식·구간 문구 순서와 범위·완성본 바이트·믹스 메타·Unity 임포트 메타·레벨·시작과 종료 경계를 검사합니다. 새로운 내레이션을 자동 생성하지 않습니다. 완성본의 부동소수점 믹스 측정값은 **Peak 0.690570, RMS 0.075951**이며 Peak 제한 0.85 이내입니다.

선정한 원본에 로컬 음성 인식을 별도로 실행했을 때 문장은 `이건 내가 세계 최고의 부자가 되는 이야기이다.`로 인식됐습니다. 메타의 `reference_verification`은 해당 원본 SHA-256과 함께 보관한 선정본 검사 결과입니다. 이는 문장 누락 여부를 확인한 결과이며 억양·감정·청취 품질을 보증하지 않습니다. 재생성 스크립트와 `--check`는 음성 인식이나 주관적 청취 평가를 수행하지 않습니다. Unity 실행·설치 검증은 해당 버전의 [개발 노트](development-log.md)를 기준으로 확인합니다.

## 원본 목소리를 다시 생성할 때

현재 제작 도구는 Git에서 제외한 `build/voice-tools/`에 있습니다. 모델과 공식 `helper.py`는 게임에 포함하지 않습니다. 원본 재생성은 아래 준비가 끝난 환경에서만 별도로 실행합니다.

- Python 3.12 가상환경: `build/voice-tools/venv/`.
- 공식 모델: `supertone-oss-archive/supertonic-3`, revision `aafc6e32416a594460b32413efc49d7fe4ce6d46`, 저장 위치 `build/voice-tools/model/`.
- 공식 helper: `supertone-oss-archive/supertonic`의 `py/helper.py`, commit `1e9799e964ea4c0dad7cde993b65c3c813a7b373`, 저장 위치 `build/voice-tools/helper.py`. 같은 commit의 MIT `LICENSE`도 보관합니다.
- 사용한 합성 패키지: `numpy 2.5.4`, `onnxruntime 1.31.0`, `soundfile 0.14.0`. 파일별 SHA-256과 패키지 버전은 제작 메타에 기록했습니다.

새 환경에서는 [공식 보관소 설치 안내](https://github.com/supertone-oss-archive/supertonic#quick-start)를 참고해 위 모델 revision과 helper commit을 내려받습니다. 모델 다운로드에는 Hugging Face CLI를 사용할 수 있으며 가입이나 API 키는 필요하지 않습니다. 다운로드 명령은 아래와 같습니다.

```powershell
hf download supertone-oss-archive/supertonic-3 --revision aafc6e32416a594460b32413efc49d7fe4ce6d46 --local-dir .\build\voice-tools\model
```

원본 교체 명령은 다음과 같습니다. 첫 명령은 기존 원본 WAV와 제작 메타를 덮어씁니다. 두 번째 명령으로 효과음과 섞은 뒤 검사합니다.

```powershell
.\build\voice-tools\venv\Scripts\python.exe .\scripts\record-opening-neural.py
python .\scripts\generate-opening-audio.py
python .\scripts\generate-opening-audio.py --check
```

도구 위치가 다르면 첫 명령에 `--tool-dir '도구폴더'`를 지정합니다. 스크립트 자체는 다운로드하지 않습니다. 실행 환경에 따라 신경망 합성 결과의 바이트나 발화 시점은 달라질 수 있습니다. 원본을 바꾸면 문장을 다시 확인하고, JSON의 `cues`와 `StoryScreen.cs`의 문구 시점·전체 연출 길이를 맞춰야 합니다. 원본 재합성의 바이트 동일성과 저장된 원본에서 효과음을 다시 섞는 결정적 재현은 구별합니다.

## 출처와 배포 조건

[Supertonic 3 공식 모델](https://huggingface.co/Supertone/supertonic-3)은 한국어와 고정 프리셋 음성을 지원합니다. 모델 라이선스는 **OpenRAIL-M**, 공식 예제 코드는 **MIT**입니다. 저장소는 2026-09-09 보관 처리됐으며, 코드와 모델을 특정 revision으로 내려받아 제작에 사용했습니다. [공식 보관소](https://github.com/supertone-oss-archive/supertonic)

[모델 라이선스](https://huggingface.co/Supertone/supertonic-3/blob/main/LICENSE) §6은 생성물에 대한 제공자의 권리 주장이 없다고 명시하며, 사용 제한은 계속 적용합니다. Attachment A(e)의 기계 생성 콘텐츠 고지를 반영해 게임 도움말에 **“내레이션: AI 생성 음성 (Supertonic 3)”**을 표시합니다. 게임과 저장소에는 생성한 WAV와 자체 제작 스크립트를 제공하며 모델 가중치·프리셋·공식 helper는 배포하지 않습니다. 모델 자체를 재배포하는 경우에는 별도의 라이선스 사본·고지·사용 제한 전달 조건을 확인해야 합니다. 특정 성우나 실존 인물의 목소리를 복제하지 않았습니다.

Unity 임포트는 Decompress On Load·PCM·Preload, 정규화 끄기, 2D입니다. 실제 스피커·이어폰의 주관적 청취 평가와 Android 실기기의 오디오 중단·복귀 검증은 별도 항목입니다. 이전 Heami 제작 과정과 0.26.0 검증 결과는 [개발 노트](development-log.md)에 보존합니다.

| 파일 | SHA-256 |
| --- | --- |
| 원본 내레이션 | `9a16422d3f1a6da170d3ff19fa2c464440a9c567da6794ba5d8196a2929daaf2` |
| 게임용 완성본 | `c2d1b287fcc10b90c1c13e83d4571875bf35ed7bf50b39b79f0b28a857d7830b` |
