# 전투 효과 애니메이션

## 에셋 이름과 폴더 규칙

- 투사체는 `캐릭터명_Projectile`, 전용 명중 효과는 `캐릭터명_2nd_Evolution_Hit`, 공통 명중 효과는 `Common_Hit`으로 이름을 통일한다.
- 이미지 경로는 `Assets/Art/Effect/Projectile`과 `Assets/Art/Effect/Hit`으로 구분한다. 관련 클립과 컨트롤러는 `Assets/Animations/Effects/Projectile` 또는 `Hit`, 프리팹은 `Assets/Prefabs/Effects/Projectile` 또는 `Hit`에 둔다.
- 파일 이동 시 기존 `.meta`와 GUID, 스프라이트 내부 ID를 유지한다. 내부 에셋 이름과 문서 경로는 새 이름에 맞추되 Animator 상태 이름 `Flight`·`Hit` 및 런타임 동작은 유지한다.
- 이름 정리 이후 2차 진화 명중 효과 5개의 `.anim`과 `.controller`를 `Assets/Animations/Effects/Hit`에 추가한다. 각 스프라이트 이름의 숫자 접미사 순서로 60fps 재생하며 반복하지 않는다. 기본 Animator 상태는 `Hit`이고, 마지막 프레임이 1/60초 표시된 뒤 클립 종료 시 `OnHitAnimationFinished` 이벤트를 보낸다. 기존 `Common_Hit`은 유지한다. 이번 애니메이션 생성에서는 프리팹 생성이나 전투 연결을 추가하지 않는다.

[명세 색인](README.md) · [아군](AllySpec.md) · [적군](EnemySpec.md)

## 60fps 클립과 프리팹

2026-11-16 요청에 따라 세 스프라이트 시트를 숫자 접미사 순서로 재생한다. 재생 속도는 Animator 기본 배율 1이며, 각 스프라이트는 게임 시간 기준 1/60초 동안 표시한다.

| 효과 | 클립 | 프레임 | 재생 길이 | 반복 |
| --- | --- | ---: | ---: | --- |
| 도사 마법 | [Do_Taoist_Projectile.anim](../Assets/Animations/Effects/Projectile/Do_Taoist_Projectile.anim) | 54 | 0.9초 | 비행 중 반복 |
| 모 화살 | [Mo_Archer_Projectile.anim](../Assets/Animations/Effects/Projectile/Mo_Archer_Projectile.anim) | 18 | 0.3초 | 비행 중 반복 |
| 적 피격 | [Common_Hit.anim](../Assets/Animations/Effects/Hit/Common_Hit.anim) | 11 | 약 0.1833초 | 한 번 |
| 도사 2차 진화 명중 | [Do_Taoist_2nd_Evolution_Hit.anim](../Assets/Animations/Effects/Hit/Do_Taoist_2nd_Evolution_Hit.anim) | 22 | 약 0.3667초 | 한 번 |
| 개 전사 2차 진화 명중 | [Gae_Warrior_2nd_Evolution_Hit.anim](../Assets/Animations/Effects/Hit/Gae_Warrior_2nd_Evolution_Hit.anim) | 17 | 약 0.2833초 | 한 번 |
| 양 창병 2차 진화 명중 | [Geol_Spearman_2nd_Evolution_Hit.anim](../Assets/Animations/Effects/Hit/Geol_Spearman_2nd_Evolution_Hit.anim) | 15 | 0.25초 | 한 번 |
| 소 철퇴병 2차 진화 명중 | [Yut_Maceman_2nd_Evolution_Hit.anim](../Assets/Animations/Effects/Hit/Yut_Maceman_2nd_Evolution_Hit.anim) | 33 | 0.55초 | 한 번 |
| 말 궁수 2차 진화 명중 | [Mo_Archer_2nd_Evolution_Hit.anim](../Assets/Animations/Effects/Hit/Mo_Archer_2nd_Evolution_Hit.anim) | 8 | 약 0.1333초 | 한 번 |

