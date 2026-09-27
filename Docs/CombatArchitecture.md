# 공격 구성 개편

기존 검 에셋과 좌우 교대 휘두르기를 유지하면서, 공격 구성 변경을 추가 효과 실행과 분리했다.

추가된 독립 발동·방출·명중 모듈, 상태형 효과, 특별 조합 룰, 연쇄 제한은 [전투 모듈 확장 안내](CombatModules.md)를 참고한다. 아래 예제 에셋은 그대로 호환된다.

## 실행 흐름

`무기 기본 Recipe + 보유 AttackRecipeModifier → Resolve → 무기 Runtime → 공격체별 Runtime → DamageContext / DamageResult`

- `WeaponAttackDefinition`의 Recipe에 Shape가 없으면 기존 근접 Runtime을 사용한다. 기존 프리팹을 다시 연결할 필요가 없다.
- Shape가 있으면 근접 Collider 프리팹 없이 모듈 공격을 실행한다. Shape와 Trajectory를 별도로 선택한다.
- 입력은 Started / Held / Released / Cancel을 구분한다. Automatic은 누르는 동안 연사, Press는 누를 때만, ChargeRelease는 충전 후 놓을 때 발사하고, Channel은 놓을 때까지 유지한다. 새 발동 방식은 Trigger 모듈로 확장한다.
- 충전 시간은 Recipe의 `chargeSeconds`, 발사 후 다음 공격까지의 간격은 `1 / AttackSpeed`, 공격체 수명은 `lifetime`이다. 공속이 바뀌어도 충전 시간과 공격체 수명은 자동으로 바뀌지 않는다.
- 공격 시작 때 피해량·사거리·투사체 속도 배율을, 충전 발사 때 충전량을 확정한다. 충전 중 스탯 변경은 발사 시점에 반영한다.
- `AttackActiveStarted` 추가 효과 목록은 입력을 시작한 시점의 목록을 사용한다. 공유 ScriptableObject에는 공격 횟수·쿨타임 같은 실행 상태를 저장하지 않는다.

## 에셋으로 시험하기

`Assets/Data/CombatExamples`에 다음 예제가 있다. Gameplay의 Player / PlayerCombatController에서 Equipped Weapon에 지정하고 Play한다.

| 에셋 | 동작 |
|---|---|
| Weapon_Projectile | 누르는 동안 직선 탄 발사, 첫 적중 후 소멸 |
| Weapon_ChargedBeam | 충전 후 놓으면 0.5초 유지되는 빔, 0.1초 간격 피해 |
| Weapon_ChargedRing | 충전량에 비례하는 고리가 직선으로 이동, 내부는 빈 판정 |
| Weapon_WrappedBeam | 몸 주변을 감싸는 선과 전방 직선이 동시에 존재하는 빔 |

기존 검을 장착한 상태에서 Starting Attack Items에 `Item_ChargedBeam`, `Item_ChargedRing`, `Item_Orbit`, `Item_Multishot`을 넣어도 시험할 수 있다. 추가 획득과 제거는 `EquipAttackItem` / `UnequipAttackItem`, 무기 교체는 `EquipWeapon`을 사용한다. 생존 런의 보물 드롭과 선택 화면에서도 공격 변형을 획득할 수 있다. 세부 흐름은 `SurvivalRun.md`를 참고한다.

예제는 `ROTLINE > Combat > Create Example Assets`로 누락된 것만 다시 만들 수 있다. 이미 존재하는 에셋은 덮어쓰지 않는다. 시각 효과는 판정 확인용 LineRenderer이며 최종 아트가 아니다.

## 아이템 조합 규칙

1. 낮은 Priority부터 적용하고 같은 Priority는 고유 Id의 ordinal 순서로 적용한다. 동일 항목을 덮어쓰는 변환은 나중에 적용된 것이 이긴다. Id가 비거나 중복되면 거부한다.
2. 같은 아이템은 Maximum Stacks까지 중첩한다. 추가 발사 수·각도는 합산하고 피해·반지름 배율은 중첩 횟수만큼 곱한다. 수치 상한은 Recipe에서 검사한다.
3. 아이템 제거 때 원래 무기부터 다시 계산하므로 이전 구성이 복원된다. 빔 100, 고리 200, 공전 300, 다중 발사 400이 예제 우선순위다.
4. 장비 구성이 바뀌면 진행 중인 충전·공격·소유 공격체를 취소하고 새 구성을 장착한다. 기존 공격 쿨타임은 유지한다. 날아가던 탄을 실시간 변형하거나 유지하는 정책은 이번 구현에 포함하지 않았다.
5. Shape가 없는 기존 근접 공격에는 피해 배율과 Press/Automatic 발동을 적용한다. 발사 수·경로·반지름 변경은 Shape를 갖춘 모듈 공격에서 사용한다.

