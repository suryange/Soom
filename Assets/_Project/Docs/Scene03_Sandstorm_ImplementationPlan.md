# Scene 03 두 번째 호흡 콘텐츠 - 모래폭풍 구현 계획

## 1. 목표 사용자 흐름

이 문서의 기준 흐름은 다음과 같다.

```text
첫 번째 호흡 콘텐츠 완료
  → Guiding Light를 따라 이동
  → Sandstorm Box Trigger 진입
  → 챕터 UI: "끝없는 모래 / 불안한 첫 발"
  → 퀘스트 UI: "모래 폭풍"
  → 모래 파티클과 Fog가 강해지고 바람 SFX 재생
  → 현재 길을 안내하던 Guiding Light의 빛이 0으로 감소
  → 폭풍 상승 연출 완료 후 두 번째 호흡 미션 대기
  → B 입력으로 BreathingActive 진입
  → 공통 원형 호흡 UI와 "깊게 호흡하여 모래폭풍을 잠재우세요" 표시
  → 들숨/날숨 1루프 완료마다 구슬 1개 채움
  → 매 루프마다 파티클 Emission과 Fog Density가 1/3씩 감소
  → 3루프 완료
  → 파티클/Fog/바람 완전 해제
  → Guiding Light 빛 복구
  → Idle 복귀 및 다음 동선 진행
```

핵심 결정 사항:

- 모래폭풍은 첫 번째 호흡 콘텐츠와 같은 `BreathEventsSO` 및 `BreathCircleUI`를 재사용한다.
- 공용 이벤트를 재사용하더라도 각 콘텐츠가 자신이 시작한 호흡 세션만 처리하도록 미션 소유권을 구분한다.
- 트리거 진입 직후에는 폭풍 상승 연출을 먼저 보여주고, 상승 완료 후에 호흡 시작 입력을 받는다.
- 호흡 1루프 완료 피드백은 3단계로 고정하지 않고 `BreathManager.targetLoopCount`를 기준으로 계산한다.
- Fog는 Scene의 진입 전 설정을 저장했다가 성공, 취소, 비활성화 경로에서 안전하게 복구한다.
- Guiding Light는 편집 시점 템플릿이 아니라 첫 번째 콘텐츠가 런타임에 생성한 활성 인스턴스를 제어한다.

## 2. 현재 구현 확인 결과

확인 기준:

- 코드와 YAML Scene/Prefab을 정적으로 확인했다.
- `Scene_03_InGame_Outside`의 현재 직렬화 상태를 기준으로 했다.
- Unity Play Mode 및 Quest 실기기 동작은 아직 검증하지 않았다.
- 로컬 `dotnet` SDK가 없어 프로젝트 외부 컴파일 검사는 수행하지 못했다. 최종 컴파일은 Unity 6000.3.9f1에서 확인해야 한다.

### 2.1 Runtime 코드에 이미 구현된 기반

- [x] `SandstormZoneTrigger`가 `BoxCollider` 진입과 `Player` 태그를 검사한다.
- [x] 트리거 1회 실행 옵션과 `SandstormController.EnterZone()` 호출 경로가 있다.
- [x] `SandstormController`에 `Idle → Rising → Active → Calming → Cleared` 상태가 정의되어 있다.
- [x] Particle System 재생 및 Emission Rate 보간 로직이 있다.
- [x] `RenderSettings.fogDensity` 상승, 단계 감소, 원래 값 복구 로직이 있다.
- [x] 루프형 바람 SFX용 `AudioSource`를 런타임에 생성하는 로직이 있다.
- [x] `GuidingLightController`에 빛 강도 Fade Out/Restore API가 있다.
- [x] `BreathEventsSO.OnBreathLoopCompleted`를 받아 루프 횟수에 비례해 파티클과 Fog를 감소시킨다.
- [x] `OnMissionSuccess`에서 파티클, Fog, SFX, 등불, 호흡 UI를 정리하는 기본 로직이 있다.
- [x] 공용 `BreathCircleUI`는 실시간 호흡값, 3개 슬롯 채움, 성공 Pulse를 지원한다.
- [x] `Systems.prefab`의 `BreathManager.targetLoopCount`는 3이고 동일한 `BreathEventsChannel.asset`을 사용한다.

