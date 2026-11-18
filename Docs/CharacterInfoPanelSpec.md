# 캐릭터 정보 패널 구성

작업31 완료(2026-11-19): 진화 버튼의 ‘표시 대상’과 ‘실행 가능’ 조건을 분리했다. 준비 중 활성 일반1성은 재료가 부족해도 버튼을 표시하며 부족 시 비활성/무실행·무소비·무안내다. 일반2성과 백호는 버튼 GameObject 자체를 숨긴다.1성이라도 던지기·보유 결과·입력 잠금 중에는 실행을 막고 전투/정지/승패/비활성 대상에서는 선택과 버튼을 정리한다. 실행 성공/선택 해제 직후에도 이전 버튼이 남지 않도록 갱신한다. 아래31 잔여는 이전 시점 기록이다.

## 작업31 진화 버튼 정책과 검증

`AllyActionButtons.UpdateEvolutionButton`은 활성 준비 대상의 `Ally.CanEvolve`로 버튼 표시 여부를 결정하고,기존 `YutActionController.CanEvolve(target)`로 활성화 여부를 결정한다. 일반 Ally의 CanEvolve는 EvolutionStage==1이며 WhiteTiger는 false로 재정의한다. 따라서2성·백호는 비활성 그림조차 남기지 않고 숨긴다. RequestEvolution 후 즉시 갱신하고 OnDisable에서 비활성/숨김을 함께 적용해 늦은 클릭으로 이전 대상 버튼이 남지 않게 한다. 실제 진화 재료/풀/점유 처리 코드는 유지했다.

[검증 도구](../Assets/Editor/EvolutionButtonPolicyVerification.cs)를 실제 씬 복사본 Play Mode로 실행해97개 검사 통과·검사 구간 런타임 오류0. [결과](Verification/EvolutionButtonPolicyVerification.json). 일반5종 각각의 재료1/2기 표시·비활성/무실행/무소비/무안내,재료3기에서 실제 포인터 버튼 클릭으로 진화1회,2성·백호의 숨김/늦은 이벤트/직접 호출 거부를 확인했다. 다른 종류/다른 칸 재료는 합산하지 않고 보유 결과/던지기/정지/실제 특별소환 처리/승패/실제 전투/비활성 대상 차단과 다음 준비 복구도 확인했다. 일부 상태는 메모리 속성 fixture이며 실제 윷 애니메이션 전체의 재검증은 기존 준비 입력 회귀에서 구분한다.

실제1920×1080 렌더링: [1성 재료 부족](Previews/EvolutionOneStarShortage.png)·[2성 숨김](Previews/EvolutionTwoStarHidden.png)·[백호 숨김](Previews/EvolutionTigerHidden.png). 전체 빌드/수동 완주는 실행하지 않았다.

작업31 회귀는 기존 준비 입력53개·정보 전환167개,합계220개 통과·각 검사 구간 오류0이다. [회귀 결과](Verification/EvolutionButtonRegressionVerification.json). 변경 런타임/검증 코드2개 파일이 검증 복사본과 SHA-256 일치하며 변경 문서 로컬 링크와 `git diff --check`를 통과했다.

Game 뷰에서1성1기를 클릭하면 회색 진화 버튼이 보이고 눌러도 변화/안내가 없어야 한다. 같은 칸 같은1성3기로 만들면 버튼이 활성화되어 진화하며,생성된2성을 다시 클릭하거나 백호를 클릭하면 진화 버튼이 보이지 않아야 한다.

작업30 완료(2026-11-19): 기존 배경·테두리/배치를 고정하고 기본 내용→정보,정보→기본 내용,정보→다른 유닛 정보의 순차 페이드를 연결했다. 구현 선택으로 기본 전환 시간을0.24초(나가기/들어오기 각0.12초),unscaledDeltaTime 기반으로 둔다. 두 내용이 동시에 보이지 않으며 빠른 재선택/빈 곳 클릭은 현재 진행 중 상태를 되돌려 마지막 요청에 수렴한다. 전투·일시정지·대상 소멸·강제 비활성은 즉시 정리한다. 아래29의 ‘30 잔여’는 당시 기록이다.

