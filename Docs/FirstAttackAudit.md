# 작업17 첫 공격 준비 상태 대조 (2026-11-18)

판정: **매 전투의 첫 공격 준비 완료는 보장되지 않는다. GAP-028은 미해결로 유지한다.** 작업17의 대조·검증은 완료했으며 런타임 공격 코드·프리팹·씬은 수정하지 않았다.

## 기준과 관찰 경로

작업12에서 다시 내보낸 사용자 지정 Google Docs 탭4의10장 기준은 각 전투 시작 시 첫 공격이 준비 완료 상태이고 첫 유효 표적 진입 시 동작을 시작하는 것이다. 같은 장의 개발 완료 전달 및 추가 설계/신규 구현 요청 중단 조건에 따라 차이를 확인·기록하는 범위다.

`AllyPool.Request→Ally.ResetForReuse`는 nextAttackTime=0으로 초기화한다. 실제 표적을 얻으면 `Ally.LateUpdate→UpdateAttack`이 공격 모션을 시작하고 nextAttackTime을 현재 시각+공격 간격으로 설정한다. 웨이브 종료의 `Ended→ClearCombatTarget→OnTargetUnavailable`는 표적/진행 공격을 정리하지만 nextAttackTime을 초기화하지 않는다. `TurnController.EndBattle→BeginPreparation→StartBattle`도 유지된 아군의 공격 기한을 다시 설정하지 않는다.

따라서 짧은 준비 후 바로 유효 표적이 있으면 이전 기한까지 기다리고,준비 중 기한이 자연히 지나면 첫 공격을 바로 시작한다. 매 웨이브에 항상 지연된다는 뜻은 아니다. 이 보고서의 첫 공격은 **공격 모션 시작**이며 피해/발사 시각이 아니다.

## 실행 결과

Unity6000.3.28f1 ThrowYut 씬 복사본에서 관찰 검사74개 통과·검사 구간 런타임 오류0. [결과 JSON](Verification/FirstAttackVerification.json)은 `checksPassed=true`와 `requirementSatisfied=false`를 구분한다.

| 프리팹 종류 | 새 풀 요청 후 시작 | 짧은 준비 후 다음 전투 시작 지연 | 충분한 준비 후 시작 |
| --- | ---: | ---: | ---: |
| 도사1·2성 | 0.01초 | 약0.84초 | 0.01초 |
| 전사1·2성 | 0.01초 | 약0.67초 | 0.01초 |
| 창병1·2성 | 0.01초 | 약1.26초 | 0.01초 |
| 철퇴병1·2성 | 0.01초 | 약2.01초 | 0.01초 |
| 궁수1·2성 | 0.01초 | 약0.34초 | 0.01초 |

위10개 비교는 공격 모션이 시작된 직후 마지막 적에게 검증용 치명 피해를 주고 사망 완료를 직접 알린 조기 전환 배치다. 각 유닛의 이전 공격 기한과 다음 실제 모션 시작 시각이 일치하는지를 확인했다. 새 소환·진화 재료 초기화에 문제가 있다는 판정은 아니다.

별도 철퇴병1성 사례는 마지막 적에게 검증용 치명 피해를 주고 **실제 사망 애니메이션 완료 이벤트**를 기다렸다. 전투 종료 시 이전 기한까지 약1.48초가 남았고 바로 다음 전투에서 모션 시작까지 약1.49초 기다렸다. 즉 사망 연출 대기만으로 항상 이전 간격이 만료되는 것도 아니다. 이 경우도 마지막 적 피해는 테스트에서 주었으며 자동 전투의 자연 처치 빈도를 측정한 것은 아니다.

## 검증 방법과 한계

[FirstAttackVerification.cs](../Assets/Editor/FirstAttackVerification.cs)는 실제10개 프리팹·AllyPool·Animator·Wave/TurnController를 사용한다. 마지막 적 처리 후 실제 EndBattle/다음 준비/StartBattle 경로를 따라가고 attackStartFrame/nextAttackTime의 변화를 관찰한다. 테스트에서 공격 이벤트나 UpdateAttack을 직접 호출하거나 nextAttackTime 값을 덮어쓰지 않는다.

검사 중에만 단일 고HP 적 데이터32개를 등록하고 아군을 입구 근처에 배치해 즉시 유효 표적이 있도록 했다. 적은 생성 후 이동을 정지하며 Time.captureDeltaTime=.01로 게임 시간을 고정한다. 수치는 게임 시간이며 벽시계 측정이나 일반 플레이의 발생 빈도가 아니다. 준비를 이전 기한 이후까지 기다린 대조 사례도 포함한다. 실행 중 설정은 저장하지 않는다.

검증한 Ally/AllyPool/TurnController/Wave·10개 프리팹·씬·검증 도구의 SHA-256을 작업본과 대조했다. 기존 검증 복사본/캐시를 재사용하고 원본 에디터는 유지했다. 전체 빌드·모션 렌더링·공격 DPS/피해량은 이번 검증 범위가 아니다. 초기 에디터 검색 인덱스 예외는 검사 시작 전의 로그이며 오류0은 검사 구간 기준이다.

배치 재실행은 별도 복사본에서 `-executeMethod FirstAttackVerification.ReimportAndRun -nographics`를 사용한다. Logs/FirstAttackVerification.json에 결과를 쓰고 검증 편집기를 종료하므로 사용 중인 원본 에디터에서 직접 실행하지 않는다.
