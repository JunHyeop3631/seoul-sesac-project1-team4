# 적군 명세

## 피격 효과 풀링

`TakeDamage(damage, attackHitEffect)`는 공격자가 전달한 피격 효과를 우선 재생한다. 생략하면 적 프리팹의 기본 효과를 사용한다. 종류별 효과도 동일한 `HitEffectPool`에서 프리팹별로 재사용한다.

`HitEffectPool`은 `ThrowYut` 씬의 `TurnController` 아래에 배치한다. `EnemyPool`의 Inspector 참조로 연결하고, 적을 요청할 때 `SetHitEffectPool`로 해당 적에 전달한다.

피해가 적용되면 `Enemy.TakeDamage`가 [HitEffectPool](../Assets/Scripts/Enemy/HitEffectPool.cs)에 `Common_Hit` 재생을 요청한다. 프리팹별 풀에서 대기 효과를 재사용하고, 대기 객체가 없으면 생성한다. 매 재생 시 위치와 회전을 설정하고 Hit 상태를 처음부터 재생한다. 애니메이션 종료 이벤트 `OnHitAnimationFinished`에서 효과를 비활성화해 풀에 반환한다. 같은 프레임의 여러 피격 효과는 각각 독립된 객체로 재생한다. 풀은 적과 별도 오브젝트가 관리하며 씬 종료 시 정리한다.

[명세 색인](README.md) · [아군](AllySpec.md) · [웨이브](WaveSpec.md)

## 종류와 설정

`FastEnemy`와 `BossEnemy`도 공통 `Enemy`를 상속하며 각각 전용 프리팹을 사용한다. 적 풀에는 `FastEnemy = 1`, `BossEnemy = 2`으로 등록했다. 두 적의 Animator Override Controller는 NormalEnemy의 네 상태를 공유하면서 각자의 좌우 걷기·사망 클립으로 교체한다. 코드 초기값은 이동 속도2·체력10이지만 현재 스테이지는 프리팹의 통과 시간과 웨이브별 HP로 결정한다. 보스의 HP 바는 스프라이트 높이에 맞춰 배치했다.

