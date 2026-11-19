# 아이템 아이콘 Single Sprite 전환

- 대상: Assets/Art/Item/Item의 Item_Dokkaebi_Lucky_Pouch, Item_Dokkaebi_Yut, Item_Gumiho_Heart, Item_Heavenly_Luck_Talisman, Item_Heavenly_Mandate_Coin, Item_Soul_Crystal PNG 6개.
- Sprite Mode를 Single로 전환하고 기존 GUID와 원본 PNG를 보존한다.
- 변경 전에 씬 Image와 ShopItemDefinition.icon의 기존 이미지 경로를 기록하고, 재임포트 후 실제 Single Sprite 객체로 다시 연결한다. 예전 Multiple fileID는 남기지 않는다.
- 인벤토리는 ShopItemDefinition.Icon을 읽으므로 같은 아이템 정의를 통해 연결을 유지한다. 아이템 기능과 UI 위치/크기는 수정하지 않는다.

검증: Unity가 6개 Single Sprite(fileID 21300000)를 모두 로드했고, 씬 Image 7개와 아이템 정의 5개의 연결을 복원·저장했다. Assets 아래 해당 GUID 참조를 검색해 예전 Multiple fileID가 남지 않았음을 확인했다. Play Mode에서 상점의 활성 아이템 5종과 영혼의 결정 상세 아이콘 표시를 확인했다. 구미호의 심장 행은 기존 비활성을 유지하며 해당 Sprite 로드와 저장된 참조만 확인했다. 임시 변환 스크립트는 제거했다.