작업29 완료(2026-11-19): 개별 유닛 클릭으로 추가 정보 버튼 없이 즉시 초상/이름/성급/실제 ATK·AS를 표시한다. 기존 오른쪽 배경·테두리는 유지하고 윷/상점/인벤토리/전투 시작 쪽 내용만 숨긴다. 정보 패널의 중복 배경은 투명하게 두고 포인터 차단만 유지한다. 빈 보드 클릭은 기본 내용으로 복귀하며 잔액·윷 결과·유닛 배치를 소비하지 않는다. 백호도 기존 이름/대기 이미지와 실제100/0.5를 연결한다. 아래 ‘정보 버튼 추가 클릭/백호 미연결’은 이전 구현 시점 기록이다. 전환 애니메이션은30,진화 버튼 성급별 숨김은31에 남긴다.

작업29의 백호는 현재 프리팹 EvolutionStage=1을 읽어 별1개를 표시한다. 새로운 성급/진화 규칙을 추가하지 않는다. 제공된 백호 이야기 본문은 확인되지 않아 빈 Profile을 두고 이야기 장식을 숨긴다. 일반5종1·2성의 기존 공통 이야기는 유지한다.

## 작업30 전환 상태와 검증

- `CharacterInfoPanel`은 ToInfo/SwapOut/ToDefault/Info 상태와 단일 blend값을 사용한다. 기본→정보는 blend -1→0에서 기본 내용을 숨기고0→1에서 정보를 나타낸다. 반대 방향은1→0→-1이다. SmoothStep으로 양 끝 변화가 부드럽게 멈추도록 한다.
- 정보 내용은 루트 CanvasGroup으로,윷·상점 등 기본 UI는 기존7개 CanvasGroup으로 페이드한다.4개 월드 윷은 원래 색상/활성 상태를 저장해 알파만 페이드하고 종료 시 정확히 복원한다. 배경 Sprite/테두리/위치/배율은 움직이지 않는다.
- 다른 유닛을 선택하면 기존 정보를0까지 숨긴 뒤 최신 요청 대상의 이름/초상/능력치를 채우고 다시 나타낸다. CurrentAlly는 최신 요청,displayedAlly는 실제 표시 데이터의 대상을 구분한다. 빠른 연속 선택은 진행률을 초기화하거나 코루틴을 쌓지 않고 최신 목표만 바꾼다.
- 빈 곳 클릭과 행동 버튼 선택 해제는 `Hide()`로 복귀 애니메이션을 시작한다. 복귀 중 재선택은 같은 진행 상태에서 방향을 되돌린다. 전환 중 기본 버튼의 raycast/interactable은 막고 기본 화면 복귀가 끝나면 이전 값을 복원한다.
- 준비 상태 종료/일시정지/비활성·제거 대상/외부 강제 비활성은 페이드 완료를 기다리지 않고 즉시 정리한다. 잔액·윷 결과·배치·기존 윷 색상은 보존한다.

[전환 검증 도구](../Assets/Editor/CharacterInfoTransitionVerification.cs)를 실제 씬 복사본에서 실행해167개 검사 통과·검사 구간 런타임 오류0. [결과](Verification/CharacterInfoTransitionVerification.json). 진입/복귀 양쪽 중간 프레임,연속 재선택·취소/재진입·이전 요청 무효화,매 전환 프레임의 두 내용 비중첩/0~1 알파,배경·배치 고정,원래 윷 색상 복원,진행 중 정지/풀 반환/강제 비활성/전투 진입을 확인했다. 검사 수는 중간 프레임 관찰 수에 따라 실행 환경에서 달라질 수 있다.

실제1920×1080 렌더링: [정보 진입 중](Previews/InfoTransitionEntering.png)·[정보 표시 완료](Previews/InfoTransitionSettled.png)·[정보 복귀 중](Previews/InfoTransitionLeaving.png)·[기본 화면 복귀](Previews/InfoTransitionReturned.png). 정적 PNG는 각 순간의 증거이며,시간에 따른 연속 상태는 실행 검사로 검증했다. 전체 빌드/수동 완주는 실행하지 않았다.31의 성급별 진화 버튼 숨김은 남긴다.

