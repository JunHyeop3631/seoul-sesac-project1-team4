# 윷 결과·던지기·소환·이동 배치 — 2026-11-19

## 요청과 기준

이전 YutThrowV2Test 데모/GIF의 버튼 크기와 상대 배치를 현재 게임에 적용한다. 윷 던지기 모션이나 확률/소환/이동 규칙은 이번에 바꾸지 않는다.

## 화면 구성

Canvas 1920×1080 기준 오른쪽 두루마리의 기존 중심 앵커 X=.8515625를 유지한다.

- 윷 던지기: 460×208, 아래 기준 Y=410. 기존 비용 없는 버튼 배경과 눌림 이미지를 유지하고 실제 비용을 하단에 표시한다. 기본20, 천운부 적용16이다. 데모의 임시284 이미지는 가져오지 않는다.
- 윷 결과: 350×100, 아래 기준 Y=535. 예전 데모의 글자별 타이트한 스프라이트를 원본 비율로 표시한다.
- 소환/이동: 각230×164, 중심 X는 오른쪽 패널 중심에서 -122/+122, 아래 기준 Y=377이다. 같은 줄·크기·간격으로 배치한다. 기존 상점/인벤토리와 전투 시작 위치는 유지한다.
- 데모에서 승인한 완성형 소환/이동 카드 원본을 재사용한다. 따로 조립했던 이름·이동 숫자·소환 하단 문구 자식을 제거해 중복 표시나 잘못된 줄바꿈을 없앤다. 도/개/걸/윷/모/빽도 각각 정상·눌림 스프라이트를 연결한다. 빽도는 기존대로 소환 불가다.
- 기본/눌림은 동일 UI 사각형과 동일 크기의 스프라이트 영역을 사용한다. 옛 눌림 시트가1px 더 높아 생기는 차이는 슬라이스 크기를 기준 시트와 맞춰 처리한다. PNG 픽셀은 변경하지 않는다.

## 구현

YutActionButtonVisuals는 결과에 맞는 완성 카드와 해당 눌림 이미지만 교체한다. TurnController가 기존처럼 활성/비활성·재화·결과 소비·행동 처리를 소유한다. 각 Button과 기존 클릭/사운드 연결을 유지한다.

YutActionLayoutSetup.Apply가 원본 시트를 Sprite Editor Data Provider로 분할하고 현재 ThrowYut 씬에 배치한다. Regions.json에는 옛 YutImageArt.asset의 승인 UV가 들어 있다. 원본은 Assets/Art/Button/YutApprovedCards에 복사해 독립적으로 동작한다. 기존 UIRefinementSetup도 새 Configure를 사용한다.

모션·전투·사운드·상점·새 인벤토리 배치는 변경하지 않는다. Git 커밋·업로드는 하지 않는다.

## 검증

현재 프로젝트를 동기화한 Unity 6000.3.28f1 검증 사본의 실제 ThrowYut Play Mode에서6가지 카드/결과, 눌림 이미지 실제 전환, 빽도 차단, Esc 메뉴·상점 품절 유지를 검사했다. 실제 비용20/16·천운부·이동 취소·인벤토리 선택/사용 회귀도 통과했다. 결과는 Docs/Verification의 UIRefinement/HeavenlyLuck/CancelAction/InventoryVerification.json에 있다.

화면: [던지기](Previews/YutActionLayout/ThrowReady.png) · [개 결과](Previews/YutActionLayout/ActionResult1.png) · [모 결과](Previews/YutActionLayout/ActionResult4.png) · [눌림](Previews/YutActionLayout/ActionPressed.png).
