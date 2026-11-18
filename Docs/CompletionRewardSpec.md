# 일반 웨이브 완료 보상 (2026-11-18)

작업22는 지정 원문13/14장과 ImplementationGaps의 일반 웨이브 생존 완료 보상을 따른다.1~9웨이브는 적이 일부 탈출해도 하트가 남고 모든 생성/잔여 적 처리가 끝나면 일반재화60을1회 지급한다.9회 기본 합계는540이다. 처치 보상과 별도이며10웨이브에 기본60을 추가하지 않는다. 천명전의60→120은 작업39의 후속 범위다.

## 현재 연결과 변경

기존 `Wave.TryCompleteWave→WaveManager.WaveCompleted→TurnController.EndBattle` 경로를 유지했다. EndBattle은 실행 중/준비 중/승패/정산 중 재입력을 거부하고,중간 웨이브의 completionReward를 StageCurrency.Grant에 전달한 뒤 EndTurn/BeginPreparation으로 넘어간다. WaveCleared에는 완료한1기준 웨이브 번호와 실제 지급액을 보낸다.

Wave1~9의 completionReward=60은 이미 설정돼 있었다. Wave10은 최종 웨이브 분기에서 완료 보상을 지급하지 않지만,Inspector의 값도 이 정책과 일치하도록60→0으로 명시했다. 작업22에서 런타임 코드를 다시 수정하지 않았다. 최종 보스 처치100은 작업21의 별도 수입이고 처치 즉시 최종 승리는 [작업23](BossOutcomeSpec.md)에서 연결했다.

하트0은 Wave.StopWave로 종료하고 Completed를 보내지 않으므로 정산하지 않는다. Wave.IsRunning=false와 TurnController.endingBattle/preparing 보호로 완료 이벤트·지갑 변경 콜백에서 같은 정산을 반복하지 않는다. 다음 준비 단계에서 일반/특별 잔액을 초기화하지 않는다. 두루마리 연출 자체는 작업43에서 별도로 확인한다.

## 작업22 검증

Unity6000.3.28f1 실제 ThrowYut 씬 복사본에서 **88개 검사 통과·검사 구간 런타임 오류0**. [결과](Verification/CompletionRewardVerification.json)·[검증 도구](../Assets/Editor/CompletionRewardVerification.cs).

- 실제 등록된1~9웨이브의 편성/HP를 사용해 전투를 연속 실행했다. 매번 첫 적1기는 실제 Enemy.Update의 짧은 검증 경로를 완주해 탈출하고 나머지를 치명 피해/사망 완료로 처리했다. 자연 전투의 클리어 난이도를 검증한 것은 아니다.
- 각 웨이브에서 처치 수=전체-1,탈출1,완료60,준비 전환1회를 확인했다. 하트10→1로 감소해도 보상은 지급됐다.9회 완료 합계540,193기 처치386,시작120으로9웨이브 후 잔액1046이다. 특별재화7도 전부 이월됐다.
- 모든 지갑 변경 콜백에서 EndBattle/StartBattle을 재호출했지만 완료/보상/준비 전환은 각1회였다. 준비 상태에서 완료를 다시 호출해도 추가 지급하지 않았다. WaveCleared의 번호/실제 지급액60도 확인했다.
- 10웨이브의 실제HP6000 보스 처치100으로 최종 잔액1146,일반 완료 보상 합계540,WaveCleared9회를 확인했다. 작업22 당시에는 사망 완료 기반 승리였고,작업23 이후 보스 처치 즉시 승리 경로에서도 같은 금액/횟수로 회귀 통과했다. 즉시 승패 자체의 검증은 [작업23](BossOutcomeSpec.md)에 기록했다.

검사 중 생성 간격·시간 고정·특별재화·짧은 탈출 경로만 메모리에서 바꾸고 저장하지 않았다. 탈출 적의 속도/통과 시간 필드는 반환 직후 원래 값으로 복원해 풀 재사용에 검증 설정이 남지 않게 했다. 기존 하트0 패배 중단17개 회귀도 통과·검사 구간 오류0이다(`Logs/DefeatStopAfterCompletionRewards.json`).

검증한 코드·씬·10개 웨이브 데이터·검증 도구의 SHA-256이 작업본과 일치한다. 기존 검증 복사본/캐시를 재사용하고 원본 에디터는 유지했다. 전체 빌드·두루마리 렌더링·아이템 배율은 이번 범위가 아니다. 초기 검색 인덱스 예외는 검사 시작 전이며 위 오류0은 검사 구간 기준이다.

배치 재실행은 별도 복사본에서 `-executeMethod CompletionRewardVerification.ReimportAndRun -nographics`를 사용한다. Logs/CompletionRewardVerification.json에 결과를 쓰고 검증 편집기를 종료하므로 원본의 열린 에디터에서 직접 실행하지 않는다.