작업30 회귀: 전환 완료를 기다리도록 갱신한 기존 정보 내용162개·준비 입력53개·특별소환창43개,합계258개 통과·각 검사 구간 오류0이다. [회귀 결과](Verification/CharacterInfoTransitionRegressionVerification.json). 변경 런타임/씬/생성기/검증 코드6개 파일의 검증 복사본 SHA-256 일치,문서 링크와 `git diff --check`를 확인했다.

Game 뷰에서는 유닛 클릭→정보 진입 도중 다른 유닛 클릭→빈 보드 클릭→즉시 다시 선택 순서로 확인한다. 배경 테두리는 움직이지 않고 마지막 대상 또는 윷 화면만 남아야 한다. 전환 중 일시정지/전투 시작 시에도 흐릿한 내용이나 클릭 차단이 남지 않아야 한다.

## 작업29 실행 흐름과 검증

1. `TurnController.LateUpdate → HandleBoardClick → YutActionController.GetAllyAt`으로 클릭 좌표의 개별 유닛을 찾는다. 기존 `AllyRangeDisplay.Show → targetChanged → AllyActionButtons.SetTarget` 연결에서 `CharacterInfoPanel.Show`를 즉시 호출한다. 별도 정보 버튼은 숨기고 진화 버튼만 가운데에 둔다. 진화 실행 조건/성급별 버튼 숨김은31 범위다.
2. 이동 모드에서는 목적지 선택을 먼저 처리한다. 유닛을 클릭하면 이동 소속 묶음은 기존 최대3기를 유지하고 정보/사거리는 클릭한1기를 따른다. 빈 보드 클릭은 정보와 사거리만 숨기며 결과를 버리거나 이동을 확정하지 않는다.
3. 정보 표시는 기존7개 UI 루트의 CanvasGroup alpha/interactable/raycast 상태와4개 윷 SpriteRenderer의 enabled 상태를 저장해 내용만 숨긴다. 기본 오브젝트 자체를 끄지 않아 RefreshYutButtons와 윷 결과/애니메이션 상태를 유지한다. 정보 종료 시 저장한 표시 상태를 복원한다. 월드 배경의 Sprite/위치/배율은 변경하지 않는다.
4. 유효한 대상 정보로 바뀔 때 기존 상점/인벤토리는 닫고 정보 내용으로 대체한다. 빈 클릭은 윷 내용으로 돌아온다. 정보 패널의 투명 Image는 오른쪽 패널 내부 클릭이 빈 보드 클릭으로 처리되지 않도록 포인터 차단만 담당한다.
5. 선택한 개별 Ally의 실제 ATK/AS/EvolutionStage를 표시한다. 대상의 능력치/성급이 바뀌면 갱신하고,빠른 재선택은 최신 대상으로 바꾼다. 비활성/제거/null 대상 또는 준비 종료·일시정지에서는 닫아 이전 정보가 남지 않도록 한다.

[생성기](../Assets/Editor/CharacterInfoContentBuilder.cs)는 기존 ThrowYut 씬의 배치/이미지를 유지하고 표시 그룹·백호 Profile·추가 정보 버튼 비활성 상태를 저장한다. [검증 도구](../Assets/Editor/CharacterInfoContentVerification.cs)는 실제 씬 복사본 Play Mode에서162개 검사 통과·검사 구간 런타임 오류0을 확인했다. [결과](Verification/CharacterInfoContentVerification.json).

일반10종+백호1종의 개별 표시/빈 곳 복귀,공통 이름/이야기,별/실제 ATK·AS/비율 유지 초상,배경 고정,기존 결과/잔액/배치 보존,개별 정보와3기 이동의 분리,목적지 우선 처리,비활성 대상,빠른 재선택,상점 대체,일시정지/전투 차단을 확인했다. 클릭은 실제 보드 좌표의 HandleBoardClick/GetAllyAt 경로를 사용하며 이 검증만으로 실제 마우스 장치 입력 전체를 재검증했다고 주장하지 않는다.