- 각 클립과 같은 폴더에 같은 이름의 `.controller`가 있다. 기존 투사체 2종과 `Common_Hit`은 같은 역할의 [효과 프리팹 폴더](../Assets/Prefabs/Effects)에 있는 프리팹의 Animator와 연결되어 있다. 2차 진화 명중 효과 5종은 클립과 컨트롤러만 생성했다.
- 투사체의 Animator는 스프라이트만 변경한다. 이동·목표 추적·회전·피해 시점은 기존 코드에서 처리한다.
- `HomingProjectile`은 Animator만 사용한다. `Launch`가 호출되면 **`Flight` 상태**를 진행률 0부터 재생한다. 도사·모의 Controller가 각각 `Do_Taoist_Projectile.anim`·`Mo_Archer_Projectile.anim`을 연결하며, 투사체 컴포넌트에 별도의 클립 참조는 없다.
- `HitEffect`도 별도 클립을 참조하지 않고 **`Hit` 상태**를 재생한다. Controller가 `Common_Hit.anim`을 연결하고, 클립 종료 지점의 **`OnHitAnimationFinished` Animation Event**가 효과를 제거한다. 클립 이름과 상태 이름이 같아야 하는 가정은 사용하지 않는다.
- 런타임에는 프리팹에 저장된 에셋 참조를 사용한다. `Assets` 경로를 문자열로 읽는 Editor 전용 API는 사용하지 않으므로 빌드에서도 같은 참조로 재생한다.
- 게임 배속과 일시정지는 Animator와 효과 수명에도 적용한다.
- 도사의 비행 시간은 현재 0.3초다. 따라서 0.9초짜리 마법 클립은 비행 중 앞부분만 재생하고 도착 시 투사체와 함께 종료한다. 전체 시퀀스를 비행 시간에 강제로 압축하지 않는다. 전체를 보려면 비행 시간을 0.9초 이상으로 조정해야 한다.

## 적 피격 효과

- `Enemy.TakeDamage`가 유효한 양수 피해를 적용할 때 `Common_Hit`을 적의 현재 중심 위치에 생성한다. 일반·빠른·보스 적 프리팹에 연결한다.
- 투사체 공격은 도착 시, 개·걸·윷은 타격 이벤트 시점에 피해 처리와 함께 효과가 생성된다. 처치 타격에도 효과를 표시한다.
- 적의 이동·사망 Animator와 독립된 오브젝트로 재생하므로 기존 적 애니메이션을 중단하지 않는다. 생성된 위치에서 재생하며, 적을 따라가지는 않는다.
- [HitEffect.cs](../Assets/Scripts/Enemy/HitEffect.cs)는 클립 끝의 `OnHitAnimationFinished` 이벤트에서 효과를 파괴한다. 이벤트는 마지막 스프라이트가 처음 나타나는 프레임이 아닌 **클립 종료 시각 약 0.183333초**에 배치해 마지막 스프라이트도 1/60초 동안 표시한다. 재생 속도와 일시정지를 따라 이벤트 실행 시점도 변한다. 무효 피해와 경로 도착 자체에는 효과를 생성하지 않는다.
- 완료 여부를 매 프레임 검사하는 공통 코루틴은 제거했다. 피격 효과의 `Hit` 상태와 적의 사망 상태는 종료 이벤트까지 재생되도록 구성하며, 도중에 임의의 상태로 강제 전환하면 종료 이벤트가 실행되지 않을 수 있다.
- 효과의 표시 크기는 원본 Pixels Per Unit 설정과 기본 Transform 배율을 따른다.

## 에셋 생성

[CombatEffectBuilder.cs](../Assets/Editor/CombatEffectBuilder.cs)의 `Build`가 클립·컨트롤러·효과 프리팹을 생성한다. 에셋을 다시 구성할 때는 기존 설정을 덮어쓸 수 있으므로 명시적으로 실행한다. 자동 실행은 하지 않는다.

## 검증

2026-11-16 스크립트 MSBuild 컴파일과 별도 Unity 6000.3.28f1 프로젝트의 Play Mode 자동 검증 **146개 항목을 통과**했다. 종료 이벤트 방식으로 변경한 뒤 이벤트 위치·자식 전달·재사용된 생존 적의 잘못된 종료 알림 무시를 포함해 기존 공격·효과 검증을 다시 실행했다.

애니메이션별 60fps·길이·프레임 순서·Animator 연결·실제 재생·일시정지·반복 또는 종료를 확인했다. 적의 피해 시 효과 생성과 수명 종료도 확인했다. 실제 게임 화면에서의 크기·가시성·회전 중 외형은 별도 육안 확인 대상이다.

피격 효과의 저속 재생·가속 종료 이벤트, 일반·빠른·보스 적 각각의 좌우 사망 상태·일시정지·느린 재생 시 조기 반환 방지·종료 이벤트에 의한 반환·풀 재사용 시 체력과 걷기 상태 복구를 확인했다. `YutStick`의 직접 클립 재생 구조는 변경하지 않았다.
