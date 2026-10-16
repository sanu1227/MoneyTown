# 스토리 일러스트 제작 기록

작성일: 2026-10-16 / 적용 버전: 0.25.0

## 제작 방식

MoneyTown의 도입부와 총자산 이정표에 사용하는 신규 일러스트 6장입니다. Codex 내장 `image_gen.imagegen`으로 각각 별도 생성했습니다. 외부 게임의 자산이나 사용자 참고 이미지를 제작 입력으로 사용하지 않았습니다. CLI/API 대체 경로는 사용하지 않았습니다.

그림은 짧은 검은 머리의 익명 청년, 남색 재킷, 청록색 그림자와 금빛 도시라는 공통 시각 요소를 유지합니다. 주인공은 뒷모습 또는 뒤쪽 사선 구도로 표현하며, 대사창과 겹치는 하단은 어둡고 단순하게 구성했습니다. 모든 최종 PNG를 직접 확인했습니다. 책상 장면의 모니터 장식과 도시 장면의 간판은 같은 내장 도구로 제거했습니다.

최종 출력은 아래 프로젝트 경로로 원본 바이트 그대로 복사했습니다. 그림 속 문장 대신 Unity가 한국어 대사를 별도로 표시합니다.

## 최종 에셋

기준 경로: `unity/Assets/Resources/StoryArt/`. 모든 파일은 **1024×1536 PNG**, 세로 **2:3**입니다.

| 파일 | 장면 | 크기(bytes) | SHA-256 |
| --- | --- | ---: | --- |
| [00-beginning.png](../unity/Assets/Resources/StoryArt/00-beginning.png) | 프롤로그 · 작은 방의 큰 꿈 | 2010713 | `28cac70b88e93b2552638661fe5eddf74f11c504eb29dd0da395d3743ff509ae` |
| [01-first-step.png](../unity/Assets/Resources/StoryArt/01-first-step.png) | 200만원 · 첫 번째 이정표 | 2339129 | `12718196d4a09a696c5939900be5db45d7b79738a0b36b847fa23fa1d1e69d3d` |
| [02-own-desk.png](../unity/Assets/Resources/StoryArt/02-own-desk.png) | 1천만원 · 나만의 책상 | 1910512 | `59c9cd6e72b47f02bf765fc590e44578cedb5b181ef542c3a7cc1b9e162de3e3` |
| [03-city-view.png](../unity/Assets/Resources/StoryArt/03-city-view.png) | 1억원 · 넓어진 시야 | 2396898 | `fa3e13762d182c2ff385fe33d1cb625beeab6f81ab97e51d6f473fe64c4b6e75` |
| [04-boardroom.png](../unity/Assets/Resources/StoryArt/04-boardroom.png) | 10억원 · 함께 짊어질 책임 | 1977292 | `e8e5d9bc65b886abd4b93de84acd5106c2348bbaf0843ad0a80612886a0d1a61` |
| [05-world-horizon.png](../unity/Assets/Resources/StoryArt/05-world-horizon.png) | 100억원 · 세계를 향한 다음 장 | 2330130 | `aa247487af49df91a319a4973128f73c39b54ccf65a40950884ed6d2078d8cfd` |

Unity 임포트 설정은 Texture2D, sRGB, 최대 크기 2048, 밉맵 비활성, Read/Write 비활성, Bilinear 필터, Clamp 래핑입니다. 각 PNG와 폴더의 `.meta`를 함께 관리합니다. 실제 화면 크기별 대사 가독성과 자르기 검증은 Player 검증 결과를 따릅니다.

## 생성 프롬프트

각 생성 호출의 최종 프롬프트는 아래 공통 문구에 해당 장면 문구를 줄바꿈으로 이어 붙인 것입니다. 생성 호출에는 `transparent_background: false`를 사용했으며, 신규 그림의 참조 이미지는 지정하지 않았습니다.

### 공통 문구

```text
Use case: illustration-story.
Asset type: finished portrait cinematic story illustration for MoneyTown, a cozy Korean mobile investment game, vertical 2:3, 1024 x 1536 pixels.
Style/medium: premium hand-painted semi-real animated-film key art, refined painterly textures, beautiful environmental storytelling, convincing perspective, rich depth, restrained cinematic light. A sophisticated finished illustration, not a sketch, not a 3D render.
Recurring protagonist: unnamed young Korean adult, short slightly tousled black hair, slim ordinary build, seen only from behind or three-quarter rear, modest dark navy jacket and pale shirt; believable hands and anatomy.
Color palette: deep midnight navy and muted teal shadows, warm amber gold light, harmonious natural accents.
Composition: main narrative focal point and protagonist's head in the central upper two-thirds; preserve important visual context in center so portrait mobile cropping works. Lower quarter softly dark, simple, quiet and low contrast to support a separate game dialogue overlay. Full bleed artwork, no frame.
Constraints: absolutely no words, letters, numbers, text, signage, logos, UI, captions, watermark. No split panels, no collage, no contact sheet. Create exactly one complete illustration.
```

### 00-beginning.png

