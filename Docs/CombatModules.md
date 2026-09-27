# 조합 가능한 전투 모듈

아이템·드롭·인벤토리 콘텐츠와 독립된 전투 확장 기반이다. 아이템 시스템은 나중에 효과 출처와 중첩 수만 전달하면 된다. 이미 있던 예제 아이템은 호환용으로 유지하며 이번 변경에서는 새 아이템을 만들지 않았다.

## 전체 흐름

```text
무기 Recipe + 효과 출처별 Modifier
    → 일반 변경을 Priority / Id 순서로 적용
    → 특별 조합 Rule을 Priority / Id 순서로 적용
    → 최종 Recipe 검증 및 복사
    → TriggerRuntime이 발동 요청
    → EmissionDefinition이 발사 일정 생성
    → AttackExecutionRuntime이 공격체 생성·취소·연쇄 예산 관리
    → Shape + TrajectoryRuntime + HitPolicyRuntime
    → 피해량 변경 → 실제 피해 → OnHit → 처치 시 OnKill
```

새 동작은 아래 추상 클래스를 구현하고 에셋으로 연결한다. 전투 컨트롤러에 아이템 이름별 분기를 추가하지 않는다.

| 확장점 | 계약 | 상태의 소유자 |
|---|---|---|
| 발동 | AttackTriggerDefinition → AttackTriggerRuntime | 장착한 무기 실행 인스턴스 |
| 방출 | AttackEmissionDefinition.Build | 발동마다 만드는 발사 일정 |
| 형상 | AttackShapeDefinition.BuildPoints | 공격체가 복사한 점 목록 |
| 이동 | AttackTrajectoryDefinition.CreateRuntime → AttackTrajectoryRuntime | 개별 공격체 |
| 명중 | AttackHitPolicyDefinition → AttackHitPolicyRuntime | 개별 공격체/근접 공격 |
| 효과 | CombatEffectDefinition → CombatEffectRuntime | 캐릭터의 CombatEffectStateStore |
| 시너지 | AttackCompositionRule.Apply | 구성 해석 단계, 지속 실행 상태 없음 |

Definition 에셋은 설정만 담는다. 충전량, 현재 위치, 명중 기록, 발동 횟수, 쿨타임을 공유 에셋 필드에 기록하지 않는다.

## 바로 구성할 수 있는 동작

Unity의 `Create > Game > Combat`에서 모듈 에셋을 만들고 무기 Recipe에 연결한다.

| 동작 | 구성 |
|---|---|
| 충전 레이저 | ChargeRelease + Beam + Attached + repeatHitSeconds |
| 유지형 레이저 | Channel + Beam + Attached + repeatHitSeconds |
| 시간차 다중 탄 | Burst Emission + Projectile + Linear |
| 충전 고리 | ChargeRelease + Ring + Linear + radiusScalesWithCharge |
| 유도 고리 | Ring + Homing |
| 공전 후 직진 | 원하는 형상 + Orbit Then Linear |
| 절차적 근접 공격 | Beam + Melee Swing, Lifetime과 Swing Duration을 맞춤 |
| 근접 시간차 연타 | 위 근접 구성 + Burst Emission |
| 영역에 다시 들어오면 피해 | 원하는 형상 + Hit On Reentry |

기본 방출은 단발/부채꼴/360도 방사형이다. Burst 모듈은 이 방출을 시간차로 반복한다. Channel은 일반 수명을 넘어 유지하며 Held 입력으로 조준을 갱신하고 Released/Cancel로 종료한다. 여러 갈래를 조준할 때 각 갈래의 각도 차이를 유지한다.

공속은 발사 회복 간격, chargeSeconds는 충전 시간, lifetime은 공격체 수명이다. 서로 독립적이다. 공속 회복이 끝나도 이전 발사 일정이나 공격체는 살아 있을 수 있다.

## 효과를 추가하는 방법

`CombatEffectDefinition.CreateRuntime()`에서 캐릭터별 런타임을 반환하고 필요한 훅만 구현한다.

