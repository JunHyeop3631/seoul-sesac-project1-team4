# 아군 명세

[명세 색인](README.md) · [윷 행동](YutActionSpec.md) · [적군](EnemySpec.md) · [웨이브](WaveSpec.md)

## 종류와 배치

도·개·걸·윷·모 아군은 각각 `Ally1`, `Gae_Warrior`, `Ally3`, `Ally4`, `Ally5`이며 공통 [Ally](../Assets/Scripts/Ally/Ally.cs)를 상속한다. 개 아군은 [Gae_Warrior 프리팹](../Assets/Prefabs/Gae_Warrior.prefab)과 [Gae_Warrior_left 이미지](../Assets/Art/Ally/Gae_Warrior_left.png)를 사용한다. 소환, 칸 점유, 이동은 [윷 행동 명세](YutActionSpec.md)를 따른다. `YutActionController`는 씬에 미리 배치된 아군과 새로 소환한 아군을 등록하고 대상 탐색에 사용할 `Wave`를 연결한다.

## 대상 선택과 유지

- 웨이브가 실행 중이고 사거리가 양수일 때 대상 확인과 공격을 수행한다.
- 현재 대상이 활성 상태이고 같은 소환 상태이며 XY 평면에서 사거리 안에 있으면 유지한다. 더 가까운 적이 들어와도 대상을 바꾸지 않는다.
- 대상이 없거나 사거리 밖으로 나가거나 비활성화되면 `Wave.FindAttackTarget`으로 새 대상을 찾는다. 적이 풀에서 반환·재사용됐는지는 [적군의 `ActivationVersion`](EnemySpec.md#풀-반환과-재사용)으로 구분한다.
- `Wave`는 프레임의 첫 범위 검색 전에 물리 좌표를 동기화한다. `Physics2D.OverlapCircle`로 적 레이어의 콜라이더를 조회한 뒤, 이번 웨이브에 남은 적 중 중심이 사거리 안에 있는 가장 가까운 적을 반환한다. 제곱 거리가 같으면 인스턴스 ID가 작은 적을 선택한다. 후보가 없으면 다음 프레임에 다시 검색한다.

## 기본 공격

- `Ally.LateUpdate`에서 대상을 확인한 뒤 공격 대기시간이 지났으면 `Enemy.TakeDamage`로 즉시 피해를 준다.
- 첫 공격은 대상을 확보한 시점에 가능하다. 이후에는 마지막 공격 시점부터 공격 간격을 계산하며, 대상이 바뀌어도 그 대기시간을 이어간다.
- 공격으로 적 체력이 0이 되면 아군은 현재 대상을 비운다. 적의 처치와 풀 반환 흐름은 [적군 명세](EnemySpec.md#피해와-처치)를 따른다.

| 프리팹 설정 | 코드 기본값 |
| --- | ---: |
| `Attack Range` | 3유닛 |
| `Attack Damage` | 10 |
| `Attack Interval` | 1초 |

표의 값은 코드 기본값이다. 실제 값은 각 프리팹 Inspector에서 조정한다.

## 검증

Unity 배치 실행에서 첫 타격의 체력 감소, 대기 중 체력 유지, 다음 타격, 처치 후 대상 해제와 대상 변경 시 공격 대기시간 유지를 확인했다. 탐색 방식의 비교는 [프로파일링 기록](Profiling/AllyTargetSearch.md)에 남겼다.
