# Scene 03 오브젝트·SoomUI 수정 계획

## 목표

- `ClueObject`의 메모 외형을 지정된 `memo_open 1.fbx`로 교체한다.
- `SoomUI`의 크기와 카메라 기준 위치를 Inspector에서 쉽게 조절할 수 있게 한다.
- `GuidingLight.prefab`의 시각 모델을 애니메이션 나비로 교체하고, 이동 방향 정렬과 팅커벨 가루 형태의 파티클을 적용한다.
- Scene 03의 네 호흡 구간(길잡이, 모래폭풍, 여우 1차, 여우 2차)이 같은 `SoomUI`를 일관되게 표시하도록 정리한다.

## 현재 구조 확인

- 작업 씬은 `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`이다.
- `ClueObject` 루트에는 `HologramMessage`와 `XRGrabInteractable`이 있으며, 열린 메모 자식은 `messageOpen` 참조 및 Grab Collider로 사용된다. 따라서 루트의 상호작용 스크립트는 유지하고 모델 자식만 교체해야 한다.
- 공용 UI는 하이어라키의 `SoomUI/BreathCircle`이며 `BreathCircleUI`, World Space Canvas, `cameraLocalPosition` 설정을 가진다.
- 별도로 여우 프리팹 아래에 비활성 `UI_BreathCircle` 복제본이 있고, `FoxEncounterController`만 이 복제본을 참조한다.
- 길잡이와 모래폭풍은 `SoomUI/BreathCircle`을 참조하지만, `BreathMissionGuideController`가 길잡이 미션이 아닌 동안 같은 UI에 `Hide()`를 반복 요청할 수 있어 다른 미션의 `Show()`와 충돌할 가능성이 있다.
- `butterfly_2.fbx`에는 애니메이션이 임포트되어 있지만 별도 clip 설정과 반복 재생용 Animator Controller는 아직 없다.

## 1. ClueObject 메모 모델 교체

대상:

- 씬: `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`
- 모델: `Assets/_Project/Prefabs/MyMesh/holomemo/memo_open/memo_open 1.fbx`

작업:

1. `ClueObject` 아래의 기존 열린 메모 시각 자식 `memo_open`을 지정 FBX 인스턴스로 교체한다.
2. 새 모델은 별도 `Visual` 또는 기존 열린 메모 자식 위치에 배치하여, 로컬 Rotation과 Scale을 사용자가 Inspector에서 직접 수정할 수 있게 유지한다. 모델 방향 보정용 런타임 코드는 추가하지 않는다.
3. `HologramMessage.messageOpen` 참조를 새 열린 메모 오브젝트로 다시 연결한다.
4. 기존 열린 메모 Collider가 `XRGrabInteractable.m_Colliders`에 등록되어 있으므로, 새 모델 크기에 맞는 Collider를 유지하거나 새로 맞춘 뒤 Grab 목록을 갱신한다.
5. 닫힌 메모, 홀로그램 UI, Attach Point, 스폰/웨이포인트 참조는 건드리지 않는다.

검증:

- 접근 전/후 열린 메모의 활성화 흐름이 기존과 동일하다.
- 잡기, 컨트롤러 부착, 호흡 미션 진입이 정상 동작한다.
- `ClueObject > memo_open`의 Transform을 바꾸면 코드 수정 없이 방향과 크기가 반영된다.

## 2. SoomUI 크기와 카메라 기준 위치 조절 기능

대상:

- `Assets/_Project/Scripts/UI/BreathCircleUI.cs`
- `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`

작업:

1. `BreathCircleUI` Inspector에 다음 항목을 명확한 헤더와 Tooltip으로 노출한다.
   - 카메라 로컬 위치 오프셋(X: 좌우, Y: 높이, Z: 카메라와의 거리)
   - UI 크기 또는 균일 Scale
   - 필요 시 카메라 앞에 다시 배치할지 여부
2. `Show()` 시 위 설정값을 사용하여 UI를 카메라 앞에 배치하고 크기를 적용한다. 사용자가 입력한 값을 런타임에 임의의 상수로 덮어쓰지 않게 한다.
3. 회전은 카메라의 수평 정면을 향하는 현재 방식을 유지해 HMD의 순간적인 Pitch/Roll 때문에 UI가 기울지 않게 한다.
4. 하이어라키에서 조절할 대상이 분명하도록 `SoomUI/BreathCircle`을 공용 UI의 단일 기준 오브젝트로 사용한다.

Inspector 조절 기준:

