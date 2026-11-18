# 하트0 패배와 전투 중단 (2026-11-18)

작업15는 하트0 이후 추가 생성·공격·처치/완료 보상이 계속되지 않도록 전투를 중단한다. 보스 탈출 즉시 패배와 처치 즉시 승리는 작업23의 별도 조건이다.

## 종료 흐름

`Enemy.Update`가 실제 경로를 완주하면 `Wave.ReleaseEnemy`가 해당 적을 점유 집합에서 제거하고 이벤트를 해제해 풀로 반환한 뒤 `EnemyArrived`를 보낸다. `WaveManager→TurnController.LoseLife`에서 마지막 하트를 잃으면 준비 입력을 닫고 `WaveManager.StopWave→Wave.StopWave`를 실행한다. 전투가 정리된 후 기존 BattleLost를 보내 패배UI를 표시한다.

Wave.StopWave는 다음 순서로 처리한다.

1. IsRunning=false를 먼저 확정하고 스폰 코루틴을 중단한다. 중복 StopWave는 무실행이다.
2. 생존 적과 사망 연출 중인 적을 합쳐 추적 집합을 비우고,각 적의 Defeated/DeathCompleted 구독을 해제해 풀에 반환한다. 이 회수는 처치/탈출/웨이브 완료 이벤트를 만들지 않는다.
3. Ended를 한 번 보내 진행 중인 공격과 투사체를 정리한다. 이 신호를 처리하는 동안 새 StartWave도 차단한다.

스폰 대기에서 돌아올 때도 IsRunning을 확인한다. 늦은 처치/도착/사망 애니메이션 알림은 실행 중인 웨이브와 추적 집합에 포함된 적에만 적용한다. 적 반환 시 기존 이동 경로/도착 콜백/활성 버전 초기화는 유지한다. Wave 컴포넌트 비활성화도 StopWave를 사용한다.

## 공격과 투사체

Ally.SetTargetingWave가 명시적으로 받은 Wave.Ended를 구독한다. 종료 시 CurrentTarget을 비우고 기존 OnTargetUnavailable로 진행 중 공격을 취소해 Idle로 돌아간다. 풀 비활성화 때 구독을 해제하고 재활성화/새 Wave 연결 때 복구한다. 타격 애니메이션 이벤트의 기존 RefreshTarget 검사도 IsRunning=false에서 피해를 거부한다.

ProjectileAttack은 Ally가 받은 Wave를 함께 보관하고,종료된 웨이브에서는 Fire를 거부한다. 발사한 HomingProjectile에 같은 Wave를 전달해 Ended 시 즉시 풀로 반환하며,종료된 Wave/대상 활성 버전도 LateUpdate에서 다시 검사한다. 풀 반환 시 구독을 해제하고 반복 Finish는 무실행이다. 독립 검증 등 Wave 없이 사용하는 기존 Launch 호출은 선택 인자로 호환한다. 기존 공격 피해·속도·타격/사거리 규칙은 바꾸지 않았다.

## 정상 완료와 보상

정상 완료도 Wave.IsRunning=false 뒤 Ended를 보내 전투 효과를 정리하고,기존 Completed를 한 번 보낸다. 패배 중단은 Completed를 보내지 않는다. 치명적 탈출은 EnemyArrived 내부에서 StopWave를 실행하므로 마지막 적이어도 이후 TryCompleteWave가 통과하지 않는다.

TurnController.EndBattle은 준비 중/아직 전투 중/패배/승리/종료 정산 중의 재호출을 거부한다. endingBattle을 보상 지급 전에 설정해 재화 변경 콜백의 재입력을 막고,종료 후 해제해 준비 버튼을 갱신한다. 기존 일반 웨이브 완료60과 처치 보상은 유지하며 이번 회수로 추가 지급하지 않는다. 새 판 초기화나 유닛 삭제는 이 작업에 포함하지 않는다.

## 검증

[DefeatStopVerification.cs](../Assets/Editor/DefeatStopVerification.cs)는 실제 ThrowYut 씬 복사본에서 세 시나리오를 검사한다.

- 활성 적·사망 연출 중 적·발사된 풀 투사체·진행 중 공격·남은 생성 목록이 있는 상태에서 실제 Enemy.Update의 짧은 검증 경로 도착으로 마지막 하트를 잃는다. 모두 정리되고 패배1회/완료0회/완료 보상0회를 확인한다.
- 패배UI의 일시정지 뒤에도 시간을 다시 흐르게 해 스폰 재개·지연 피해/보상이 없음을 확인한다. 낡은 도착/타격/사망 완료·반복 중단/완료 호출도 검사한다.
- 새 씬의 마지막1기 치명적 탈출에서 패배와 정상 완료의 경계를 확인한다.
- 또 다른 새 씬의 정상2기 처치에서 완료/다음 준비1회와 기존 처치2+2/완료60을 확인한다. 지갑 Changed에서 EndBattle을 재호출해도120→184로 한 번 정산한다.

Play Mode17개 검사 통과·검사 구간 런타임 오류0. [결과](Verification/DefeatStopVerification.json). 생성 간격/하트1/공격 상태·짧은 경로/게임 시간 고정은 검증 중 메모리 설정이며 저장하지 않았다. 씬 재로드도 실제 Unity 프레임과 준비 초기화를 기다린다. 전체 빌드·패배 깃발 렌더링·보스 즉시 승패는 이번 검증 범위가 아니다.

기존HP/풀 재사용907개와 전투 시작29개 회귀 검사도 통과했고 각 검사 구간 오류0이다(`Logs/WaveHealthAfterDefeatStop.json`, `Logs/BattleStartAfterDefeatStop.json`). 전투 시작 회귀는 캐시가 새 표시 윷의 의존성을 인식하도록 YutStick.prefab의 실제 의존 에셋을 먼저 재임포트한 뒤 통과했다. 이 재실행은 DefeatStopVerification.RunBattleStartRegression을 사용한다. 원본 윷 설정을 수정하지 않았다.

검증한 런타임6개·검증 도구·씬의 SHA-256을 작업본과 대조했다. 기존 검증 복사본/캐시를 재사용하고 원본 에디터는 유지했다. 작업 시작 전의 윷 버튼 색상 처리 등 관련 없는 변경은 보존했다. 초기 에디터 검색 인덱스 예외는 검사 시작 전의 로그이며 오류0은 실제 검사 구간 기준이다.

배치 재실행은 검증 복사본에서 `-executeMethod DefeatStopVerification.ReimportAndRun -nographics`다. 결과는 Logs/DefeatStopVerification.json에 쓰고 검증 편집기를 종료하므로 원본의 열린 에디터에서 직접 실행하지 않는다.
