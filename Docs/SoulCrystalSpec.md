# 영혼의 결정 효과 — 작업35

준비 중 보유한 영혼의 결정을 사용하면1개를 소모하고 특별재화5를 지급한다. 일반재화는 변하지 않는다. 재사용·전투·던지기·일시정지·승패·닫힌 창의 늦은 입력은 무소비/무지급이다. 구매 완료 이력은 유지한다.

기존 StageItemUseController에 명시적 참조를 가진 SoulCrystalEffect를 등록한다. 재화 알림 시점에도 아이템이 이미 제거된 상태가 보이도록, 사용 실행기는 보유를 알림 없이 먼저 회수하고 효과 실패 시 복원하며 성공 후 보유 변경을 알린다. 처리 중 재입력 잠금을 유지한다. 정수 상한에서5를 전부 지급할 수 없으면 소비하지 않는다.

특별재화 상단 HUD와 특별소환 창의 잔액은 기존 SpecialChanged 구독을 통해 같은 지갑 값으로 갱신한다. 소환창이 닫혀 있을 때의 내부 표시 값과 재열기 때의 실제 표시도 확인한다. 다른 아이템 효과36~39는 이번 범위에 추가하지 않는다.

## 실행 흐름

1. 인벤토리에서 영혼의 결정을 선택하고 사용 버튼을 누르면 InventoryUI.UseSelected→StageItemUseController.TryUse로 진행한다. 기존 준비/던지기/전투/일시정지/승패/보유 검사를 사용한다.
2. [SoulCrystalEffect](../Assets/Scripts/Turn/SoulCrystalEffect.cs)는 OnEnable에서 명시적 참조인 사용 실행기에 등록하고 OnDisable에서 해제한다. CanApply는 재화 컴포넌트 활성과5 전액 수용 여부를 검사한다. 외부에서 효과만 직접 호출해 지급하는 우회와 지급 콜백의 재호출을 막는다.
3. [StageItemUseController](../Assets/Scripts/Turn/StageItemUseController.cs)는 IsUsing을 잠그고 StageInventory.TryTake로 보유를 알림 없이 회수한다. 효과는 StageCurrency.GrantSpecial(5)를 호출한다. 특별재화 알림을 받은 관찰자는 이미 소비된 보유와 증가한 잔액을 본다. 성공 후 보유 변경을 알리며 인벤토리 선택/슬롯을 갱신한다. 실패는 보유를 복원하고 잠금을 해제한다.
4. StageCurrency.SpecialChanged→SpecialCurrency.Changed→SpecialSummonUI.RefreshBalance의 기존 경로에서 상단 hudDigits와 소환창 ownedDigits를 함께 갱신한다. 일반재화는 건드리지 않는다. 구매 완료 이력도 지우지 않는다.
5. [SoulCrystalBuilder](../Assets/Editor/SoulCrystalBuilder.cs)는 실제 씬의 Turn 오브젝트에 효과를 추가하고 사용 실행기·재화를 연결한다. 저장 후 씬을 다시 열어 연결을 확인한다. 싱글톤이나 별도 보상 지갑을 추가하지 않는다.

## 검증

[실제 씬 Play Mode70개 검사 통과·검사 구간 오류0](Verification/SoulCrystalVerification.json). [검증 코드](../Assets/Editor/SoulCrystalVerification.cs).

- 실제 상품 구매→인벤토리 포인터 선택→활성화된 사용 버튼 포인터 클릭으로 특별재화0→5/25→30/45→50을 확인했다. 각각1개 소비·정확히1회 지급·일반재화 유지·품절 유지·보유 슬롯/선택 갱신을 검사했다.
- 특별재화 알림 때 보유가 이미 제거되고 두 잔액 표시도 갱신돼 있음을 확인했다. 동일 사용/직접 효과/구매/일반·특별 소환/윷/전투의 콜백 재입력을 차단했다.
- 상단 HUD와 닫힌 소환창 내부 표시·소환창 재열기 표시가 일치하며25→30에서 실제 소환 버튼이 활성화된다.
- 닫힌 창의 늦은 입력·일시정지·던지기·승패·비활성 효과/재화/보유/실행기·실제 전투·정수 상한에서 무소비/무지급을 확인했다. 일부 상태는 메모리 fixture다.
- 실제 다음 웨이브 준비에서도 보유 아이템을 사용할 수 있고 기존 확정 윷 결과를 바꾸지 않는다. 새 씬은 일반120/특별0/보유0으로 초기화한다.

1920×1080 렌더링: [사용 가능 상태](Previews/SoulCrystalReady.png)·[사용 후 두 잔액5](Previews/SoulCrystalGranted.png). 화면의 일반재화50은 테스트용200에서 상품 가격150을 지불한 결과다. 사용 후 화면의 입력 차단 안내는 검증 콜백에서 전투 시작 재입력을 시도해 생긴 것이다. 테스트 보유/재화/웨이브는 저장하지 않았다. 전체 빌드·수동 완주는 수행하지 않았다.

Game 뷰에서는 준비 중 영혼의 결정을 구매하고 인벤토리에서 선택해 사용한다. 보유가 사라지고 특별재화가5 증가해야 하며 소환창을 열어 같은 잔액을 확인한다. 다시 눌러도 추가 지급이 없어야 한다. 실제 할인과 천운부 적용중 표시는 다음 작업36이다.

기존 인벤토리86개·특별소환 창43개·구매117개를 재실행해 총246개 통과·검사 구간 오류0을 확인했다. [회귀 결과](Verification/SoulCrystalRegressionVerification.json). 인벤토리 회귀는 실제 영혼의 결정 효과를 비활성화한 뒤 테스트 효과로 일반 실행기의 실패 복원/성공 소비를 별도로 검사한다. 실제 효과는70개 본 검사에서 확인했다.