## 판정과 피해

- 형상의 같은 점 목록을 화면 표시와 판정에 사용한다. 빔·고리는 선분을 따라 두께가 있는 판정을 하고, 기본 투사체는 원형 판정이다.
- 반복 간격 0은 공격체당 한 번, 양수는 해당 간격의 반복 피해다. 한 적의 여러 Collider는 중복 피해를 주지 않는다. 풀에서 재생성된 적은 SpawnVersion으로 구별한다.
- Unlimited Targets가 꺼져 있으면 Pierce Count가 허용하는 추가 대상까지 맞힌 후 소멸한다. 0은 첫 대상에서 소멸한다.
- 프레임이 수명을 넘어도 마지막 이동 구간을 검사한다. 임의 곡선에 대한 완전한 연속 충돌 해석은 아니며, 모듈 공격의 샘플 수는 프레임당 2048로 제한한다. 극단적인 속도·가는 판정·큰 프레임 지연과 대규모 탄막은 추가 검증·프로파일링이 필요하다. 기본 고리는 64개 선분으로 근사한다.
- 피해 결과는 요청 피해량이 아니라 실제 감소한 체력과 처치 여부를 반환한다. `EnemyActor.Damaged`에서 피해 주체·방향·결과를 받을 수 있다. 새 CombatEffectRuntime은 공격·명중·처치·수명 종료에 연결되며 자식 공격의 깊이와 총생성 수를 제한한다. 치명타 추첨과 흡혈 자체의 게임 규칙은 별도 효과로 구현한다.

## 수명 관리와 기타 수정

- 모드 전환, 전투 컴포넌트 비활성화, 장비 교체에서 충전·공격체·기존 검기 효과를 정리한다. 생존 런의 보상 선택·일시정지·사망·종료는 `CancelCombat()`으로 같은 정리 경로를 호출한다.
- 정화 사거리는 최종 PurificationRange, 정화 후 간격은 제거 타일 수 / PurificationPower를 사용한다. 최소 1타일분 간격을 적용한다. PurificationEfficiency는 원래 의미가 미정이므로 새 공식을 임의로 부여하지 않았다.
- 기존 특수 게이지는 MaxResource 변경을 즉시 반영한다. 현재 절대량을 유지하고, 최대치가 줄면 제한한다. 0 최대치를 허용하며 증가만으로 자동 충전하지 않는다. 애초 게이지가 없는 캐릭터에 게이지 능력을 새로 부여하는 기능은 별도다.
- 적은 비활성 상태로 풀에서 꺼내 위치와 데이터를 지정한 후 활성화한다. 이동 가능한 스폰 위치를 제한 횟수만큼 찾고, 실패하면 해당 스폰을 건너뛴다. 스폰 담당자 비활성화 시 활성 적을 반환한다.
- AppRoot가 씬의 자식이어도 씬 전환 때 유지되도록 루트로 분리한다. 빌드 시작 씬은 삭제된 SampleScene 대신 Gameplay로 수정했다.
- 여러 적 종류의 스폰 테이블, 투사체 풀링, 오염 상태/표시 분리, 오염까지 포함한 라운드 재시작은 이번 공격 개편과 별도의 후속 작업이다.

## 검증

- `ROTLINE > Tests > Combat and Lifecycle`: 조합 순서·중첩·제거, 충전/취소, 수명 분리, 고리 내부, 중복 Collider, 실제 피해량과 사망 재진입, 가변 프레임 반복 피해, 재진입 시 과거 피해 방지, 기존 검, 버서커 사거리, 게이지 범위.
- 기존 Player Stats / Modifier Scope / Modifier Collection 회귀 검사.
- `CombatPlayModeChecks.RunBatch`: 실제 Gameplay에서 이벤트 구독 해제/재구독, 입력 이벤트에서 충전/발사까지 전달, 모드 전환 정리, 아이템 제거 후 검 복원, 게이지 스탯 연동, 풀 재사용을 검사한다. 헤드리스 자동 검사이며 수동 조작감·렌더링 검증을 대체하지 않는다.