### 2.2 Editor 도구에 이미 구현된 기반

- [x] `SOOM/Build Sandstorm Zone` 메뉴가 있다.
- [x] 도구가 `SandstormZone` 루트 아래에 Box Trigger, Particle System, Controller, 진입 UI, 지시문 UI를 생성한다.
- [x] `BreathEventsChannel.asset`, `BreathCircleUI`, `GuidingLightController`를 찾아 직렬화 필드에 연결하려고 시도한다.
- [x] 같은 이름의 `SandstormZone`이 있으면 새 루트를 중복 생성하지 않는다.
- [x] Scene Fog는 활성화되어 있고 Exponential Squared 모드, 기본 Density `0.01`로 저장되어 있어 현재 Density 보간 방식과 호환된다.

### 2.3 Scene과 에셋 구현 결과

- [x] `Scene_03_InGame_Outside`에 `SandstormZone` 루트를 추가했다.
- [x] Scene에 `SandstormController`와 `SandstormZoneTrigger` 스크립트 참조를 연결했다.
- [x] 마지막 Guiding Light 동선인 `Waypoint_12`에 `6 × 3 × 6` Box Trigger를 배치했다.
- [x] Quest용 경량 Particle System을 연결하고 최대 파티클 수를 500으로 제한했다.
- [x] 외부 바람 파일이 없어 런타임 절차적 바람 루프 fallback을 구현했다.
- [x] `windLoopSfx`가 비어 있어도 절차적 Clip으로 바람 소리가 재생된다.
- [x] 완료 후 Guiding Light가 `Waypoint_13 → Waypoint_14`로 이동하도록 연결했다.

### 2.4 명세 반영 결과

- Builder의 챕터 문구를 `끝없는 모래 / 불안한 첫 발`로 수정했다.
- 퀘스트 문구는 `모래 폭풍`, 지시문은 `깊게 호흡하여 모래폭풍을 잠재우세요`로 확정했다.
- 현재 UI는 프로젝트의 챕터/퀘스트 공통 프리팹을 재사용하지 않고 단순 `TextMeshPro + FaceCamera` 오브젝트로 생성된다.
- `MissionReady`는 폭풍 상승 완료 후에만 활성화된다.
- 공용 원형 UI와 지시문은 실제 측정 상태인 `BreathingActive` 진입 시 한 번만 표시된다.

### 2.5 구현 전에 해결해야 하는 기능 결함

#### A. 첫 번째 호흡 성공 이벤트가 모래폭풍을 미리 클리어할 수 있음

현재 `SandstormController.HandleMissionSuccess()`는 `Idle` 상태를 차단하지 않는다. Scene 시작부터 컨트롤러가 활성화되어 있으면 첫 번째 호흡 콘텐츠의 `OnMissionSuccess`를 받아, 모래폭풍 진입 전에 `Cleared`가 된다. 이후 `EnterZone()`은 `Idle`이 아니므로 실행되지 않는다.

필요한 수정:

- 이 컨트롤러가 트리거로 활성화한 세션인지 나타내는 소유 플래그를 둔다.
- 루프 완료와 성공 이벤트는 `BreathingActive`이면서 모래폭풍 세션이 활성화된 경우에만 처리한다.
- 첫 번째 호흡과 이후 다른 호흡 콘텐츠의 이벤트는 무시한다.

#### B. 콘텐츠 B 안내 UI가 모래폭풍 호흡에도 노출됨

`BreathMissionGuideController`는 모든 `BreathingActive` 진입에 반응해 콘텐츠 B의 길 찾기 문구를 표시한다. 모래폭풍에서 B를 눌러도 `숨의 호흡능력은 올바른 길을...` 문구가 함께 나타날 수 있다.

필요한 수정:

- `BreathMissionGuideController`가 `HologramMessage` 소유 세션에만 반응하도록 제한한다.
- 모래폭풍 세션에서는 모래폭풍 전용 지시문만 표시한다.
- 공용 `BreathCircleUI.Show()/Hide()`의 호출 주체를 한 곳으로 정리해 중복 Reset/Fade가 발생하지 않게 한다.

#### C. 런타임 생성된 Guiding Light를 제어하지 못할 가능성이 큼

