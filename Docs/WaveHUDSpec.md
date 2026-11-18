# 웨이브·하트·적 정보 HUD

작업24 / 2026-11-19. 원문18장의 웨이브·남은 적·보스 정보·현재/최대 하트 표시를 연결한다.

- 웨이브는 현재 번호/전체10으로 표시한다. 예: `1 / 10`, `10 / 10`.
- 사용자 확정: 남은 적은 **등장 예정 + 생존 적 + 탈출한 적**이다. 따라서 `전체 - 처치`를 사용한다. 탈출은 하트를 감소시키지만 이 숫자를 줄이지 않는다. 생존 적만 나타내는 수치가 아니며 전투 완료 여부는 기존 Wave의 생성 종료/생존 적 상태로 판정한다.
- 하트는 `현재 / 최대10`이다. 일반 적 탈출은 현재값만1 감소하고, 다음 웨이브에도 이월한다. 보스 탈출은 기존 작업23 규칙대로 잔여 하트 유지/즉시 패배다.
- 보스 편성이 있는10웨이브에서는 두 번째 줄에 `보스 현재HP / 최대HP`를 표시한다. 준비 중에는 데이터의6000/6000, 전투 중에는 실제 BossEnemy.CurrentHealth, 처치 후에는0/6000을 표시한다. 탈출 후에는 마지막 HP를 유지한다.

## 실행 흐름과 파일

1. [TurnController](../Assets/Scripts/Turn/TurnController.cs)의 기존 RemainingLives와 추가 MaxLives가 하트의 현재/최대값을 제공한다. [HeartHUD.LateUpdate](../Assets/Scripts/UI/HeartHUD.cs)는 둘 중 하나가 바뀌면 텍스트를 갱신한다.
2. [Wave](../Assets/Scripts/Wave/Wave.cs)는 생성한 실제 BossEnemy를 Boss로 보관한다. Prepare/StartWave에서 비우고 StopWave 이후에는 최종HP 표시를 위해 참조를 유지한다. 객체의 비활성화는 기존 풀 수명주기를 따른다.
3. [WaveManager](../Assets/Scripts/Wave/WaveManager.cs)는 현재 Boss와 편성 데이터의 GetBossMaxHealth를 제공한다. [WaveHUD.LateUpdate](../Assets/Scripts/UI/WaveHUD.cs)는 현재 웨이브 번호/전체, 전체-처치, 보스HP를 구성하고 표시 문자열이 바뀔 때만 UI에 쓴다.
4. [ThrowYut 씬](../Assets/Scenes/ThrowYut.unity)의 기존 배경 위치·크기는 유지하고 긴 숫자와 보스 두 줄이 들어가도록 텍스트 영역을 조정한다. 남은 적/보스에는 기존 YiSunShin-Bold 한글 폰트를 사용한다. 하트의 별도×오브젝트를 숨기고 현재/최대 숫자를 배치한다.

## 검증

[WaveHUDVerification](../Assets/Editor/WaveHUDVerification.cs)이 실제 ThrowYut 씬의 복사본을 Play Mode로 열어 검사한다. 등장 예정11/생존1에서12, 일반 적 탈출 후12와하트9, 각 처치 감소, 다음 준비에서 새 전체/처치 초기화, 하트0, 보스 준비6000·피해5875·처치0·탈출5875와 남은 수 유지까지 확인한다. 도착 검사는 실제 적에 등록된 도착 콜백을 호출해 HUD와 전투 이벤트 연결을 검사하며 이동 경로 자체의 재검증은 아니다.

Unity6000.3.28f1에서74개 검사 통과·검사 구간 런타임 오류0. 실행 결과는 [JSON](Verification/WaveHUDVerification.json), 실제1920×1080 렌더링은 [일반 준비](Previews/HUDPreparation.png)·[보스 준비](Previews/HUDBossPreparation.png)·[보스 피해](Previews/HUDBossDamage.png)다. 세 렌더링에서 숫자와 한글, 보스 두 줄의 배경 내 배치를 확인했다. 검증용 시간/편성 상태는 메모리에서만 변경한다. 전체 플레이 수동 완주와 빌드 실행은 이번 범위에 포함하지 않는다.

Unity Game 뷰에서1웨이브의 `1 / 10`, `남은 적 12`, `10 / 10`과10웨이브의 `10 / 10`, `남은 적 1`, `보스 6000 / 6000`을 확인할 수 있다.