- `OnAttack`: 한 번의 발동. 다중 발사의 탄 수와 무관하게 한 번 호출한다. 기존 검은 활성 구간 진입 시 호출한다.
- `OnEmit`: 실제 공격체마다 호출한다. 분열로 만들어진 공격체에도 호출한다.
- `ModifyDamage`: 명중 직전 피해량 변경. 유효한 양수 피해만 EnemyActor가 수용한다.
- `OnHit`: 실제 적용 피해가 양수인 경우. `Result.AppliedDamage`를 기준으로 흡혈량 등을 계산할 수 있다.
- `OnKill`: 위 명중으로 죽은 경우. 일반 OnHit 다음에 호출한다.
- `OnExpire`: 자연 수명 종료. 취소·모드 전환·무기 교체에는 호출하지 않는다. 첫 명중으로 소멸하는 탄의 적중 효과는 OnHit에 연결한다.

실행 중 정보는 `CombatEvent`에 들어 있다. Owner, Attack, Target, TargetVersion, Position, 실제 DamageResult, AttackId, RootAttackId, ChainDepth, Time을 제공한다. 적은 사망 콜백에서 풀로 돌아갈 수 있으므로 처치 여부와 피해량은 Target의 현재 상태 대신 Result를 읽고, 위치는 캡처된 Position을 사용한다.

`Spawn On Combat Event`는 공용 효과 모듈이다. Attack/Hit/Kill/Expire 중 하나를 선택하고 Every Nth, Cooldown, Damage Scale, Child Recipe를 지정한다. ‘세 번째 공격마다 추가 탄’, ‘처치 시 추가 공격’, ‘수명 종료 시 분열’을 같은 모듈로 표현한다. 흡혈 대상인 플레이어 체력 시스템이나 실제 아이템 콘텐츠는 포함하지 않는다.

### 자식 공격

훅 안에서 `context.SpawnChild(childRecipe, direction, damageScale)`을 호출한다. 호출 지점의 Position에서 **한 공격체**를 예약한다. Child Recipe의 Shape/Trajectory/HitPolicy/Effects를 사용하며, 자식의 Emission 일정까지 재실행하지는 않는다. 여러 자식은 여러 번 요청한다.

- 충돌 콜백 안에서 자식 충돌을 바로 재귀 실행하지 않는다. 실행 큐로 전달한다.
- 자식 피해 = 부모 공격 피해 × 요청 damageScale × Child Recipe.damageMultiplier.
- 루트 Recipe의 Max Chain Depth / Max Child Attacks를 후손 전체가 공유한다. 자식 설정을 크게 바꿔도 루트 예산을 우회하지 않는다.
- 공격체마다 AttackId가 다르고 모든 후손은 같은 RootAttackId를 유지한다.
- 무기 실행 인스턴스의 활성 공격체 + 대기 일정 상한은 256이다. 발사 일정도 1~256개로 제한한다.
- 거부되면 false를 반환한다. 취소된 공격이나 훅 밖에 보관해 둔 이벤트로 새 자식을 생성할 수 없다.
- Cancel은 대기 일정과 모든 후손을 함께 제거한다. 취소를 자연 종료로 처리해 추가 분열을 발동하지 않는다.

## 효과 상태와 구성 변경

같은 캐릭터에서 동일한 효과 Definition은 하나의 상태를 공유한다. 다른 캐릭터는 별도 상태를 갖는다. 따라서 다중 탄마다 카운터가 초기화되지 않고, 무기/효과 출처 변경으로 Runtime을 다시 만들어도 카운터와 쿨타임이 유지된다. 효과를 제거하면 새 공격의 목록에서 빠지며, 다시 장착하면 그 캐릭터에 보관된 상태를 이어받는다.

현재 PlayerCombatController의 비활성화/파괴는 상태 저장소까지 정리한다. 모드 전환은 공격만 취소하고 카운터는 유지한다. 장비 구성 변경 시 기존 공격과 자식을 취소한 뒤 새 Recipe를 만들며 공격 회복 시간을 보존한다. 이미 날아간 탄을 유지하는 정책은 아직 사용하지 않는다.