Direct3D11/1920×1080 실제 렌더링: [2성 개 전사](Previews/InfoWarriorTwoStar.png)·[백호](Previews/InfoWhiteTiger.png)·[윷 화면 복귀](Previews/InfoReturnedToYut.png). 선택된 백호/2성의 진화 버튼 숨김은31에 남아 현재 비활성 버튼이 보일 수 있다. 전환 애니메이션·전체 빌드·수동 완주는 이번 범위가 아니다.

회귀는 기존 준비 입력53개(가상 마우스 입력 포함)·특별소환창43개·이동 미리보기65개,합계161개 통과·각 검사 구간 오류0이다. [회귀 결과](Verification/CharacterInfoRegressionVerification.json). 초기 회귀의 씬 재로드에서 파괴된 PreparationNotice에 `?.Hide`가 호출되는 MissingReferenceException을 발견해 Unity null 검사로 수정했다. 준비 입력 검증의 시작 대기도20회 Editor update로 맞춰 초기 에디터 검색 인덱스 예외와 게임 검사 구간을 구분했다. 오류를 무시하는 필터는 추가하지 않았다.

Game 뷰에서 일반/2성/백호를 클릭해 초상·이름·별·ATK/AS와 사거리 원이 해당1기에 맞는지 확인한다. 빈 보드 클릭 시 윷·상점 버튼이 복원되고 잔액/보유 윷 결과가 유지돼야 한다. 같은 칸의 일반3기 중1기를 선택해도 정보는1기,이동은기존3기 묶음을 따라야 한다.

아래는 작업29 이전 구현 및 당시 검증 기록이다.

- ThrowYut 씬의 Character Info Panel에 제공된 참고 이미지의 배치를 적용한다.
- Assets/Art/Character_Info 내부 PNG는 Sprite (2D and UI), Single로 설정한다.
- 제목, 개 전사 이름, 별 두 개, 공격력/공격 속도 아이콘과 라벨, 구분선, 이야기 제목과 장식을 배치한다.
- 추가된 01_Icons/캐릭터 창.png를 Single로 사용해 Character Portrait Frame을 배치한다. 위치는 상단 중앙 앵커 기준 (-104,-339), 크기는 256×256이며 Preserve Aspect를 켠다. PNG의 투명 여백을 포함한 크기이며 실제 테두리는 약 228×224 영역을 차지한다. 캐릭터 그림은 선택 유닛의 Idle left Sprite로 표시한다.
- CharacterInfoPanel 컴포넌트가 선택 유닛 종류와 EvolutionStage를 받아 이름과 별을 갱신한다. 이름 이미지는 Inspector의 Profiles에서 수정한다. 공격력과 공격 속도는 선택한 Ally의 AttackDamage와 AttacksPerSecond를 읽어 실제 전투 프리팹과 일치시킨다. 수치는 아군 프리팹의 Attack Damage와 Attack Interval(초 단위)에서 수정한다.
- 본문은 YiSunShin-Bold.otf를 사용하는 UI Text로 구성한다.
- 본문: 마지막 주민이 돌아올 때까지 / 문을 닫지 않는 마을의 문지기. / 오늘도 모두의 귀갓길을 지킨다.
- 아군 선택 후 정보 버튼을 누르면 AllyActionButtons.RequestInfo가 패널을 연다.
- 작업02(2026-11-18): 준비 단계에서만 아군 선택·사거리·정보 진입을 허용한다. `RequestInfo`와 `CharacterInfoPanel.Show`에서 모두 검사하며, 패널을 직접 활성화해도 준비 상태가 아니면 닫는다. 전투·승패 진입 시 TurnController가 정보창과 선택을 정리한다.
- 진화 실행과 버튼 활성 조건은 준비 단계·미사용 윷 결과 없음·던지기 연출 없음에 기존 성급/재료 조건을 함께 적용한다. 개별 정보 내용/전환과 성급별 버튼 숨김 정책은 작업29~31에서 처리한다.
- 선택 해제로 정보/진화 버튼이 비활성화되면 OnDisable에서 정보 패널도 닫는다. 선택한 유닛이 사라지거나 비활성화되어도 같은 흐름으로 닫는다.
- 다른 아군으로 선택이 바뀌면 기존 정보 패널을 닫는다. 같은 아군을 다시 클릭하면 열려 있는 패널을 유지한다.
- 패널의 기존 열기/선택 해제 시 닫기 흐름을 유지한다. 정보 버튼을 누를 때 먼저 내용을 갱신한 후 패널을 표시한다. EvolutionStage가 2이면 별 2개, 1이면 별 1개를 표시한다. 이름과 이야기는 유닛 종류별 Profile에서 공유한다. 1성과 2성은 같은 이름·이야기를 사용한다.
- Character Portrait Frame 자식 Character Portrait에 선택 유닛의 Idle left 정지 Sprite를 표시한다. Profiles의 Idle Left / Evolved Idle Left에 기본·2차 진화 10종을 연결한다. 초상화는 중앙 앵커/피벗, 위치 (-3,4), 크기 208×200, Preserve Aspect로 원본 비율을 유지한다. 기존 프레임 이미지는 유지한다.