- 위치: `BreathCircleUI > Camera Local Position`에서 X/Y/Z 수정
- 크기: 새 `UI Scale` 필드 또는 `SoomUI/BreathCircle`의 균일 Transform Scale 수정
- Z가 작을수록 카메라에 가까워지고, Y가 커질수록 화면에서 위로 이동한다.

검증:

- Play Mode에서 값을 바꾸고 각 미션을 시작했을 때 새 크기와 위치가 반영된다.
- 머리를 돌린 뒤 다음 호흡 미션을 시작해도 현재 카메라 정면에 배치된다.
- 네 미션 사이에서 UI 크기와 위치가 달라지거나 누적 Scale이 생기지 않는다.

## 3. GuidingLight를 애니메이션 나비로 교체

대상:

- 프리팹: `Assets/_Project/Prefabs/GuidingLight.prefab`
- 모델: `Assets/_Project/Prefabs/MyMesh/butterfly/Butterflyy_final/butterfly_2.fbx`
- 이동: `Assets/_Project/Scripts/GuidingLightController.cs`

### 3.1 프리팹 계층 정리

권장 계층:

```text
GuidingLight                 ← 이동/회전 담당, GuidingLightController 유지
├─ ButterflyVisual           ← butterfly_2.fbx, 사용자 Rotation/Scale 조절
├─ PointLight                ← 기존 Light 유지
└─ TinkerDust                ← Particle System
```

작업:

1. 기존 `GlowOrb` 시각 메시를 제거하거나 비활성화하고 `butterfly_2.fbx`를 `ButterflyVisual` 자식으로 넣는다.
2. `GuidingLightController`, 이동 속도, 웨이포인트, 플레이어 대기 거리, 빛 세기 제어는 프리팹 루트에 그대로 유지한다.
3. 나비의 모델 축 보정은 `ButterflyVisual`의 로컬 Rotation으로 처리하고, 크기는 그 자식의 로컬 Scale로 처리한다. 두 값 모두 Inspector에서 바로 수정 가능하게 둔다.
4. 프리팹 루트의 정면(+Z)이 실제 이동 방향을 향하도록 기존 `Quaternion.LookRotation(motionDirection)` 로직을 유지한다. 모델 앞면만 `ButterflyVisual` 로컬 Rotation으로 한 번 맞춘다.

### 3.2 날개짓 반복 애니메이션

1. `butterfly_2.fbx`의 Animation Import를 활성 상태로 유지한다.
2. FBX 내 날개짓 clip의 `Loop Time`을 활성화한다.
3. 전용 Animator Controller를 만들고 날개짓 clip을 Default State로 지정한다.
4. `ButterflyVisual`의 Animator에 Controller를 연결하며 Culling Mode는 화면 밖에서도 필요한 경우 애니메이션이 계속 갱신되도록 설정한다.
5. Play Mode에서 대기 중, 이동 중, 플레이어를 기다리는 중 모두 날개짓이 끊기지 않는지 확인한다.

### 3.3 팅커벨 가루 파티클

`TinkerDust`에 다음을 시작값으로 설정하고 모두 Inspector에서 수정할 수 있게 둔다.

- Main
  - Looping: On
  - Simulation Space: World (나비가 이동한 자리에 가루가 남도록 설정)
  - Start Lifetime: 약 0.6~1.4초
  - Start Speed: 약 0.05~0.25
  - Start Size: 약 0.015~0.06
  - Start Color: 연한 금색/민트색 계열, 알파는 부드럽게 감소
- Emission
  - Rate over Time: 약 15~30
- Shape
  - 작은 Sphere 또는 Cone, 나비 몸체 뒤쪽에 배치
- Color over Lifetime
  - 밝게 시작해 끝에서 투명해지는 Gradient
- Size over Lifetime
  - 초반에 살짝 커졌다가 작아지도록 Curve
- Noise
  - 낮은 Strength로 가루가 살짝 흩날리게 설정
- Renderer
  - 작은 원형/별가루 텍스처와 URP 호환 투명 Additive 또는 Alpha Blended Material 사용
  - Render Alignment는 View 권장

사용자 수정 위치:

- 가루 양: `Emission > Rate over Time`
- 오래 남는 시간: `Main > Start Lifetime`
- 퍼지는 정도: `Main > Start Speed`, `Shape`, `Noise > Strength`
- 입자 크기: `Main > Start Size`, `Size over Lifetime`
- 색과 사라짐: `Color over Lifetime`
- 나비 뒤에서 나오는 위치: `TinkerDust` 자식 Transform의 Local Position

검증:

