# 윷 던지기 명세

- 플레이어가 던지기 버튼을 한 번 누르면 윷 네 개를 한꺼번에 던진다.
- 윷마다 배(평평한 면)와 등(둥근 면)을 각각 50% 확률로 판정한다. 네 윷의 판정은 서로 독립적이다.
- 배가 위를 향한 윷의 개수로 결과를 정한다.

| 배 개수 | 결과 |
| ---: | --- |
| 0 | 모 |
| 1 | 도 |
| 2 | 개 |
| 3 | 걸 |
| 4 | 윷 |

- 빽도는 적용하지 않는다.

## 현재 구현

`YutThrowController`는 한 번의 던지기 전체를 관리한다. 윷 네 개를 던지고, 각 윷의 면을 모아 판정하며, 결과 이미지를 표시하고 완료를 알린다. `YutStick`은 자기 윷의 면을 뽑고 표시한다.

### 씬 구성

- [ThrowYut 씬](../Assets/Scenes/ThrowYut.unity)의 `Canvas > Throw Button`은 화면에 `Throw`로 표시된다. 버튼의 `On Click`에는 `YutThrowController.Throw()`가 연결되어 있다.
- `YutThrowController`의 `Sticks`에는 `YutStick 1`부터 `YutStick 4`까지 등록되어 있다. 각 윷에는 `YutStick`과 `SpriteRenderer`가 있으며, `YutStick`에 해당 렌더러와 배·등 스프라이트가 지정되어 있다.
- `EventSystem`이 버튼 입력을 처리한다.
- `YutThrowController`에 `Yut Result`의 `Image`와 도·개·걸·윷·모 스프라이트 다섯 개를 연결한다. 윷 위쪽의 결과 이미지는 첫 던지기 전까지 숨겨지고, 던질 때마다 새 판정에 해당하는 이미지로 갱신된다.

### 버튼 입력부터 결과 전달까지

1. [YutThrowController.Throw()](../Assets/Scripts/YutThrowController.cs)는 컴포넌트가 활성 상태인지 확인한다. `HasReadySticks()`에서 서로 다른 윷 네 개가 활성 상태이고 각 윷의 렌더러와 두 면의 스프라이트가 연결됐는지 검사한다. 설정이 올바르지 않으면 경고를 남기고 던지기를 시작하지 않는다.
2. [YutStick.Throw()](../Assets/Scripts/YutStick.cs)는 각 윷에서 한 번씩 같은 프레임에 호출된다. `Random.Range(0, 2)`로 자기 면을 뽑아 해당 스프라이트를 즉시 표시하고, `YutFace.Belly` 또는 `YutFace.Back`을 반환한다.
3. `YutThrowController`는 반환값에서 배의 수를 세어 위 표에 따라 `YutThrowResult`를 정하고 `LastResult`에 저장한다.
4. `YutThrowController.ShowResult()`는 판정에 해당하는 스프라이트를 `Yut Result`에 표시한다. 이어서 `ThrowCompleted` 이벤트로 판정 결과를 전달한다.

## 남은 구현

- 윷이 떠올랐다가 착지하는 연출을 연결한다. 각 윷의 면과 전체 판정은 던지는 순간 결정한다. 네 윷이 모두 착지해 최종 면을 표시하면 `YutThrowController`가 완료를 알린다. 임시 연출 시간은 두지 않는다.
- 게임 시스템에서 완료 시점에 판정 결과를 받아 말 이동 등 턴 처리에 연결한다.