초상화 검증: Unity에서 10종 프리팹으로 Show를 호출해 연결 Sprite 이름, Image 활성화와 Preserve Aspect를 확인했다. Scene/Game 뷰에서 2성 개 전사 미리보기의 크기와 프레임 안쪽 여백을 확인했다. 코드 빌드 경고 0/오류 0. 임시 검증 스크립트는 제거했다.

검증: Assembly-CSharp 빌드 경고 0 / 오류 0. Unity Play Mode에서 소환 → 아군 선택 → 정보 버튼 → 패널 열림 → 빈 곳 클릭 → 선택 버튼과 패널 닫힘을 확인했다.

캐릭터 테두리 배치는 Unity Scene/Game 뷰에서 확인했다. 이름/별/능력치 배치는 유지한다.

## 유닛별 표시 데이터

| 유닛 | 1성 ATK | 2성 ATK | AS 회/초 |
| --- | ---: | ---: | ---: |
| 돼지 도사 | 12 | 25.2 | 1.2 |
| 개 전사 | 25 | 52.5 | 1.5 |
| 양 창병 | 18 | 37.8 | 0.8 |
| 소 철퇴병 | 35 | 73.5 | 0.5 |
| 말 궁수 | 8 | 16.8 | 3.0 |

공격력은 불필요한 소수점 없이, 공격 속도는 소수점 한 자리와 회/초 단위를 표시한다. DPS/사거리/기본 공격 UI는 추가하지 않는다.

동적 표시 검증: Unity에서 다섯 유닛의 기본/2차 진화 프리팹 총 10개를 생성해 실제 Show 호출 후 이름 Sprite, 별 활성화, 공격력과 공격 속도 문자열이 표와 일치하는 것을 확인했다. null 선택 시 패널 닫힘도 통과했다. Play Mode에서는 소 철퇴병을 소환하고 정보 버튼으로 별 1개, 공격력 35, 공격 속도 0.5회/초 표시를 확인했으며 빈 곳 클릭 시 패널이 닫히는 것도 확인했다. 임시 검증용 Editor 스크립트는 확인 후 제거했다.

## 캐릭터별 이야기 연결 (2026-11-18)

정보 버튼의 RequestInfo 경로를 유지한다. 캐릭터 선택만으로 정보창을 열지 않는다. Profiles의 Story에 캐릭터별 3줄 이야기를 저장하고 Story Lines의 UI Text 3개에 표시한다. 진화 성급은 초상, 별, 실제 Ally 공격력·공격 속도 표시에만 사용한다. 수치는 표시용 상수가 아니라 각 프리팹의 AttackDamage/AttacksPerSecond를 읽는다. 현재 플레이 가능한 일반 아군 다섯 종류의 10개 프리팹을 대상으로 한다.

## 백호대장군 이야기 확정 (2026-11-19)

기존의 백호 이야기 미확정/빈 본문 기록을 대체한다. 백호 Profile과 정보창 생성기에 다음 3줄을 연결했다. 본문이 있으므로 기존 표시 로직에서 이야기 제목과 장식도 함께 표시된다.

도깨비를 봉인한 전설의 장군.
수호자들의 간절한 부름에
잠들었던 창을 다시 들어 올린다.
