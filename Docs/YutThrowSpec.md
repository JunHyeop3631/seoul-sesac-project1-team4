# 윷 던지기 명세

[명세 색인](README.md) · [턴 흐름](TurnSpec.md) · **윷 던지기** · [윷 행동](YutActionSpec.md) · [윷판](YutBoardSpec.md) · [웨이브](WaveSpec.md)

목차

- [현재 구현](#현재-구현)
  - [씬 구성](#씬-구성)
  - [버튼 입력부터 결과 전달까지](#버튼-입력부터-결과-전달까지)
- [남은 구현](#남은-구현)

- 플레이어가 던지기 버튼을 한 번 누르면 윷 네 개를 한꺼번에 던진다.
- 윷마다 배(평평한 면)와 등(둥근 면)을 각각 50% 확률로 판정한다. 네 윷의 판정은 서로 독립적이다.
- 배가 위를 향한 윷의 개수로 결과를 정한다.

| 배 개수 | 결과 |
| ---: | --- |
| 0 | 모 |
| 1 | 매 던지기마다 무작위로 선택된 백도 윷만 배이면 백도, 나머지는 도 |
| 2 | 개 |
| 3 | 걸 |
| 4 | 윷 |

- 백도는 매 던지기마다 무작위로 선택된 백도 윷만 배가 나왔을 때 적용한다. 백도 6.25%, 도 18.75%, 개 37.5%, 걸 25%, 윷·모 각각 6.25%다. 던지기마다 네 윷 중 하나를 같은 확률로 선택하고, 그 윷에만 X 표시 애니메이션과 최종 스프라이트를 적용한다. 연속해서 같은 윷이 선택될 수 있다.

던지기 가능 시점과 비용은 [턴 명세의 재화·비용](TurnSpec.md#던지기-재화와-비용), 결과 사용 중 재던지기와 상점 관련 제한은 [행동 선택과 준비 중 상점 이용](TurnSpec.md#행동-선택과-준비-중-상점-이용)을 따른다. 결과별 소환 아군과 이동 거리는 [윷 행동 명세의 아군 배치 표](YutActionSpec.md#아군-배치)에서 다룬다.

## 현재 구현

`YutThrowController`는 한 번의 던지기 전체를 관리한다. 윷 네 개를 던지고, 각 윷의 면을 모아 판정하며, 결과 이미지를 표시하고 완료를 알린다. `YutStick`은 자기 윷의 면을 뽑고 표시한다.

### 씬 구성

- [ThrowYut 씬](../Assets/Scenes/ThrowYut.unity)의 `Canvas > Throw Button`은 화면에 `윷 던지기`로 표시된다. 버튼의 `On Click`에는 `TurnController.ThrowYut()`가 연결되어 있다. 준비 단계와 미사용 결과를 확인한 뒤 `YutThrowController.Throw()`를 호출한다.
- `Yut Result`는 `Canvas` 바로 아래에 두고 `Yut Action Buttons`를 그 자식으로 둔다. 던지기 버튼이 숨겨진 동안에도 결과와 행동 버튼을 표시할 수 있다.
- `YutThrowController`의 `Sticks`에는 `YutStick 1`부터 `YutStick 4`까지 등록되어 있다. 각 윷에는 `YutStick`과 `SpriteRenderer`가 있으며, `YutStick`에 해당 렌더러와 배·등 스프라이트가 지정되어 있다.
- `EventSystem`이 버튼 입력을 처리한다.
- `YutThrowController`에 `Yut Result`의 `Image`와 도·개·걸·윷·모 스프라이트 다섯 개와 백도 스프라이트를 연결한다. 백도 이미지는 결과 시트의 백도 칸을 사용한다. 윷 위쪽의 결과 이미지는 첫 던지기 전까지 숨겨지고, 던질 때마다 새 판정에 해당하는 이미지로 갱신된다.

### 버튼 입력부터 결과 전달까지

1. [YutThrowController.Throw()](../Assets/Scripts/Yut/YutThrowController.cs)는 컴포넌트가 활성 상태인지 확인한다. `HasReadySticks()`에서 서로 다른 윷 네 개가 활성 상태이고 각 윷의 렌더러와 두 면의 스프라이트가 연결됐는지 검사한다. 설정이 올바르지 않으면 경고를 남기고 던지기를 시작하지 않는다.
2. [YutStick.Throw()](../Assets/Scripts/Yut/YutStick.cs)는 각 윷에서 한 번씩 같은 프레임에 호출된다. `Random.Range(0, 2)`로 자기 면을 뽑고 `YutFace.Belly` 또는 `YutFace.Back`을 즉시 반환한다. 현재 면과 뽑힌 면에 따라 등 → 등, 등 → 배, 배 → 등, 배 → 배 클립을 선택해 재생한다.
3. `YutThrowController`는 반환값에서 배의 수를 세어 위 표에 따라 `YutThrowResult`를 정하고 `LastResult`에 저장한다.
4. `YutThrowController`는 네 윷의 재생 완료를 기다린 뒤 `ShowResult()`로 결과 이미지를 표시하고 `ThrowCompleted` 이벤트를 전달한다. 재생 중에는 추가 던지기를 받지 않는다.
5. [TurnController](../Assets/Scripts/Turn/TurnController.cs)가 이벤트를 받아 결과를 보관하고 소환·이동 버튼을 표시한다. 선택한 행동에 결과를 전달하는 흐름은 [턴 흐름 명세](TurnSpec.md)에서 다룬다.

### 던지기 애니메이션

`YutStick` 프리팹에 `Animator`와 `Yut_BackToBack`, `Yut_BackToBelly`, `Yut_BellyToBack`, `Yut_BellyToBelly` 클립을 연결한다. `YutStick`은 Unity Playables로 선택한 `.anim` 클립을 직접 재생한다. 재생 시간은 클립 길이를 사용하며, 현재 네 클립은 60fps, 96프레임, 1.6초이고 반복하지 않는다. 재생 완료 후에는 최종 면의 기본 스프라이트를 표시한다.

## 남은 구현

- 비활성화 등으로 재생이 중단된 경우에는 윷을 결정된 최종 면으로 정리한다. 컨트롤러의 대기가 취소되면 완료 이벤트를 전달하지 않는다.

Yut Result 표시 이미지는 Assets/Art/YutResult/Yut_Results_Grid_3x2.png의 Multiple 스프라이트를 사용한다. _0=도, _1=개, _2=걸, _3=윷, _4=모, _5=백도이며 YutThrowController의 doSprite~backdoSprite에 순서대로 연결한다.

Yut Result의 RectTransform은 오른쪽 화면 중앙 Anchor X=0.8515625, Pos=(0,550), Pivot=(0.5,0.5), Size=200×120, Scale=(2,2,1)로 설정한다. 표시 영역은 400×240으로 원본 스프라이트의 640:384 비율과 일치한다. Yut Action Buttons의 Y=-5.5로 기존 화면 위치를 유지한다.
