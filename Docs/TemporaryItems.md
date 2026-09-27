# 임시 능력치 아이템

아이템 데이터는 `Assets/Data/Items`에 있으며 아이콘은 모두 None입니다.

| 에셋 | 이름 | 효과 |
| --- | --- | --- |
| Item_Experience50 | 학습 노트 | 획득 경험치 +50% |
| Item_AttackSpeed20 | 가벼운 장갑 | 공격속도 +20% |
| Item_AttackPower2 | 숫돌 | 공격력 +2 |
| Item_Luck3 | 행운의 동전 | 행운 +3 |
| Item_ProjectileSpeed01 | 가속 깃털 | 탄속 배율 +0.1 |

탄속은 기존 능력치인 ProjectileSpeedMultiplier를 사용하므로 기본 배율 1.0이 1.1이 됩니다. 퍼센트 효과는 BasePercent에 합산됩니다. 공격속도 아이템 두 개는 기본 공격속도 대비 +40%입니다. 경험치 아이템은 ExperienceGainRate를 변경하며, 생존 런의 실제 경험치 수집에 반영됩니다.

## Unity에서 시험하기

1. Gameplay 장면의 Player를 선택합니다. PlayerItemInventory가 추가되어 있습니다.
2. 실행 전 Starting Items 목록에 원하는 에셋을 넣으면 시작 시 획득합니다. 기본 목록은 비어 있습니다.
3. 실행 중 Debug Item에 에셋을 지정한 뒤 컴포넌트의 메뉴에서 `Debug/Add Selected Item (Play Mode)`를 실행하면 하나 획득합니다.
4. `Debug/Remove Selected Item (Play Mode)`는 해당 아이템 한 개를 제거합니다.

코드에서는 `PlayerItemInventory.AddItem(item)`과 `RemoveItem(item)`을 사용합니다. 플레이어 초기화 및 Start 이후 호출하며, 성공 여부를 bool로 반환합니다. 같은 에셋을 반복 획득하면 중첩됩니다. 비활성화 시 효과를 회수하고 다시 활성화하면 보유 수량대로 복원합니다. 생존 런에서는 경험치 조각과 보물을 수집한 뒤 보상 선택으로 획득합니다.

새 능력치 아이템은 `Create > Game > Items > Stat Item`으로 만들고 Modifiers에 능력치, 계산 종류, 수치를 지정합니다. 퍼센트 수치는 50, 20처럼 입력합니다.

검증: `ROTLINE > Tests > Temporary Items`는 다섯 에셋의 빈 아이콘, 수치 적용, 중첩과 제거를 확인합니다. Gameplay의 실행 검증은 CombatPlayModeChecks에 포함됩니다.