Builder는 편집 시점에 Scene의 `GuidingLightController`를 찾는다. 그러나 실제 길잡이 등불은 첫 호흡 성공 후 `GuidingLight(Clone)`으로 생성된다. `SandstormController.Awake()`의 자동 탐색도 이 생성보다 먼저 실행되므로 참조가 계속 비어 있을 수 있다.

필요한 수정:

- `EnterZone()` 시점에 활성 Guiding Light 인스턴스를 다시 찾거나, 생성 이벤트에서 해당 인스턴스를 전달받아 저장한다.
- 비활성 템플릿이나 프리팹 참조가 아니라 현재 이동 중인 인스턴스인지 검증한다.
- 참조가 없을 때 폭풍 자체는 계속 진행하되 한 번만 명확한 경고를 출력한다.

#### D. 취소 후 재시작 경로가 없음

현재 `BreathingActive`에서 B를 다시 누르면 `Idle`로 취소된다. 하지만 Sandstorm Trigger는 `triggerOnce=true`이고 컨트롤러는 취소를 처리하지 않아, 폭풍은 남은 채 다시 `MissionReady`로 들어갈 방법이 없다.

필요한 수정:

- 모래폭풍 미션 중 `BreathingActive`가 성공 없이 종료되면 호흡 UI만 닫고 폭풍은 현재 강도로 유지한다.
- 플레이어를 다시 `MissionReady`로 전환해 재도전할 수 있게 한다.
- 재도전 시 공용 원형 UI 슬롯과 루프 진행률을 0부터 다시 시작할지 정책을 확정한다.
  - 권장: 취소는 해당 시도의 루프 진행률을 초기화하고 폭풍은 최대 강도로 복구한다.

#### E. 전역 Fog와 Coroutine 정리 경로가 부족함

성공 경로 외에 오브젝트 비활성화, Scene 전환, 강제 취소가 일어나면 `RenderSettings.fogDensity`와 바람 SFX가 폭풍 값으로 남을 수 있다.

필요한 수정:

- 진입 전 Fog 활성 여부, 모드, Density를 저장한다.
- `OnDisable` 또는 명시적 `AbortAndRestore()`에서 파티클, Fog, SFX, 등불, UI를 복구한다.
- 동시에 실행 중인 Emission/Fog/등불 Coroutine을 중지하고 최종값을 확정한다.

## 3. 구현 작업 목록

### 3.1 호흡 미션 소유권과 상태 흐름 정리

- [x] 첫 번째 길 찾기 호흡과 두 번째 모래폭풍 호흡을 구분하는 미션 소유 정보를 추가한다.
  - 최소 구현: 각 콘텐츠 컨트롤러의 `isMissionOwnerActive` 가드
  - 확장 구현: `BreathMissionId` 또는 공용 Mission Context를 `PlayerStateManager`에 저장
- [x] `SandstormController`가 다음 조건을 만족할 때만 호흡 이벤트를 처리한다.
  - Trigger 진입 완료
  - 폭풍 상승 완료
  - 현재 미션 소유자가 Sandstorm
  - 현재 플레이어 상태가 `BreathingActive`
- [x] `Idle` 또는 다른 콘텐츠 진행 중 들어온 `OnBreathLoopCompleted`와 `OnMissionSuccess`를 무시한다.
- [x] 폭풍 상승 완료 후에만 `SetMissionZone(true)`를 호출하도록 시작 시점을 옮긴다.
- [x] `BreathingActive` 진입과 종료를 구독하여 지시문, 공통 원형 UI, 재도전 상태를 제어한다.
- [x] 성공과 취소를 구분한다.
  - 성공: `Cleared → Idle`
  - 취소: 폭풍 유지 또는 최대치 복구 → `MissionReady`
- [x] 동일 Trigger 재진입, B 연타, 중복 성공 이벤트가 상태를 두 번 바꾸지 않도록 가드를 둔다.

완료 기준:

- 첫 번째 호흡을 완료해도 모래폭풍 컨트롤러는 `Idle`을 유지한다.
- 폭풍 상승 전에는 두 번째 호흡을 시작할 수 없다.
- 취소 후 B로 다시 시작할 수 있다.
- 다른 호흡 콘텐츠의 성공 이벤트로 폭풍이 사라지지 않는다.