- 프리팹을 직접 열어 나비 Rotation/Scale과 파티클 값을 수정할 수 있다.
- 스폰된 나비의 앞면이 실제 진행 방향과 일치하고 코너에서 부드럽게 회전한다.
- 파티클이 나비를 따라 한 덩어리로 이동하지 않고 이동 경로에 잠시 남았다가 사라진다.
- 모래폭풍 진입 시 기존 Light Fade/Restore 기능이 계속 동작한다.

## 4. 네 호흡 미션에 공용 SoomUI 적용

Scene 03의 네 호흡 구간을 다음과 같이 취급한다.

1. 길잡이 생성 호흡 (`HologramMessage` / `BreathMissionGuideController`)
2. 모래폭풍 진정 호흡 (`SandstormController`)
3. 여우 불안 완화 1차 호흡 (`FoxEncounterController` 첫 호흡 단계)
4. 여우 불안 완화 2차 호흡 (`FoxEncounterController` 두 번째 호흡 단계)

작업:

1. 네 구간 모두 하이어라키의 단일 `SoomUI/BreathCircle` 컴포넌트를 참조하게 연결한다.
2. 여우 프리팹 아래의 중복 `UI_BreathCircle` 참조를 공용 SoomUI로 교체하고, 더 이상 사용하지 않는 복제 UI는 제거한다.
3. 길잡이용 컨트롤러가 다른 미션 동안 공용 UI를 숨기지 않도록 표시 소유권을 정리한다.
   - 각 미션은 자신이 UI를 표시한 경우에만 숨김을 요청한다.
   - 또는 공용 UI에 현재 요청 소유자를 기록하여 다른 미션의 `Show()`를 이전 미션의 `Hide()`가 취소하지 못하게 한다.
4. `BreathingActive` 진입 시 현재 미션이 `Show()`, 성공/취소/상태 이탈 시 같은 미션이 `Hide()`를 정확히 한 번 호출하도록 정리한다.
5. 각 새 세션 시작 시 `BreathEventsSO.ResetSession()`을 통해 원 크기와 3개 완료 슬롯이 초기화되는지 확인한다.
6. 여우의 두 호흡 단계 사이에서도 같은 UI를 재사용하되, 첫 단계 종료 후 숨기고 두 번째 단계 시작 시 초기화된 상태로 다시 표시한다.

검증 시나리오:

1. 길잡이 미션 시작 → SoomUI 표시 → 3회 완료 → UI 숨김 → 나비 스폰
2. 모래폭풍 미션 시작 → 동일 SoomUI 표시 → 3회 완료 → UI 숨김 → 폭풍 해제
3. 여우 첫 호흡 시작 → 동일 SoomUI 표시 및 슬롯 초기화 → 완료 후 숨김
4. 여우 두 번째 호흡 시작 → 동일 SoomUI 재표시 및 슬롯 초기화 → 완료 후 숨김
5. 각 미션에서 B 버튼 취소/구역 이탈 등 가능한 중단 경로를 실행해 UI가 화면에 남지 않는지 확인
6. 미션 전환 중 길잡이 컨트롤러의 반복 `Hide()` 때문에 모래폭풍/여우 UI가 즉시 사라지지 않는지 확인

## 구현 순서

1. 씬 백업 후 `ClueObject` 모델과 Collider/참조 교체
2. `GuidingLight.prefab`에 나비 모델, Animator, 반복 clip, 파티클 구성
3. `BreathCircleUI`의 Inspector 조절 항목 정리
4. 중복 호흡 UI 제거 및 네 미션의 공용 SoomUI 참조 통일
5. UI 표시 소유권/충돌 로직 수정
6. Unity 컴파일 오류와 Missing Reference 확인
7. Editor Play Mode에서 네 미션을 순서대로 검증
8. 가능하면 XR Interaction Simulator와 실제 HMD에서 UI 거리·크기, 나비 방향, 파티클 밀도 최종 조정

## 완료 기준

- 지정된 메모와 나비 FBX가 씬/프리팹에 적용되어 있다.
- 메모와 나비의 Rotation/Scale을 Inspector에서 코드 없이 수정할 수 있다.
- 나비가 이동 방향을 바라보며 날개짓을 무한 반복한다.
- 나비 뒤에 수정 가능한 팅커벨 가루 파티클이 재생된다.
- SoomUI의 카메라 상대 위치와 크기를 Inspector에서 조절할 수 있다.
- 네 호흡 구간 모두 같은 SoomUI가 시작/완료/취소 흐름에 맞춰 정상 표시되고 초기화된다.
- Console에 컴파일 오류, Missing Script, Missing Reference가 없다.