`NormalEnemy`, `FastEnemy`, `BossEnemy`는 공통 [Enemy](../Assets/Scripts/Enemy/Enemy.cs)를 상속한다. 웨이브 데이터와 적 풀은 `EnemyId`를 사용해 종류를 구분한다. [NormalEnemy 프리팹](../Assets/Prefabs/NormalEnemy.prefab)의 `Visual` 자식에 SpriteRenderer와 Animator를 배치하고, [NormalEnemy Animator Controller](../Assets/Animations/Enemy/Animator/NormalEnemy.controller)를 연결했다. 새 스프라이트의 표시 높이는 자식 스케일로 기존 이미지에 맞췄다. 각 적 프리팹의 콜라이더는 [아군의 대상 탐색](AllySpec.md#대상-선택과-유지)에 사용한다.

| 프리팹 설정 | 코드 기본값 |
| --- | ---: |
| `Move Speed` | 초당 2유닛 |
| `Max Health` | 10 |

표의 값은 코드 기본값이다. 실제 스테이지 HP는 [웨이브 체력 표](WaveHealthSpec.md), 이동은 프리팹의 Lap Duration Seconds 일반30·빠른20·보스60을 따른다. 이 값이 양수이면 StartMoving이 닫힌 경로 전체 길이/통과 시간으로 Move Speed를 덮어쓴다.0이면 기존 Move Speed 직접 사용 경로를 유지한다.

## 등장과 경로 이동

1. 웨이브가 풀에서 적을 꺼내 활성화하면 `Enemy.OnEnable`에서 현재 체력을 최대 체력으로 설정한다.
2. `StartMoving`이 경유점의 월드 좌표를 보관하고, 마지막→첫 경유점의 닫는 구간까지 포함한 전체 길이로 종류별 통과 시간에 맞춰 속도를 계산한다. 적을 첫 경유점에 배치하고, `IsMoving` 상태에서 속도×Time.deltaTime만큼 이동한다. 한 프레임에 모서리에 도달하면 남은 이동 거리를 다음 구간에 사용한다. 풀 재사용 시 새 경로 길이를 다시 계산한다.
3. 마지막 경유점에서 출발점으로 돌아오면 도착 콜백을 호출한다. 웨이브는 도착한 적을 필드에서 제거해 풀로 반환한다. 이 경로에서는 처치 이벤트가 발생하지 않는다.

웨이브별 등장 구성과 씬의 경유점 설정은 [웨이브 명세](WaveSpec.md#씬-구성과-구현-상태)를 따른다. 일반·빠른 적의 도착은 하트-1이고,최종 보스는 작업23의 별도 탈출 이벤트로 잔여 하트와 관계없이 즉시 패배한다. [최종 보스 승패](BossOutcomeSpec.md).

## 피해와 처치

- 활성 상태이고 체력이 남은 적에 양수 피해가 들어오면 `TakeDamage`가 현재 체력을 낮춘다. 최솟값은 0이다.
- 피해가 적용될 때 적 중심에 `Common_Hit` 효과를 생성한다. 일반·빠른·보스 프리팹에 연결했으며, 기존 이동·사망 Animator와 독립적으로 11개 스프라이트를 60fps로 한 번 재생한다. 클립 끝의 `OnHitAnimationFinished` 이벤트에서 제거하며 속도 1일 때 약 0.1833초다. 자세한 연결은 [전투 효과 애니메이션](CombatEffects.md)을 따른다.
- 체력이 처음 0이 되면 이동을 멈추고 `Defeated` 이벤트가 한 번 발생한다. 웨이브는 적을 생존 집합에서 제거하고 처치 수를 즉시 증가시킨다. NormalEnemy는 0.5초 사망 애니메이션을 재생하고 `DeathCompleted`를 알린 뒤 풀에 반환된다. 웨이브는 사망 재생 중인 적의 반환까지 기다려 완료를 판정한다. 애니메이션을 설정하지 않은 적은 즉시 완료를 알린다.
- 처치와 경로 도착은 각각 별도로 알리며, 두 경로 모두 [웨이브 완료 판정](WaveSpec.md#완료-알림)의 필드 잔여 적 수에 반영된다.

## 일반 적 애니메이션

- 걷기 클립은 좌우 각각 60 FPS, 1초 반복이며 사망 클립은 60 FPS, 0.5초 단발이다.
- 소환 시 왼쪽 걷기로 초기화하고, 수평 이동 방향이 바뀌면 해당 방향의 걷기를 재생한다. 위아래 이동 시에는 마지막 좌우 방향을 유지한다.
- 사망 시 마지막 방향의 사망 클립을 재생한다. 체력이 0인 적은 아군의 공격 대상에서 제외한다.
- 적 컴포넌트에는 사망 클립을 따로 참조하지 않는다. `DeathLeft`·`DeathRight` 상태와 Controller 또는 Override Controller가 실제 클립을 결정한다. 여섯 사망 클립 모두 **종료 시각 0.5초**에 `OnDeathAnimationFinished` 이벤트가 있다. 자식 Animator의 [EnemyAnimationEvents.cs](../Assets/Scripts/Enemy/EnemyAnimationEvents.cs)가 이벤트를 받아 부모 `Enemy.CompleteDeathAnimation`을 호출하고, 사망 대기 중일 때만 `DeathCompleted`를 한 번 알린다. 살아 있거나 풀에서 반환·재사용된 적에게 들어온 종료 알림은 무시한다.
- 사망 완료 대기 코루틴과 고정 시간 대기는 사용하지 않는다. 속도 변경·일시정지는 이벤트 재생 시점에 반영된다. Animator나 해당 상태가 없는 적은 애니메이션 대기 없이 완료 처리한다. 사망 상태에는 종료 전 강제 전환을 추가하지 않는다.
- 목적지 도착 시에는 도착 콜백으로 풀에 반환된다.

## 풀 반환과 재사용

풀에 반환된 적은 비활성화된다. 이때 `OnDisable`이 이동 경로와 도착 콜백을 비우고 `ActivationVersion`을 증가시킨다. 같은 오브젝트가 다시 소환되면 최대 체력으로 시작하며, 아군은 버전 차이를 보고 이전 대상을 놓친 것으로 처리한다. 풀의 생성과 반환 구조는 [웨이브 명세](WaveSpec.md#적-오브젝트-풀-구조)를 참고한다.

## 검증

Walk·Death 시트 변경 후 12개 클립의 GUID와 내부 스프라이트 ID를 현재 에셋으로 재연결하고 세 프리팹의 초기 표시 이미지를 갱신했다. Unity 실행 검증에서 모든 클립의 전체 스프라이트 순서, 60 FPS, 걷기 1초·사망 0.5초의 실제 길이와 반복 여부를 확인했다. NormalEnemy·FastEnemy·BossEnemy의 좌우 걷기·사망 재생, 풀 반환 및 재사용 시 체력·애니메이션 복구를 확인했다.

수정된 빠른 적 왼쪽 사망 시트의 6장을 클립에 반영했다. 스프라이트 전환은 0·5·10·15·20·25프레임이며 마지막 스프라이트를 29프레임까지 유지한다. Unity가 마지막 키 다음 한 프레임을 포함하므로, 실제 클립 길이는 60 FPS 기준 30프레임(0.5초)이다. 실행 검증에서 마지막 여섯 번째 스프라이트와 FastEnemy 프리팹의 왼쪽 사망 재생을 확인했다.

FastEnemy와 BossEnemy는 Unity Play Mode에서 전용 클래스와 네 클립 교체, 좌우 걷기 및 프레임 전환, 사망 재생 중 활성 유지, 완료 후 풀 반환, 같은 인스턴스 재사용 시 체력과 걷기 상태 복구를 확인했다.

Unity 배치 실행에서 치명타 시 처치 이벤트 1회 발생, 이동 종료와 풀 반환, 재소환 시 최대 체력 복구를 확인했다. 경로를 완주한 적은 처치 이벤트 없이 풀에 반환되는 것도 확인했다.

일반 적 애니메이션 연결 후 Unity Play Mode에서 걷기 프레임 전환, 오른쪽 걷기·사망 상태 선택, 처치 수 즉시 증가, 사망 재생 중 활성 유지와 완료 후 반환, 같은 인스턴스 재사용 시 최대 체력·왼쪽 걷기 복구를 확인했다. 도착 시 처치 수가 증가하지 않는 것도 확인했다.
