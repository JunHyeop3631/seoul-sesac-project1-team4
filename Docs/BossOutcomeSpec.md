# 최종 보스 처치·탈출 결과 (2026-11-18)

작업23은 지정 원문의 최종 보스 전용 승패를 연결한다.10웨이브 HP6000 보스를 처치하면 보상100 정산 후 사망 연출을 기다리지 않고 승리한다. 탈출하면 잔여 하트와 관계없이 패배한다. 일반 적의 탈출 하트-1이나 일반 웨이브 완료60과 구분한다. 천명전의 보스200은 작업39 후속 범위다.

## 처치 순서

`Enemy.TakeDamage→Wave.DefeatEnemy→WaveManager.HandleEnemyDefeated`에서 기존 EnemyDefeated 수신자를 모두 실행한다. EnemyKillRewards가 StageCurrency에100을 지급해HUD를 갱신한 뒤,WaveManager가 BossDefeated를 보낸다. 따라서 보상과 승리를 서로 독립된 동일 이벤트 구독 순서에 맡기지 않는다.

TurnController는 실제 진행 중인 마지막 웨이브의 BossDefeated에만 반응한다. IsBattleWon=true를 먼저 확정하고 Wave.StopWave로 스폰·생존/사망 대기 적·진행 공격·투사체를 정리한 뒤 BattleWon을 보낸다. 보스도 풀에 반환되어 TakeDamage는 사망 애니메이션을 시작/대기하지 않는다. 기존 결과UI는 정산된 잔액 상태에서 승리를 표시한다. 일반 Completed/WaveCleared/완료60은 발생하지 않는다.

일반 EndBattle의 마지막 배열 항목 자동 승리 분기는 제거했다. 마지막 데이터가 잘못 일반 적으로 구성되어 완료됐다는 사실만으로 승리하지 않는다. 실제10웨이브 데이터의 보스1기 설정은 유지한다.

## 탈출 순서와 패배 상태

Wave.ReleaseEnemy가 적을 풀로 반환한 뒤 EnemyEscaped(Enemy)를 전달한다. WaveManager가 보스 종류를 확인해 BossEscaped를 보내고,TurnController가 IsBattleLost=true를 확정해 StopWave/준비UI 정리/BattleLost를 실행한다. 이 과정에서 Wave가 중단되므로 일반 EnemyArrived와 TryCompleteWave는 후속 진행하지 않는다.

보스 탈출에는 일반 하트-1을 적용하지 않으며 기존 잔여 하트는 유지한다. 패배는 별도 IsBattleLost로 표현한다. 입력·결과 정산·처치 보상이 이 상태도 검사하므로 하트가 남아 있어도 새 전투/추가 정산을 할 수 없다. 일반 하트0 패배도 같은 LoseBattle 경로를 사용한다. 보스의 잔여 하트를 임의로0으로 바꾸지 않는다.

승패 플래그는 전투 정리/결과 이벤트보다 먼저 확정한다. 늦은 피해·탈출·사망 완료·일반 완료·중복 중단은 이미 정한 결과와 잔액을 바꾸지 않는다. 결과UI의 일시정지를 해제하더라도 스폰/공격이 재개되지 않는다.

## 작업23 검증

Unity6000.3.28f1 ThrowYut 씬 복사본 Play Mode **51개 검사 통과·검사 구간 런타임 오류0**. [결과](Verification/BossOutcomeVerification.json)·[검증 도구](../Assets/Editor/BossOutcomeVerification.cs).

- 실제10웨이브 HP6000 보스를 치명 피해로 처치했다. TakeDamage 반환 전에 승리/잔액220/보스·발사된 투사체 회수를 확인했고,이벤트 순서는credit→win이었다. 승리 콜백 안에서도220과Wave 중단 상태를 확인했다. 사망 완료는 호출하기 전이다.
- 하트10/1에서 보스를 짧은 검증 경로로 실제 완주시켜 즉시 패배를 확인했다. 하트는 각각10/1을 유지하고 일반 탈출/완료/보상은0회였다.
- 최종 보스 뒤에 일반3기가 생성 대기하는 메모리 배치도 처치 순간 중단해 추가 스폰이 없었다. 실제 스테이지 편성을 바꾸거나 저장하지 않았다.
- 마지막 웨이브를 일반1기로 바꾼 잘못된 메모리 배치는 일반 완료만으로 승리하지 않으며 처치2 외에 완료60을 주지 않았다.
- 기존 결과UI에10/10 도달 상태가 전달되고,늦은/반복 피해·사망 완료·탈출·완료 호출과 일시정지 해제 뒤에도 결과1회/수입 고정/적·투사체 중단을 유지했다.

검사 중 웨이브 번호·하트·일부 편성/생성 간격·짧은 탈출 경로·시간은 메모리에서만 바꾸고 저장하지 않았다. 전체 빌드·결과UI 렌더링·아이템 배율은 이번 범위가 아니다. 기존 최종 승패 UI 아트/연출은 작업44~45의 별도 검증 범위다.

기존 완료 보상88개와 하트0 패배 중단17개 회귀도 통과·각 검사 구간 오류0이다(`Logs/CompletionRewardAfterBoss.json`, `Logs/DefeatStopAfterBoss.json`). 검증한 변경 런타임4개·검증 도구·씬의 SHA-256을 작업본과 대조했다. 원본 에디터는 유지하고 기존 검증 복사본/캐시를 재사용했다. 초기 검색 인덱스 예외는 검사 시작 전이며 오류0은 실제 검사 구간 기준이다.

배치 재실행은 별도 복사본에서 `-executeMethod BossOutcomeVerification.ReimportAndRun -nographics`를 사용한다. Logs/BossOutcomeVerification.json에 결과를 쓰고 검증 편집기를 종료하므로 원본의 열린 에디터에서 직접 실행하지 않는다.