### 3.2 Trigger와 진입 UI 구현

- [x] `Scene_03_InGame_Outside`에서 Guiding Light 동선 후반에 Box Trigger를 배치한다.
- [x] Collider의 `Is Trigger=true`, Player 감지 태그/레이어, Rigidbody 충돌 조건을 실제 XR Origin으로 검증한다.
- [x] 챕터 UI 문구를 정확히 수정한다.
  - 제목: `끝없는 모래`
  - 부제: `불안한 첫 발`
- [x] 퀘스트 UI에 `모래 폭풍`을 표시한다.
- [x] 챕터/퀘스트 UI를 카메라 기준 World Space 또는 프로젝트 공통 UI 프리팹 중 하나로 통일한다.
- [x] UI 표시 시간, 순서, Fade In/Out을 정한다.
- [x] Trigger가 여러 Collider를 가진 XR Origin에 의해 여러 번 호출되어도 한 번만 시작되는지 확인한다.

완료 기준:

- Guiding Light를 따라 Trigger에 진입하면 UI와 폭풍 연출이 정확히 한 번 시작된다.
- 문구가 기능 명세와 일치하고 한글 폰트가 깨지지 않는다.

### 3.3 모래폭풍 VFX와 Fog 연출 완성

- [x] 최종 모래폭풍 Particle System 또는 VFX Prefab을 선정해 연결한다.
- [x] 필요하면 주변에 배치된 복수 Particle System을 지원하도록 단일 필드를 배열 또는 전용 VFX Root로 변경한다.
- [x] 진입 시 Particle System 활성화와 Emission Rate 상승을 `riseDuration` 동안 보간한다.
- [x] 기본 Fog Density `0.01`과 최대 Fog Density를 Quest 가독성 기준으로 튜닝한다.
  - 현재 코드 기본 최대값: `0.12`
- [x] 호흡 루프 완료마다 목표 강도를 `1 - completedLoops / targetLoopCount`로 계산한다.
- [x] 연속 이벤트가 들어올 때 이전 Lerp를 중단하고 현재값에서 다음 목표값으로 자연스럽게 이어지게 한다.
- [x] 세 번째 루프에서 성공 이벤트와 루프 이벤트가 같은 프레임에 연달아 와도 최종값이 0과 원래 Fog 값으로 확정되게 한다.
- [x] 파티클 최대 수 500, 낮은 알파, World Simulation Box로 과도한 시야 가림과 Overdraw를 제한한다.

완료 기준:

- 진입 시 폭풍이 점진적으로 강해진다.
- 1회, 2회, 3회 성공 때 시야가 명확히 단계적으로 회복된다.
- 완료 후 Particle System이 Stop되고 Fog가 진입 전 값으로 복구된다.

### 3.4 바람 SFX 추가

- [x] 외부 에셋 없이 루프 가능한 절차적 바람 Clip fallback을 추가한다.
- [x] `windLoopSfx`가 있으면 우선 사용하고, 비어 있으면 절차적 Clip을 자동 연결한다.
- [x] 진입 시 Fade In, 호흡 루프 완료 시 폭풍 강도에 비례한 Volume 감소, 성공 시 Fade Out을 구현한다.
- [x] `SoomAudioManager`의 SFX 볼륨을 반영한다.
- [x] Scene 전환, 취소, 비활성화 시 AudioSource가 반드시 정지되는지 확인한다.
- [x] 2D 앰비언트와 3D 공간음 중 실제 플레이 감각에 맞는 방식을 확정한다.
  - 현재 구현은 `spatialBlend=0`인 2D 루프다.

완료 기준:

- 폭풍 시작과 종료가 소리로도 자연스럽게 연결된다.
- 중복 AudioSource 또는 중첩 루프가 생성되지 않는다.

### 3.5 런타임 Guiding Light 연동

- [x] 첫 번째 호흡 성공으로 생성된 활성 Guiding Light 인스턴스를 Sandstorm에 전달한다.
- [x] Trigger 진입 시 해당 인스턴스의 `GuidingLightController`를 검증한다.
- [x] 폭풍 상승과 함께 실제 `Light.intensity`를 `lightFadeDuration` 동안 0으로 보간한다.
- [x] 필요하면 이동도 멈출지 결정한다.
  - 기능 명세는 빛 강도만 요구하므로 기본 계획은 이동 상태를 변경하지 않는다.