효과 목록은 Recipe.Copy에서 독립적으로 복사한다. 중복된 같은 Definition은 한 번만 바인딩된다. 같은 종류의 효과를 중첩해 강화하려면 Modifier에서 수치를 합성하거나 별도 효과 설정 에셋을 사용한다. Definition 자체를 게임 도중 수정하지 않는다.

## 특별 조합

기본 Modifier를 적용한 뒤 WeaponAttackDefinition의 Composition Rules를 적용한다. `Required Sources Override`는 필요한 출처 Id가 모두 존재할 때 Trigger/Emission/Shape/Trajectory/HitPolicy를 교체한다.

예를 들어 ‘빔 출처 + 공전 출처’가 함께 있을 때 **Wrapped Beam 형상 + Attached 경로**로 바꾸는 Rule을 만들 수 있다. 단순히 빔을 공전 이동시키는 것과 구분해서 표현할 수 있다. 출처 하나를 제거하면 처음부터 재계산하므로 그 시너지가 사라진다. 특수한 조합 계산은 AttackCompositionRule 하위 클래스로 확장한다.

룰은 한 번씩만 적용하며 정렬은 Priority → ordinal Id다. 중복 Id를 거부하고 고정점 반복을 하지 않아 룰 간 무한 재평가를 막는다. 출처가 아이템인지, 캐릭터 능력인지, 임시 버프인지는 전투 실행기가 구분할 필요가 없다.

## 기존 코드와의 경계

기존 검 프리팹은 ComposedWeaponAttackRuntime 어댑터로 유지한다. 입력 모듈, 명중 정책, 공격/명중/처치 효과, 자식 공격 실행기를 공유하므로 기존 모션을 다시 제작하지 않아도 효과를 붙일 수 있다.

옛 Collider 프리팹을 여러 개 예약 생성하는 기능은 이 어댑터에 넣지 않았다. Emission 모듈을 사용하려면 Shape가 있는 구성으로 옮긴다. Beam + Swing은 근접 공격도 같은 모듈 실행기에서 처리할 수 있음을 보여준다. 어댑터에 Emission을 지정하면 구성 오류를 반환한다. 새 콘텐츠는 Shape 기반 Recipe를 사용하는 것이 기본이다.

기존 `AttackEffectDefinition.OnAttack` / 검기 투사체는 호환 경로로 남긴다. 새로운 상태형 효과는 CombatEffectDefinition으로 작성한다. 새 프로시저 시스템을 기존 검기 내부에 자동으로 주입하지 않는다.

## 검증 및 한계

`ROTLINE > Tests > Combat Modules`에서 사용자 정의 Trigger, 시간차 방출/취소, 유지형 빔, 캐릭터별 상태와 재장착, 연쇄 예산/깊이, 명중 정보, 조건부 조합 복원, 재진입 판정, 유도 이동 상태, N회 발동, 자연 종료/취소 차이, 절차적 근접 연타를 검사한다. 기존 34개 전투 검사와 Gameplay Play Mode 검사도 유지한다.

현재 형상은 점/두께 있는 선분으로 표현하며 고리는 다각선 근사다. 임의 메시지나 노드 그래프를 실행하는 엔진이 아니다. 시간차 발사는 큰 프레임에서도 누락하지 않지만 움직이는 대상의 과거 위치를 복원하거나 모든 물리 상호작용을 프레임 독립적으로 재현하는 시뮬레이터는 아니다. 충돌은 샘플링하고 형상 512점·프레임당 샘플 수 제한을 둔다. 대규모 탄막 성능 최적화, 풀링, 최종 아트 및 게임 내 아이템 UI는 별도 작업이다.

## 자동 실행과 차지 입력

PlayerCombatController는 일반 공격을 현재 조준 방향으로 자동 반복합니다. Automatic, Press, Channel은 버튼을 누를 필요가 없고, ChargeRelease만 좌클릭으로 충전하고 놓아서 발사합니다. 새 차지 트리거는 AttackTriggerRuntime.RequiresChargeInput을 true로 재정의해야 합니다. 무기나 공격 아이템으로 트리거가 바뀌면 새 런타임의 입력 정책이 적용됩니다. 정화는 정화 모드에서 조준한 칸에 자동으로 적용됩니다.
