# 윷 행동 컨트롤러 명세

[명세 색인](README.md) · [턴 흐름](TurnSpec.md) · [윷 던지기](YutThrowSpec.md) · **윷 행동** · [윷판](YutBoardSpec.md) · [웨이브](WaveSpec.md)

목차

- [1차 범위](#1차-범위)
- [의존 정보](#의존-정보)
- [동작 규칙](#동작-규칙)
  - [아군 배치](#아군-배치)
  - [아군 이동](#아군-이동)
  - [선택 처리](#선택-처리)
- [후속 범위와 미정 사항](#후속-범위와-미정-사항)

## 1차 범위

`YutActionController`는 호출에 전달된 윷 결과를 사용해 **아군 배치** 또는 **아군 이동**을 처리한다.

윷 행동은 턴 시작 후 준비 단계에서 완료한다. 윷 결과 보관, 행동 선택 확정, 턴·웨이브 진행은 [턴 흐름 명세](TurnSpec.md)에서 다룬다.

윷 결과의 판정과 전달은 [윷 던지기의 버튼 입력부터 결과 전달까지](YutThrowSpec.md#버튼-입력부터-결과-전달까지), 행동 확정·완료 시점과 입력 제한은 [턴 명세의 행동 선택과 준비 중 상점 이용](TurnSpec.md#행동-선택과-준비-중-상점-이용)을 따른다.

## 의존 정보

| 정보 | 제공 주체 | 사용 목적 |
| --- | --- | --- |
| 행동에 사용할 윷 결과 | `DeployAlly` 또는 `SelectAlly`의 호출 인자 | 배치할 프리팹이나 이동 후보를 결정한다. |
| 칸 연결과 이동 후보 | [YutBoard](../Assets/Scripts/Yut/YutBoard.cs)의 `GetDestinationCells` | 아군의 현재 칸과 윷 결과를 바탕으로 도착 후보를 계산한다. |
| 윷판 칸 | 씬의 `TurnController > Cells` 바로 아래에 있는 [YutCell](../Assets/Scripts/Yut/YutCell.cs) 컴포넌트 | `Board Root`에 `Cells`를 연결한다. 시작할 때 자식의 `CellId`로 칸을 등록하며, 배치된 아군과 도착 후보 표시는 해당 Cell의 자식이다. |
| 아군 유형과 프리팹 | [Ally](../Assets/Scripts/Ally/Ally.cs) 및 `Ally1`~`Ally5` 컴포넌트가 붙은 프리팹 | 컨트롤러가 윷 결과에 맞는 프리팹을 선택해 생성한다. |
| 아군의 현재 칸 | [YutBoardOccupancy](../Assets/Scripts/Yut/YutBoardOccupancy.cs) | 배치와 이동이 같은 점유 상태를 공유한다. 선택한 아군의 현재 칸을 조회하고, 동작이 완료되면 해당 아군의 칸을 갱신한다. |

## 동작 규칙

### 아군 배치

`Summon Button`은 `Assets/Art/Button/Yut_UI_Separated`의 분리 이미지로 구성한다. `Action_Normal_Full`·`Action_Pressed_Full`을 배경과 누름 상태에 연결하고, 자식 Image에 소환 아이콘·소환 제목·돼지 도사 이름·하단 소환 문구를 배치한다. 조립 기준 크기는 `280 × 193.846154`이며 부모 UI의 2배 배율을 고려해 로컬 배율 `0.31964593`로 표시한다. 실제 Canvas 기준 크기는 약 `179.001721 × 123.924269`이다. 기존 `TurnController.ChooseSummon` 클릭 연결을 유지한다. 현재 유닛 이름 표시는 도 이미지로 고정되어 있고 윷 결과별 교체는 이 UI 구성 작업에 포함하지 않는다.

`Throw Button`은 `Throw_Normal_NoCost_0`의 스프라이트 비율 `1743:835`에 맞춰 `187.5 × 89.82358`로 설정한다. `Summon Button`은 `Action_Normal_Full`의 비율 `832:576`을 따른다. 두 Image 모두 Preserve Aspect를 켠다. 윷 던지기 버튼의 높이를 늘린 뒤 Y를 514.964716으로 올리고, 자식 Yut Result의 Y를 240.929432로 보정해 결과 및 행동 버튼의 화면 위치는 유지한다.

두 버튼은 원본 스프라이트의 1px당 동일한 Canvas 배율 `375 / 1743`을 적용한다. Throw의 표시 크기는 약 `375 × 179.64716`, Action의 표시 크기는 약 `179.001721 × 123.924269`이며, 표시 너비의 비도 원본 너비의 비인 `1743:832`와 일치한다. 소환 버튼 배경과 Throw 버튼을 함께 25% 확대한다. 소환 버튼 자식은 제공된 완성 이미지에 맞춰 중앙 앵커/피벗으로 배치한다. 아이콘은 위치 (-51, 42), 크기 90×80; 상단 소환 제목은 (55, 39), 104×60.32; 돼지 도사 이름은 (0, -27), 145×48.160714; 하단 소환 문구는 (0, -60), 69×40.02로 구성한다.

소환 버튼을 누르면 결과에 대응하는 아군 프리팹 **한 개**를 **`Cell 01`의 자식**으로 생성하고 점유 상태에 등록한다. 1번 칸은 오른쪽 아래 시작점인 0번 칸 바로 위에 있다.

| 결과 | 배치할 아군 | 기존 아군을 이동시킬 때의 거리 |
| --- | --- | ---: |
| 도 | `Ally1` | 1칸 |
| 개 | `Gae_Warrior` | 2칸 |
| 걸 | `Ally3` | 3칸 |
| 윷 | `Ally4` | 4칸 |
| 모 | `Ally5` | 5칸 |

### 아군 이동

1. 플레이어가 이동 버튼으로 행동을 확정한 뒤, 이미 배치된 아군을 클릭한다.
2. 컨트롤러가 해당 아군의 현재 칸과 윷 결과로 도착 후보를 계산하고, 각 후보 칸에 선택 표시를 띄운다.
3. 플레이어가 표시된 칸을 클릭하면 선택한 아군을 해당 Cell의 자식으로 옮기고, 출발 칸과 도착 칸의 점유 상태를 갱신한다.

예를 들어 개가 나왔을 때 4번 칸의 아군을 클릭하면 6번을 표시한다. 5번 칸의 아군을 클릭하면 바깥길의 7번과 대각선의 21번을 표시한다.

칸 연결·분기별 이동 경로와 점유 관리는 [윷판 명세의 현재 구현](YutBoardSpec.md#현재-구현), 아직 정하지 않은 도착·완주 처리는 [윷판의 미정 규칙](YutBoardSpec.md#미정-규칙)을 참조한다.

### 선택 처리

이동을 확정한 뒤에는 다른 아군을 선택해 도착 후보를 갱신할 수 있다. 도착 후보는 [지점표시.png](../Assets/Art/지점표시.png)로 표시하고, 이동이 완료되면 지운다. 소환하거나 이동한 아군의 로컬 위치는 해당 Cell의 `(0, 0, 0)`이다.

## 후속 범위와 미정 사항

- [윷판의 수용 인원 규칙](YutBoardSpec.md#후속-구현-규칙)에 따라 각 칸에는 최대 6마리까지 배치하거나 이동할 수 있다. 가득 찬 칸은 이동 후보에서 제외하며, 배치 실패 시 윷 결과를 유지하고 행동을 다시 선택한다.
- 아군 유형별 구체적인 스펙은 추후 정의한다.


Move Button은 Summon Button과 같은 크기(280 × 193.84616), 배율(0.31964594), 상단 Y 위치를 사용한다. Action_Normal_Full / Action_Pressed_Full을 연결한다. 자식 Image는 Move Icon, Move Title, Move Step, Move Subtitle로 구성하고 Raycast Target을 끈다. 참고 이미지에 맞춰 상단에 화살표와 이동 제목, 구분선 아래에 Do_1(1칸)과 이동 문구를 한 줄로 배치한다. ChooseMove 클릭 연결은 유지하며 현재 문구는 1칸 고정이다.

오른쪽 배경 패널의 가로 범위(기준 해상도에서 약 X=1350~1920)의 중앙 X=1635를 버튼 공통 중심으로 사용한다. Throw Button은 오른쪽 패널 중앙 앵커 X=0.8515625 기준 Pos X=0, Yut Result와 Yut Action Buttons의 Pos X는 0으로 설정한다. Summon/Move Button은 각각 Pos X=-54/+54로 같은 중심에서 대칭이며 높이와 크기도 같다.

Single 스프라이트 전환 후 PNG의 투명 여백 때문에 실제 그림이 축소되는 문제는 자식 Background Image로 보정한다. 버튼 루트의 크기·앵커·Pivot·클릭 영역을 유지하고 루트 Image는 알파 0으로 둔다. 자식 배경은 이전 스프라이트 영역과 같은 픽셀 배율 및 중심으로 배치하며, Button Target Graphic은 자식 배경을 참조한다. 내부 이미지 8개도 이전 잘라낸 영역과 원본 PNG의 크기/중심 차이를 반영해 RectTransform을 보정한다. 스프라이트 Import Mode는 Single을 유지한다.

Throw/Summon/Move Button의 Transition은 Sprite Swap을 사용한다. Throw는 Throw_Pressed_NoCost, Summon과 Move는 Action_Pressed를 Pressed Sprite로 참조한다. Target Graphic은 각각 Throw Background, Summon Background, Move Background의 Image이며, 손을 떼면 기본 이미지로 복귀한다.