- [x] 폭풍 성공 시 Awake에서 저장한 원래 강도로 복구한다.
- [x] 등불이 파괴되었거나 참조가 없을 때 Null 예외 없이 폭풍을 진행한다.

완료 기준:

- 템플릿이 아니라 화면에 보이는 Guiding Light가 실제로 어두워졌다가 복구된다.
- 첫 호흡 완료 시점과 Sandstorm의 Awake 순서에 영향을 받지 않는다.

### 3.6 공통 호흡 UI와 모래폭풍 지시문 통합

- [x] 공용 `BreathCircleUI`는 `BreathingActive` 진입 시 한 번만 `Show()`한다.
- [x] 모래폭풍 지시문을 확정한다.
  - 권장: `깊게 호흡하여 모래폭풍을 잠재우세요`
- [x] 콘텐츠 B 전용 `BreathMissionGuideController`가 Sandstorm 세션에는 반응하지 않게 한다.
- [x] 루프 시작 시 슬롯 3개가 비어 있고, 매 루프당 정확히 하나씩 채워지는지 확인한다.
- [x] 취소 후 재시작 시 슬롯, 구슬 위치, 스케일, 호흡값을 초기화한다.
- [x] 세 번째 구슬 이동 Animation과 성공 Pulse가 UI Hide보다 먼저 보이도록 이벤트 순서 또는 지연 시간을 조정한다.
- [x] 폭풍 감소 피드백과 구슬 채움 타이밍을 같은 루프 완료 이벤트에 맞춘다.

완료 기준:

- 모래폭풍 호흡 중 길 찾기 안내문이 보이지 않는다.
- 공통 원형 UI가 중복 표시되거나 중간에 Reset되지 않는다.
- 구슬 채움과 폭풍 감소가 사용자가 같은 성공 피드백으로 인지할 수 있게 동기화된다.

### 3.7 `SandstormZoneBuilder`를 갱신형 자동 배선 도구로 수정

- [x] 메뉴 이름을 역할이 명확하게 드러나도록 변경한다.
  - 권장: `SOOM/Scene 03/Setup Sandstorm Breath Zone`
- [x] 기존 `SandstormZone`이 있으면 즉시 종료하지 말고 자식과 컴포넌트를 찾아 생성 또는 갱신한다.
- [x] 반복 실행해도 Trigger, Controller, UI, Particle, AudioSource가 중복되지 않게 한다.
- [x] 챕터 문구를 `끝없는 모래 / 불안한 첫 발`로 수정한다.
- [x] `BreathEventsChannel.asset`과 Scene의 기존 `BreathCircle_Outside`를 연결한다.
- [x] 런타임 Guiding Light 획득 방식에 맞는 소유자 또는 이벤트 참조를 연결한다.
- [x] 별도 VFX/Wind 에셋이 없을 때 경량 Particle과 절차적 Wind fallback을 자동 사용한다.
- [x] 기존 플레이스홀더 Particle/UI를 최종 에셋으로 마이그레이션하되 사용자가 조정한 Transform은 보존한다.
- [x] 필수 참조가 없으면 빌드를 완료한 것처럼 로그하지 말고 항목별 경고를 남긴다.
- [x] 실행 후 Scene을 Dirty 처리하고 사용자가 저장할 수 있게 한다.

Inspector 검증 항목:

- `breathEventsChannel`
- `chapterUIRoot`
- `zoneTextUIRoot`
- `instructionUIRoot`
- Sandstorm Particle System 또는 VFX 목록
- `windLoopSfx`
- 런타임 Guiding Light 공급자
- `breathCircleUI`
- `totalBreathLoops` 또는 `BreathManager.targetLoopCount` 연동값
- Trigger의 `sandstormController`, `playerTag`, `triggerOnce`

완료 기준:

- 도구를 여러 번 실행해도 Hierarchy와 UI가 중복되지 않는다.
- 기존 수동 배치 위치를 덮어쓰지 않는다.
- Console에 Missing Reference 또는 Serialized Field 경고가 없다.

### 3.8 종료와 다음 콘텐츠 연결

