# 캐릭터 정보 패널 구성

- ThrowYut 씬의 Character Info Panel에 제공된 참고 이미지의 배치를 적용한다.
- Assets/Art/Character_Info 내부 PNG는 Sprite (2D and UI), Single로 설정한다.
- 제목, 개 전사 이름, 별 두 개, 공격력/공격 속도 아이콘과 라벨, 구분선, 이야기 제목과 장식을 배치한다.
- 추가된 01_Icons/캐릭터 창.png를 Single로 사용해 Character Portrait Frame을 배치한다. 위치는 상단 중앙 앵커 기준 (-104,-339), 크기는 256×256이며 Preserve Aspect를 켠다. PNG의 투명 여백을 포함한 크기이며 실제 테두리는 약 228×224 영역을 차지한다. 캐릭터 그림은 선택 유닛의 Idle left Sprite로 표시한다.
- CharacterInfoPanel 컴포넌트가 선택 유닛 종류와 EvolutionStage를 받아 이름과 별을 갱신한다. 이름 이미지는 Inspector의 Profiles에서 수정한다. 공격력과 공격 속도는 선택한 Ally의 AttackDamage와 AttacksPerSecond를 읽어 실제 전투 프리팹과 일치시킨다. 수치는 아군 프리팹의 Attack Damage와 Attack Interval(초 단위)에서 수정한다.
- 본문은 YiSunShin-Bold.otf를 사용하는 UI Text로 구성한다.
- 본문: 마지막 주민이 돌아올 때까지 / 문을 닫지 않는 마을의 문지기. / 오늘도 모두의 귀갓길을 지킨다.
- 아군 선택 후 정보 버튼을 누르면 AllyActionButtons.RequestInfo가 패널을 연다.
- 선택 해제로 정보/진화 버튼이 비활성화되면 OnDisable에서 정보 패널도 닫는다. 선택한 유닛이 사라지거나 비활성화되어도 같은 흐름으로 닫는다.
- 다른 아군으로 선택이 바뀌면 기존 정보 패널을 닫는다. 같은 아군을 다시 클릭하면 열려 있는 패널을 유지한다.
- 패널의 기존 열기/선택 해제 시 닫기 흐름을 유지한다. 정보 버튼을 누를 때 먼저 내용을 갱신한 후 패널을 표시한다. EvolutionStage가 2이면 별 2개, 1이면 별 1개를 표시한다. 이야기 본문은 고정한다.
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
