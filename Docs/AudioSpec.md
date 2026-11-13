# 오디오 구조 설계

[명세 색인](README.md) · [윷 던지기](YutThrowSpec.md) · [윷 행동](YutActionSpec.md) · [아군](AllySpec.md) · [웨이브](WaveSpec.md)

## 결정과 설계 상태

싱글톤을 사용하지 않는 것은 사용자가 확정한 조건이다. 아래의 컴포넌트 구성과 연결 방식은 이번 요청에 따라 작성한 설계안이다. 클래스명과 메서드명은 구현 시 사용할 제안 이름이다.

공통 BGM·효과음은 씬의 `AudioManager`가 재생하고, 유닛 전용 소리는 해당 유닛의 `UnitAudio`가 재생한다. 각 재생 장치의 출력은 하나의 `AudioMixer`에 연결해 종류별 볼륨을 함께 조절한다.

## 씬과 에셋 구성

```text
게임 씬
├── AudioManager
│   ├── AudioManager 컴포넌트
│   ├── BGM용 AudioSource → GameAudioMixer / BGM
│   └── 공통 효과음용 AudioSource → GameAudioMixer / SFX
├── 윷·행동·턴 컨트롤러와 UI
│   └── 필요한 곳에 AudioManager 참조 연결
├── 유닛 인스턴스
│   ├── Ally 또는 Enemy
│   ├── UnitAudio 컴포넌트
│   └── 유닛 효과음용 AudioSource → GameAudioMixer / SFX
└── Main Camera
    └── AudioListener

공유 에셋
├── GameAudioMixer
│   └── Master
│       ├── BGM
│       └── SFX
└── AudioClip 파일들
```

각 게임 씬에는 공통 오디오를 담당하는 `AudioManager`를 하나 배치하고 Inspector에서 참조를 연결한다. `AudioManager`는 일반 `MonoBehaviour`로 구성한다. 유닛 프리팹에는 자신의 `UnitAudio`, `AudioSource`, 클립을 연결하고 Mixer 그룹은 공유 에셋을 참조한다.

활성 게임 씬의 `AudioListener`는 하나를 사용한다. 씬 전환 중 BGM 유지가 필요해지면 씬 수명과 오디오 수명을 함께 설계한다.

## 구성 요소별 책임

| 구성 요소 | 책임 | 보관할 참조와 데이터 |
| --- | --- | --- |
| 게임 컨트롤러 | 행동 성공과 상태 전환을 판단하고 공통 소리 재생 요청 | 필요한 `AudioManager` 참조 |
| `AudioManager` | BGM 시작·교체·정지, 클릭·윷·소환 등 공통 효과음 재생 | BGM·효과음용 `AudioSource`, 공통 클립 |
| `UnitAudio` | 해당 유닛의 공격·피격 등 전용 효과음 재생 | 같은 유닛의 `AudioSource`, 전용 클립 |
| `AudioSource` | 지정된 클립 재생, 개별 음량과 공간 설정 적용 | 출력할 Mixer 그룹 |
| `GameAudioMixer` | 전체·BGM·효과음 볼륨 적용 | `Master`, `BGM`, `SFX` 그룹 |
| 설정 UI | 사용자의 볼륨 조절을 Mixer에 반영 | Mixer 참조와 노출된 볼륨 파라미터 |

설정 UI가 추가되면 Inspector로 Mixer를 연결한다. 볼륨 값 저장과 복원은 설정 기능을 구현할 때 연결한다.

## 참조와 호출 흐름

공통 소리가 필요한 컨트롤러는 `[SerializeField]` 필드로 `AudioManager`를 참조한다. 유닛은 자신의 `UnitAudio`를 참조한다. 유닛의 공격음은 `AudioManager`를 거치지 않고 자신의 `AudioSource`에서 재생하며, 공통 효과음과 같은 `SFX` 그룹으로 출력한다.

```text
소환 성공
YutActionController → AudioManager.PlaySummon()
                   → 공통 효과음용 AudioSource → SFX → Master

아군 공격 실행
Ally → UnitAudio.PlayAttack()
     → 유닛의 AudioSource → SFX → Master

전투 시작 확정
턴·웨이브 진행 코드 → AudioManager.PlayBgm(clip)
                  → BGM용 AudioSource → BGM → Master
```

`AudioManager`가 게임의 진행 조건을 다시 판정하지 않도록 한다. 호출하는 게임 로직에서 실행 여부를 판단한 뒤 해당 소리를 요청한다. 동일한 사건의 재생 요청은 한 곳에서 담당한다.

## 클립 보관과 재생 시점

| 소리 | 클립을 연결할 위치 | 재생 요청 시점 |
| --- | --- | --- |
| 버튼 클릭 | `AudioManager` | 버튼 입력이 수락됐을 때 |
| 윷 던지기 | `AudioManager` | 실제 던지기가 시작됐을 때 |
| 도·개·걸·윷·모 | `AudioManager`의 결과별 클립 필드 | 결과가 표시됐을 때 |
| 소환·배치 | `AudioManager` | 실제 소환 또는 이동이 성공했을 때 |
| 칸 정원 초과 알림 | `AudioManager` | 정원 때문에 행동이 거절됐을 때 |
| 유닛 공격·피격 | 각 유닛 프리팹의 `UnitAudio` | 실제 공격·피격 처리 시점 |
| BGM | `AudioManager`의 클립 필드. 웨이브별 선택이 필요하면 `WaveData` 참조 검토 | 전투 등 해당 상태 진입이 확정됐을 때 |
| 승리·패배 | `AudioManager` | 게임 전체의 승패가 확정됐을 때 |

현재 아군 공격은 `Ally.LateUpdate`에서 즉시 피해를 적용한다. 공격음을 연결할 때는 실제 공격이 수행되는 지점에서 한 번 요청한다. 향후 애니메이션으로 공격 시점을 옮기면 소리 요청도 그 시점에 맞춘다.

## 재생과 볼륨 처리

- BGM은 전용 `AudioSource`에 클립을 지정해 재생한다. 반복 여부는 곡의 용도에 맞춰 설정한다. 같은 곡의 반복 요청으로 재생 위치가 계속 초기화되지 않도록 처리한다.
- 짧은 효과음은 `PlayOneShot`으로 재생한다. 유닛마다 `AudioSource`가 있으므로 여러 유닛이 동시에 소리를 낼 수 있다.
- 개별 소리의 상대 음량은 클립별 재생 설정 또는 해당 `AudioSource`에서 맞춘다. 사용자의 전체·배경음·효과음 볼륨은 Mixer 그룹에서 적용한다.
- 유닛을 비활성화하거나 풀에 반환할 때는 해당 유닛의 재생 중인 소리를 정지한다. 풀 재사용 시 이전 재생 상태가 이어지지 않도록 처리한다.

## 구현 전에 정할 세부 항목

- 같은 윷 결과의 여러 음원 중 선택 방식.
- 준비·전투·승패 상태별 BGM과 전환 방식.
- 유닛 소리의 2D·3D 설정 및 거리 감쇠 사용 여부.
- 공격음이 많이 겹칠 때 동시 재생 제한 적용 여부와 기준.
- 일시정지 중 BGM·효과음의 처리, 볼륨 기본값과 저장 방식.

## 현재 구현 상태

`Assets/SoundEffects`에 음원 파일이 있다. 문서 작성 시 `Assets/Scripts`에서 `AudioSource`, `AudioClip`, `AudioMixer` 사용 코드는 확인되지 않았다. 이 문서는 구현 전 구조를 기록한다.