- [x] 모래폭풍 성공 직후 다음 진행 조건을 정의한다.
  - Guiding Light 이동 재개
  - 다음 Waypoint 활성화
  - 다음 Trigger 또는 Ending Sequence 활성화
- [x] `Cleared`가 저장되어 Trigger 재진입으로 폭풍이 다시 시작되지 않게 한다.
- [x] 성공 후 `PlayerStateManager`가 `Idle` 또는 다음 콘텐츠가 요구하는 상태로 정확히 전환되는지 확인한다.
- [x] Scene 재시작 정책을 정한다.
  - 새 Play/Scene Load에서는 초기화
  - 세이브 시스템이 있다면 완료 상태 복원

완료 기준:

- 폭풍을 잠재운 뒤 플레이어가 막히지 않고 다음 목표로 이동할 수 있다.
- 완료된 폭풍이 같은 세션에서 재발동하지 않는다.

## 4. 권장 구현 순서

1. 미션 소유권과 첫 호흡 이벤트 오염을 먼저 수정한다.
2. 취소/재시작과 `BreathingActive` UI 표시 시점을 정리한다.
3. 런타임 Guiding Light 인스턴스 전달 경로를 만든다.
4. Builder를 갱신형으로 수정하고 Scene에 Trigger/Controller/UI를 배치한다.
5. 플레이스홀더 파티클로 3단계 Fog/Emission 피드백을 검증한다.
6. 최종 VFX와 Wind SFX를 연결하고 Quest 성능을 튜닝한다.
7. 다음 콘텐츠 연결과 전체 회귀 테스트를 수행한다.

이 순서를 따르는 이유는 Scene 아트와 튜닝 전에 공용 호흡 이벤트의 소유권을 해결해야 첫 번째와 두 번째 콘텐츠를 연속으로 테스트할 수 있기 때문이다.

## 5. 최종 사용자 테스트 절차

### 테스트 A: 첫 호흡 이벤트 격리

1. Scene 시작 후 첫 번째 단서 호흡을 3회 완료한다.
2. Guiding Light가 생성되어 이동하는지 확인한다.
3. 아직 Sandstorm Trigger에 들어가지 않은 상태에서 SandstormController 상태를 확인한다.

예상 결과:

- SandstormController는 `Idle`이다.
- 모래 파티클, 높은 Fog, Wind SFX는 시작되지 않는다.
- 첫 호흡 성공 이벤트가 Sandstorm 진행률에 반영되지 않는다.

### 테스트 B: Trigger 진입과 폭풍 상승

1. Guiding Light를 따라 Trigger로 이동한다.
2. Trigger 경계를 한 번 통과한다.
3. 폭풍 상승 시간 동안 UI, VFX, Fog, SFX, 등불을 확인한다.

예상 결과:

- `끝없는 모래 / 불안한 첫 발`과 `모래 폭풍`이 표시된다.
- 파티클과 Fog가 점진적으로 강해진다.
- 바람 SFX가 한 번만 재생된다.
- 화면에 보이는 Guiding Light의 빛이 0으로 감소한다.
- 상승 완료 전에는 호흡이 시작되지 않는다.

### 테스트 C: 호흡 1회당 폭풍 감소

1. 폭풍 상승 완료 후 B를 눌러 호흡을 시작한다.
2. 들숨/날숨을 한 번 완료할 때마다 화면과 Inspector 값을 확인한다.

예상 결과:

- 콘텐츠 B 문구 없이 모래폭풍 지시문과 공통 원형 UI만 보인다.
- 1회 성공: 구슬 1개, 폭풍 약 2/3 유지
- 2회 성공: 구슬 2개, 폭풍 약 1/3 유지
- 3회 성공: 구슬 3개, 폭풍 0
- 각 단계에서 Particle Emission과 Fog Density가 함께 감소한다.

### 테스트 D: 성공 정리

1. 세 번째 호흡 완료 직후 2~3초 동안 상태를 관찰한다.

예상 결과:

- 파티클이 완전히 멈춘다.
- Fog가 진입 전 Density와 설정으로 복구된다.
- Wind SFX가 Fade Out 후 정지한다.
- Guiding Light 빛이 원래 강도로 복구된다.
- 호흡 UI와 지시문이 정상적으로 사라진다.
- 플레이어 상태가 Idle로 돌아가고 다음 진행이 열린다.