```text
Scene: A modest Korean rooftop studio apartment at dawn, viewed from inside. In the upper middle, the young protagonist stands near a large open window, looking toward an immense distant city skyline just catching sunrise. An inexpensive desk with a closed notebook, a simple laptop showing only soft abstract light, and a small mug suggests humble beginnings. Weathered concrete rooftops, a few plants, warm bedding and quiet household objects. Grand dream within a very small room, calm determination and possibility. Golden morning rays enter through the window, dust motes and gently lifted curtain, deep teal room shadows. Keep lower foreground largely the simple dark desk edge and floor.
```

### 01-first-step.png

```text
Scene: On the rooftop of a modest Korean apartment building just after sunrise. The young protagonist stands three-quarter back at a low parapet, holding a small closed notebook safely against the body, looking over layered neighborhood rooftops toward a sunlit business district. A few resilient potted plants and a simple rooftop bench. A small personal victory, hopeful and grounded, not wealthy yet. Amber sunlight catches the dark hair and shoulders, soft morning haze separates the city layers. Emphasize beautiful vast sky and city in the upper two-thirds. Bottom quarter a quiet dark terrace foreground.
```

### 02-own-desk.png

```text
Scene: A compact newly arranged workroom in a Korean city, warmly lit in the late afternoon. The young protagonist is seated at a modest elegant wooden desk, seen from three-quarter back, studying an open unmarked notebook beside a small single monitor. Monitor contains only abstract softly glowing curves with no characters, digits, labels or readable interface. A leafy plant, a desk lamp, one shelf with blank-spined books and a city window create a personal place to focus. Thoughtful, earned comfort and patient learning. Rich warm wood and amber desk light against deep navy teal corners. Protagonist and desk in middle upper composition; lower quarter quiet dark floor and chair silhouette.
```

### 03-city-view.png

```text
Scene: After rain at blue hour, the young protagonist stands alone on an elegant pedestrian bridge overlooking a brilliant Korean business district. Three-quarter back portrait, dark navy casual jacket, a small closed notebook in one hand. Wet paving reflects soft amber city light, sleek glass towers glow through blue haze, a river cuts through the city, no traffic signs. The scene feels spacious, reflective and calm, a hard-earned wider perspective after growth. Fine droplets on a rail, clouds breaking above, quiet rather than luxury excess. Protagonist's head and city horizon in the upper two-thirds, lower quarter a simple shadowed bridge foreground.
```

### 04-boardroom.png

```text
Scene: A refined but modest glass meeting room high above the city at sunset. The young protagonist, recognizable short black hair and dark navy jacket, seen three-quarter back at a warm wooden meeting table, reviewing blank paper plans with exactly two adult colleagues, small distant human figures across the table. One colleague seated, one quietly pointing toward a simple physical unmarked model on the tabletop. Kind collaborative body language. Room modest in scale, one plant, no lavish opulence. Thoughtful responsibility and building something together, amber sunset through glass walls against dark navy teal shadows. Faces secondary, protagonist and table in upper middle. Lower quarter subdued quiet chair and floor foreground. No logos, no numbers, no chart labels, no text on papers or elsewhere.
```

### 05-world-horizon.png

```text
Scene: A high rooftop terrace overlooking a vast beautiful coastal global metropolis at twilight. The same unnamed young Korean adult with short tousled black hair stands confidently with both hands gently resting at a simple dark railing, back toward viewer, dark navy jacket and pale shirt. Golden light on the far horizon meets a deep teal ocean beyond layers of luminous towers and a sweeping harbor. A few terrace plants, subtle moving clouds, an immense sky. This is an open future and ambition to become the world's richest person, not a boast of already having achieved it. Emotion is quiet courage, responsibility, wonder and possibility. Painterly city depth and magical but natural gold reflections. Protagonist's head upper middle, lower quarter quiet dark terrace floor. No landmarks requiring exact real geography, no boats with labels, no text anywhere.
```

## 최종 정리 편집

아래 두 장은 각 최초 출력 PNG를 `view_image`로 확인한 뒤, 해당 파일을 `referenced_image_paths`에 지정하여 편집했습니다. 최종 에셋은 이 편집 결과이며 최초 변형을 배포 폴더에 함께 넣지 않았습니다. 모두 `transparent_background: false`입니다.

### 02-own-desk.png

```text
Use case: precise-object-edit. Edit this existing MoneyTown illustration only to remove every brand-like mark and glyph: remove the small gold infinity-like emblem from the center of the monitor's bottom bezel, making the bezel uniformly plain black; make all book spines fully unmarked. Preserve the entire composition, protagonist, room, golden lighting, painterly finish, original 1024 x 1536 portrait resolution and all other details. The image must contain no words, letters, numbers, logo, brand emblem or watermark. This is not a new scene; retain the original image everywhere except the tiny marks.
```

### 03-city-view.png

```text
Use case: precise-object-edit. Edit this existing MoneyTown illustration only to remove all signage or text on the distant buildings. In particular remove the small blue illuminated sign with white letter-like marks near the top of the tower to the right of the protagonist's head; replace that sign with ordinary blue glass and warm window reflections matching the building. Remove any other tiny glyphs or branding from rooftops. Preserve the entire composition, exact protagonist, bridge, city, sky, wet reflections, painterly finish and 1024 x 1536 portrait resolution. No words, letters, numbers, logos, watermark or signs anywhere. Keep all details outside the tiny signage regions unchanged.
```