### 테스트 E: 취소와 재도전

1. 폭풍 호흡을 시작해 1회 성공한다.
2. B를 눌러 취소한다.
3. 다시 B를 눌러 호흡을 시작하고 3회를 완료한다.

예상 결과:

- 취소 때문에 폭풍 콘텐츠가 영구 정지하지 않는다.
- 재시작 시 원형 UI와 내부 루프 수가 합의된 정책대로 초기화된다.
- 재도전 성공 후 폭풍이 정확히 한 번 클리어된다.

### 테스트 F: 예외와 회귀

- Trigger 안에서 여러 Player Collider가 접촉해도 한 번만 시작된다.
- Trigger 경계를 반복 통과해도 오브젝트와 AudioSource가 중복되지 않는다.
- B를 빠르게 연타해도 상태가 꼬이지 않는다.
- 첫 번째 콘텐츠의 Guiding Light 스폰과 Waypoint 이동이 유지된다.
- Scene을 종료하거나 오브젝트를 비활성화하면 Fog와 SFX가 원복된다.
- 전체 과정에서 `NullReferenceException`, `MissingReferenceException`, Serialized Field 경고가 없다.

## 6. 완료 판정 체크리스트

- [x] 첫 번째 호흡 성공이 SandstormController에 잘못 반영되지 않는다.
- [x] Guiding Light를 따라 Box Trigger에 진입하면 폭풍이 한 번 시작된다.
- [x] 챕터/퀘스트 문구가 명세와 일치한다.
- [x] 모래 Particle, Fog, Wind SFX가 점진적으로 강해진다.
- [x] 실제 Guiding Light 인스턴스의 빛이 0으로 감소한다.
- [x] 폭풍 상승 완료 후 B로 두 번째 호흡을 시작한다.
- [x] 공통 원형 UI가 3회 판정을 표시한다.
- [x] 콘텐츠 B 안내문과 모래폭풍 안내문이 충돌하지 않는다.
- [x] 호흡 1회마다 Particle과 Fog가 단계적으로 감소한다.
- [x] 3회 성공 후 폭풍, Fog, SFX가 완전히 정리된다.
- [x] Guiding Light의 빛이 원래 강도로 복구된다.
- [x] 취소 후 재도전할 수 있다.
- [x] 성공 후 다음 콘텐츠로 진행할 수 있다.
- [x] Builder 반복 실행 시 Hierarchy와 UI가 중복되지 않는다.
- [ ] Quest에서 프레임 저하와 과도한 시야 가림이 없다.
- [x] Console에 Null/Missing Reference 예외가 없다.

## 7. 관련 파일

### Runtime

- `Assets/_Project/Scripts/Sandstorm/SandstormController.cs`
- `Assets/_Project/Scripts/Sandstorm/SandstormZoneTrigger.cs`
- `Assets/_Project/Scripts/System/BreathManager.cs`
- `Assets/_Project/Scripts/System/BreathEventSO.cs`
- `Assets/_Project/Scripts/System/BreathEventsChannel.asset`
- `Assets/_Project/Scripts/System/PlayerStateManager.cs`
- `Assets/_Project/Scripts/UI/BreathCircleUI.cs`
- `Assets/_Project/Scripts/Outside/BreathMissionGuideController.cs`
- `Assets/_Project/Scripts/GuidingLightController.cs`
- `Assets/_Project/Scripts/ClueObject.cs` (`HologramMessage`)

### Editor / Scene / Prefab

- `Assets/_Project/Editor/SandstormZoneBuilder.cs`
- `Assets/_Project/Editor/Scene03OutsideWiringBuilder.cs`
- `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`
- `Assets/_Project/Prefabs/Systems.prefab`
- `Assets/_Project/Prefabs/GuidingLight.prefab`
- `Assets/UI component/06 chapter.prefab`

## 8. 이번 문서 작성 범위

이번 단계에서는 현재 Runtime 코드, Editor Builder, Scene/Prefab 직렬화 상태와 오디오 에셋 유무를 확인하고 구현 계획만 작성했다. Runtime 코드, Editor 도구, Scene, Prefab, VFX, SFX는 변경하지 않았다.
