# Scene 03 Clue Interaction 수정 및 회귀 테스트 계획

## 1. 목표 사용자 흐름

```text
ClueObject 발견
  → 감지 원 안에서 단서 위치 확인
  → Ray로 선택 가능한 거리에 도달하면 감지 위치 UI 숨김
  → G(Grip/Select)를 누른 채 ClueObject Grab
  → 열린 메시지가 선택한 컨트롤러에 붙어 함께 이동
  → G를 놓으면 열린 상태로 최초 위치/회전 복귀
  → MissionReady 진입
  ├─ Ray + G로 열린 메시지 재확인
  └─ 컨트롤러 위 B 안내 확인 후 B 입력
       → BreathingActive 진입
       → 호흡 측정 시작 및 3회 성공
       → Guiding Light 한 번 스폰
       → Waypoint 이동 및 Idle 복귀
```

## 2. 2026-07-13 사용자 테스트 결과

상태 표기:

- `통과`: 사용자가 Play Mode에서 직접 확인함
- `수정 필요`: 상태 전환 또는 일부 동작은 되지만 목표 UX와 다름
- `차단`: 앞 단계 문제 때문에 아직 시험할 수 없음

| 구간 | 결과 | 확인 내용 및 남은 문제 |
|---|---|---|
| 단서 접근 및 Ray 조준 | 통과 | ClueObject 접근과 오른손 Ray 조준 정상 |
| 최초 감지 UI | 수정 필요 | 감지 원 안에 ClueObject가 들어오도록 위치를 추적하고, Ray로 선택 가능한 시점에는 위치 이미지를 숨겨야 함 |
| 첫 G 입력과 모델 열기 | 통과 | 닫힌 모델이 사라지고 열린 메시지가 표시됨 |
| Grab 중 컨트롤러 추종 | 수정 필요 | Select는 되지만 물체가 선택한 컨트롤러에 붙어 함께 움직이지 않음 |
| 첫 Release와 자동 복귀 | 통과 | 열린 상태 유지, 최초 위치/회전 복귀, MissionReady 전환 정상 |
| MissionReady B 안내 | 수정 필요 | 표시와 상태 구독은 정상이나 카메라 앞이 아니라 선택/오른손 컨트롤러 위에 배치해야 함 |
| 열린 메시지 재확인 | 통과 | Hover G 안내, 재Grab, Interact 전환, 재Release와 MissionReady 복원 정상 |
| B 입력 상태 전환 | 통과 | MissionReady에서 B 입력 시 BreathingActive로 전환되고 이전 안내가 숨음 |
| 호흡 측정 시작 | 수정 후 재시험 | Controller 입력과 성공 횟수는 Console에서 확인됨. Breath Circle이 표시되지 않던 경로를 상태 동기화 방식으로 수정했으며 Game View 재확인이 필요함 |
| 호흡 성공 및 Guiding Light | 차단 | Breath Circle 표시를 재확인한 뒤 테스트 D/E를 진행해야 함 |

정적 배선 확인이나 코드 경로 존재만으로 완료 처리하지 않는다. 아래 체크 표시는 실제 Play Mode 재시험 결과를 기준으로 갱신한다.

## 3. 수정 작업 계획

### 3.1 최초 감지 위치 UI와 Ray 선택 구간 전환

- [x] `05 detect_Somthing` 위치 이미지와 `DetectionUI` 설명 UI의 역할을 분리한다.
  - 위치 이미지: 멀리서 단서 위치를 찾는 안내
  - 설명 UI: 감지한 물체에 대한 설명
- [x] ClueObject의 월드 위치를 카메라 화면 좌표로 투영하여 감지 원 안에 실제 ClueObject가 들어오도록 위치 이미지를 추적시킨다.
- [x] 감지 UI 표시 조건을 다음처럼 정리한다.
  - 카메라 FOV와 감지 거리 안이며 아직 Ray 선택 구간이 아닐 때 위치 이미지 표시
  - ClueObject가 화면 밖이거나 감지 범위를 벗어나면 위치 이미지와 설명 UI 숨김
  - 유효한 XR Ray Hover가 시작되면 위치 이미지는 즉시 숨김
- [x] Ray 선택 가능 여부는 임의 거리값보다 `XRGrabInteractable.hoverEntered`/`hoverExited`의 실제 Far Ray Hover를 우선 사용한다.
- [x] Direct/Near Hover가 위치 이미지 전환을 잘못 발생시키지 않도록 Hover를 발생시킨 Interactor 종류를 확인한다.
- [x] Ray가 잠시 벗어난 경우 위치 이미지를 다시 표시할지 결정하고 일관되게 구현한다.
  - 기본안: 메시지가 아직 닫혀 있고 FOV/감지 범위 안이면 다시 표시
- [x] 첫 Grab 시작 시 위치 이미지와 설명 UI를 모두 숨기고, 열린 이후에는 최초 감지 UI가 다시 나오지 않게 한다.
- [x] `InteractionDetector`와 `HologramMessage`가 동시에 UI를 켜고 끄며 깜빡이지 않도록 표시 소유권을 한 경로로 통합한다.

구현 메모:

- `InteractionDetector`는 FOV/거리 감지 요청만 전달하고 실제 표시 조합은 `HologramMessage`가 소유한다.
- `DetectionIndicatorTracker`가 닫힌 모델의 Renderer/Collider 중심을 화면 좌표로 투영해 감지 원을 이동시킨다.
- 활성 `ICurveInteractionDataProvider`를 가진 Hover만 Far Ray로 판정하며 Direct/Near Hover는 무시한다.
- Far Ray Hover 중에는 위치 이미지만 숨기고 설명 UI는 감지 범위 안에서 유지한다. 첫 Grab 또는 FOV/거리 이탈 시 두 UI를 모두 숨긴다.

완료 기준:

- ClueObject를 바라보면 감지 원 안에 단서가 위치한다.
- 오른손 Ray로 실제 선택 가능한 순간 위치 이미지가 사라진다.
- Ray Hover와 Grab 경계에서 UI가 깜빡이거나 중복 표시되지 않는다.

### 3.2 Grab한 ClueObject를 선택 컨트롤러에 부착

- [x] 현재 Scene의 `XRGrabInteractable` 설정을 Inspector에서 확인한다.
  - Movement Type
  - Attach Transform
  - Use Dynamic Attach
  - Match Attach Position/Rotation
  - Far Attach Mode
  - Retain Transform Parent
- [x] Select 이벤트에 전달된 실제 Interactor가 오른손 Ray인지 확인한다.
- [x] 컨트롤러 기준의 `ClueAttachPoint`를 만들거나 Dynamic Attach를 사용하여 선택한 컨트롤러의 포즈를 따라가게 한다.
- [x] Far Ray로 선택해도 ClueObject가 기존 월드 위치나 Ray 끝점에 남지 않고 컨트롤러 가까이 붙도록 Far Attach 동작을 설정한다.
- [x] 메시지를 읽기 쉬운 위치와 회전이 되도록 Attach Point의 로컬 위치/회전을 조정한다.
- [x] Grab 중 Rigidbody가 XRI 이동을 방해하지 않는지 확인한다.
  - Kinematic/Gravity 설정
  - 제약 조건
  - Collider와 열린 모델 계층
- [x] `messageClose`에서 `messageOpen`으로 모델을 바꿔도 `XRGrabInteractable`이 붙은 루트 Transform과 Collider는 바뀌지 않게 유지한다.
- [x] 첫 Grab과 열린 메시지 재Grab 모두 같은 부착 경로를 사용한다.
- [ ] Release 다음 프레임 자동 복귀 로직이 컨트롤러 부착 수정 후에도 정상인지 회귀 확인한다.

구현 메모:

- 기존 원인은 ClueObject의 `Far Attach Mode=Defer To Interactor`와 Near-Far Interactor의 `Far` 기본값 조합이었다.
- ClueObject가 `Far Attach Mode=Near`를 직접 지정하여 어떤 Near-Far Interactor로 선택해도 Ray 명중점 대신 선택 컨트롤러의 Near Attach Pose를 사용한다.
- `ClueAttachPoint`는 로컬 위치 `(0, 0, 0.4)`, 로컬 회전 `(0, 180, 0)`으로 구성했다. 현재 ClueObject 스케일 기준 약 0.2m 앞에서 메시지 면이 플레이어 쪽을 향한다.
- `Use Dynamic Attach=false`, `Movement Type=Kinematic`, Position/Rotation Tracking 활성, Gravity/Throw 비활성으로 구성했다.
- Select 진입 로그에 Interactor 이름, Far Ray 여부, 적용 Attach Mode를 출력하여 Simulator와 실기기에서 실제 선택 주체를 확인할 수 있다.
- Scene, `Scene03CluePhysicalSetup`, `Scene03GrabInteractionSetup`, 전체 Outside 배선 경로가 동일한 설정을 사용한다.

Play Mode 재확인 대기:

- 첫 Grab과 재Grab의 실제 컨트롤러 추종 감각 및 Attach Point 위치/회전 미세 조정
- Release 후 열린 상태 자동 복귀가 한 번만 실행되는지 확인

완료 기준:

- G를 누르고 있는 동안 열린 ClueObject가 선택한 컨트롤러에 붙어 위치와 회전을 따라간다.
- 사용자가 컨트롤러를 움직여 메시지를 읽기 편한 위치로 옮길 수 있다.
- G를 놓으면 컨트롤러에서 분리되고 열린 상태로 최초 위치/회전에 한 번만 복귀한다.

### 3.3 MissionReady B 안내를 컨트롤러 위에 배치

- [x] `Scene03_MissionReadyUI`에서 Tutorial UI와 B 조작 안내를 별도 Transform으로 분리한다.
- [x] `B: 호흡 훈련 시작` 안내 전용 `MissionReadyPromptAnchor`를 오른손 컨트롤러 아래에 만든다.
- [x] B 안내를 컨트롤러 윗부분에 보이도록 로컬 위치와 크기를 튜닝한다.
- [x] 안내가 HMD 카메라를 향하도록 `FaceCamera` 또는 동일한 빌보드 처리를 적용한다.
- [x] 오른손 컨트롤러 참조가 없을 때 Null 예외가 나지 않도록 안전하게 숨기거나 대체 Anchor를 사용한다.
- [x] `Scene03MissionReadyUISetup` 자동 배선 도구가 반복 실행되어도 Anchor와 안내 UI를 중복 생성하지 않게 수정한다.
- [x] 다음 상태 표시 규칙을 유지한다.
  - MissionReady: B 안내 표시
  - 열린 메시지 재Grab/Interact: B 안내 숨김
  - 재Release/MissionReady 복원: B 안내 다시 표시
  - BreathingActive 또는 Idle: B 안내 숨김

구현 메모:

- `TutorialContent`는 기존처럼 Main Camera 앞에 유지하고 B 문구는 별도 World Space Canvas인 `MissionReadyPromptAnchor`로 분리했다.
- 런타임에 Scene의 `Right Controller`를 찾아 Anchor를 자식으로 옮기며 로컬 위치 `(0, 0.12, 0.08)`, 로컬 스케일 `0.00065`를 적용한다.
- `FaceCamera`가 Anchor의 월드 회전을 갱신하여 컨트롤러를 움직여도 문구가 HMD를 향한다.
- 기존 프리팹의 `BreathingStartPrompt`도 런타임에 자동 마이그레이션하므로 자동 배선 도구 실행 전 Scene 인스턴스와 호환된다.
- 오른손 컨트롤러를 찾지 못하면 Main Camera 우하단 폴백 위치에 표시하며, 카메라도 없으면 경고 후 안전하게 숨긴다.
- `MissionReadyUIController`가 Tutorial과 B 안내를 별도로 토글하되 동일한 PlayerState 이벤트를 사용하므로 MissionReady 이외 상태에는 둘 다 남지 않는다.

Play Mode 재확인 대기:

- 첫 Release 후 B 안내가 오른손 컨트롤러 위를 안정적으로 따라가는지 확인
- 재Grab/재Release/B 입력을 반복했을 때 Anchor가 중복되거나 이전 상태에서 남지 않는지 확인

완료 기준:

- 첫 Release 후 B 안내가 카메라 중앙이 아니라 오른손 컨트롤러 위를 따라다닌다.
- 재확인과 호흡 시작 상태 전환 때 B 안내가 중복되거나 남아 있지 않는다.

### 3.4 BreathingActive 진입 후 호흡 측정 시작 문제 진단 및 수정

- [x] B 입력 직후 아래 런타임 값을 한 번씩 확인할 수 있도록 임시 진단 로그 또는 Inspector 디버그 값을 추가한다.
  - `PlayerState == BreathingActive`
  - `BreathManager` 활성 여부
  - 상태 이벤트 구독 여부와 시작 콜백 호출 횟수
  - `isMeasuring`
  - 좌/우 Controller Transform 참조
  - 현재 회전 변화량, 정규화 값, 호흡 단계, 완료 횟수
- [x] `BreathManager`가 상태 변경 전에 이벤트를 구독했는지 확인하고, 활성화 순서와 무관하게 한 번만 구독하도록 정리한다.
- [x] 좌/우 Controller 참조가 Simulator에서 실제로 회전하는 Transform을 가리키는지 확인한다.
- [x] Controller 참조가 없을 때 측정 시작이 조용히 중단되지 않도록 명확한 경고와 안전 처리를 추가한다.
- [x] 현재 호흡 입력 방식이 실제 호흡 센서가 아니라 컨트롤러 기울기 기반인지 확정한다.
  - 컨트롤러 기울기 방식 유지 시 Simulator 조작법과 임계값을 테스트 문서에 명시
  - 실제 호흡/마이크 입력이 목표라면 별도 입력 Provider 작업으로 분리
- [x] 두 컨트롤러 평균값 때문에 한쪽만 움직였을 때 임계값에 도달하지 않는지 확인한다.
- [x] `inhaleThreshold`, `exhaleThreshold`, `maxBreathAngle`이 현재 입력 포즈에서 도달 가능한 값인지 확인한다.
- [x] Breath Circle이 하나만 존재하고 동일한 `BreathEventChannel`을 구독하는지 재확인한다.
- [x] 들숨/날숨 이벤트가 한 동작에 중복 발행되지 않도록 단계 전이와 디바운스를 확인한다.
- [x] 진단 완료 후 임시 과다 로그는 제거하고 오류/필수 상태 로그만 남긴다.

진단 및 구현 결과:

- 기존 Editor 로그에서 `StartCalibrationAndMeasurement()` 호출과 `0도 기준 완료` 로그가 확인되어 상태 이벤트와 측정 세션 시작 자체는 동작하고 있었다.
- 기존 입력은 좌우 Controller 회전 변화량의 평균이었다. 한쪽만 움직이면 들숨 기준인 평균 21°에 도달하려고 한 손을 약 42° 기울여야 했기 때문에 측정이 반응하지 않는 것처럼 보였다.
- 입력값을 좌우 Controller 중 더 큰 회전 변화량으로 변경했다. 이제 한쪽 Controller만 사용해도 된다.
- 현재 값은 실제 호흡/마이크 센서가 아니라 Controller 기울기 기반이다. 실제 호흡 센서 연동은 별도 Input Provider 작업이다.
- 기본 설정은 `maxBreathAngle=30°`, `inhaleThreshold=0.7`, `exhaleThreshold=0.3`이다.
  - 들숨: 어느 한 Controller를 기준 자세에서 21° 이상 기울이고 최소 0.15초 유지
  - 날숨: 같은 Controller를 기준 자세 쪽으로 되돌려 변화량을 9° 이하로 만들고 최소 0.15초 유지
- B는 측정 시작/취소 버튼이다. BreathingActive 진입 후 B를 다시 누르면 Idle로 취소되므로 호흡 입력 중에는 B를 다시 누르지 않는다.
- `BreathManager` Inspector에서 구독 여부, 상태 진입 횟수, 세션 횟수, 좌우 각도, 사용 중인 입력 각도, 정규화 값, 단계, 성공 횟수를 확인할 수 있다.
- 상태 진입, 들숨 인식, 회차 성공, 최종 성공, 필수 참조 오류만 로그로 남기고 프레임별 값 로그는 출력하지 않는다.
- Scene에는 `BreathCircle_Outside`와 `BreathManager`가 각각 하나이며 `BreathEventsChannel` GUID `91e1c179be2cf2444978f568e7476427`을 공통 사용한다.
- `BreathMissionGuideController`가 상태 진입 이벤트를 놓치더라도 매 프레임 현재 상태와 Breath Circle 활성 상태를 동기화하도록 보강했다.
- `BreathCircleUI.Show()`가 Canvas 활성화, World Camera 참조, CanvasGroup과 원형 Sprite를 복구한 뒤 표시하도록 보강했다.
- 중복 Show/Hide 요청은 무시하여 슬롯 초기화 반복과 Fade Coroutine 재시작을 방지한다.
- 표시 전환 시 `[BreathMissionGuideController] Breath Circle 표시/숨김` 로그를 한 번 남겨 UI 활성 여부를 확인할 수 있다.
- 추가 확인 결과 Scene에 `Main Camera/BreathCircle_Outside`와 `SoomUI/BreathCircle`이 중복 존재했고, 상태 컨트롤러가 사용자가 제작한 공용 UI가 아닌 복제본을 참조하고 있었다.
- Scene 03의 `BreathMissionGuideController` 참조를 `SoomUI/BreathCircle`로 교체했고 기존 Outside 복제본은 Scene에서 제거되어 현재 `BreathCircleUI`는 하나만 존재한다.
- 공용 Breath Circle은 `SoomUI` 자식 계층을 유지하면서 표시 중 HMD 카메라 앞 `(0, -0.08, 0.75)` 위치를 따라간다.
- `Scene03BreathInputSetup`과 `Scene03OutsideWiringBuilder`도 `SoomUI/BreathCircle`을 기준으로 배선하고 다른 Breath Circle 복제본을 제거하도록 변경했다.
- Controller 입력 성공 로그는 발생하지만 UI 값이 변하지 않는 재시험 결과를 반영해 `BreathEventsSO`에 최신 호흡값/완료 횟수와 버전을 런타임 스냅샷으로 보관하도록 보강했다.
- `BreathCircleUI`는 기존 이벤트 구독과 함께 SO 스냅샷을 매 프레임 동기화한다. UI 활성화·구독 순서 때문에 이벤트를 놓쳐도 중앙 구슬 크기와 완료 슬롯을 복구한다.
- Play Mode Inspector의 `BreathCircleUI`에서 `_eventChannelSubscribed`, `_cachedBreathValue`, `_receivedLoopCount`로 연결과 UI 수신값을 확인할 수 있다.
- 정상 연결 시 Console에 `[BreathCircleUI] 이벤트 채널 연결 완료`와 회차별 `[BreathCircleUI] 호흡 UI N회차 갱신`이 출력된다.

Play Mode 재확인 대기:

- B 입력 한 번에 측정 세션 로그가 한 번만 발생하는지 확인
- 한쪽 Controller 21° 이상 → 9° 이하 동작으로 Breath Circle과 성공 횟수가 한 번 갱신되는지 확인
- 세 사이클 완료 전까지 B를 다시 누르지 않고 3회 성공과 MissionSuccess를 확인
- Console에 Null/Missing Reference 예외가 없는지 확인

완료 기준:

- B 입력 한 번당 측정 세션이 정확히 한 번 시작된다.
- BreathingActive 진입 직후 Breath Circle이 표시되고 입력값 변화가 UI에 반영된다.
- 정해진 들숨/날숨 입력 한 사이클마다 성공 횟수가 정확히 한 번 증가한다.
- Play Mode Console에 Null/Missing Reference 예외가 없다.

### 3.5 Guiding Light 후속 회귀

구현 변경:

- Scene 03의 `HologramMessage.guidingLightPrefab`을 `Assets/PowerUp/Prefabs/PowerUpContainerYellowDollar.prefab`으로 교체했다.
- PowerUp 원본에 `GuidingLightController`가 없어도 스폰 인스턴스에 자동 부착한 뒤 기존 Waypoint 경로를 시작한다.
- Scene 03 전체 배선, Guiding Light 보상 설정, Waypoint 설정 도구도 새 PowerUp 프리팹 경로를 사용한다.
- 실제 재시험 로그에서 3/3 성공과 `MissionSuccess` 방송은 확인됐지만 보상 콜백이 미션 소유 플래그 조건에서 조용히 종료될 수 있었다.
- `MissionStarted` 상태에서 성공 방송 시점이 `BreathingActive`라면 상태 진입 이벤트를 놓쳤더라도 정상 소유 미션으로 인정하도록 수정했다.
- 성공 채널 재구독을 보강하고 스폰된 PowerUp의 모든 ParticleSystem을 즉시 재생한다.
- Console의 `호흡 성공 보상 수신`과 `빛무리 스폰 완료` 로그로 진행 상태, 프리팹, 스폰 위치, Waypoint 개수를 확인할 수 있다.

- [ ] 3회의 들숨/날숨 성공 이벤트가 정확히 세 번 누적되는지 확인한다.
- [ ] 이 ClueObject가 시작한 미션 성공일 때만 `GuidingLight(Clone)`이 정확히 하나 생성되는지 확인한다.
- [ ] `길라잡이` 안내가 한 번 표시되는지 확인한다.
- [ ] Guiding Light가 `Scene03_GuidingWaypoints` 14개를 순서대로 이동하는지 확인한다.
- [ ] 성공 후 `CompleteBreathingMission()`으로 Idle에 복귀하고 이동 잠금/Breath Circle이 해제되는지 확인한다.
- [ ] 취소 시 미션 소유 플래그가 초기화되어 이후 다른 성공 이벤트로 Guiding Light가 잘못 스폰되지 않는지 확인한다.

이 항목은 3.4의 호흡 측정 시작 문제가 해결된 뒤 진행한다.

## 4. 수정 적용 순서

1. Scene을 백업하고 Console의 기존 오류를 기록한다.
2. 3.1 감지 위치 UI와 Ray Hover 인계 조건을 수정한다.
3. 3.2 XR Grab Attach를 수정하고 첫 Grab/재Grab/Release를 우선 시험한다.
4. 3.3 B 안내를 오른손 컨트롤러 Anchor로 이동한다.
5. 관련 Editor 자동 배선 도구를 실행하고 Scene/Prefab을 저장한다.
6. 테스트 A와 B를 다시 실행해 기존 통과 항목에 회귀가 없는지 확인한다.
7. 3.4 진단 정보를 켠 상태로 테스트 C를 실행하고 호흡 측정 중단 지점을 찾는다.
8. 호흡 측정을 수정한 뒤 테스트 C의 측정 Smoke Test를 통과시킨다.
9. 테스트 D와 E로 Guiding Light, 완료, 취소, 반복 입력을 검증한다.
10. Console을 다시 확인하고 아래 최종 체크리스트를 갱신한다.

## 5. Play Mode 재시험 절차

### 사전 준비

1. `Scene_03_InGame_Outside`를 연다.
2. 수정에 해당하는 자동 배선 도구를 실행한다.
   - Clue Grab/Return/Reopen UI 배선 도구
   - MissionReady UI 및 컨트롤러 Anchor 배선 도구
   - Scene 03 Outside 전체 참조 배선 도구는 기존 참조가 비었거나 변경됐을 때만 실행
3. Hierarchy에 UI, Breath Circle, `BreathManager`가 중복 생성되지 않았는지 확인한다.
4. `HologramMessage`의 메시지 모델, UI, 이벤트 채널, Guiding Light, Spawn Point, Waypoint 14개 참조를 확인한다.
5. Scene과 변경된 Prefab을 저장한다.
6. Console을 Clear하고 Play Mode에 진입한다.
7. XR Interaction Simulator에서는 오른손 Grip/Select가 `G`, B/Secondary Button이 `2`인지 Simulator UI에서 확인한다.
8. 실기기에서는 오른손 Grip과 B 버튼을 사용한다.

### 테스트 A: 최초 감지, Grab, 자동 복귀

1. ClueObject에 접근하되 아직 Ray Hover가 발생하지 않는 거리에서 바라본다.
2. 감지 원 안에 ClueObject가 들어오는지 확인한다.
3. Ray로 선택 가능한 거리까지 접근해 오른손 Ray를 ClueObject에 맞춘다.
4. 유효한 Ray Hover가 시작될 때 감지 위치 이미지가 사라지는지 확인한다.
5. G를 누른 채 유지한다.
6. 열린 메시지가 선택한 오른손 컨트롤러에 붙는지 확인한다.
7. 컨트롤러를 위치와 각도별로 움직여 메시지가 자연스럽게 따라오는지 확인한다.
8. G를 놓는다.

예상 결과:

- 첫 G 입력에서 열린 모델로 변경되고 Select는 유지된다.
- Grab 중 ClueObject가 컨트롤러를 따라 움직인다.
- Release 후 열린 상태로 최초 위치/회전에 복귀한다.
- MissionReady가 되고 B 안내가 오른손 컨트롤러 위에 나타난다.
- 물체가 바닥에 떨어지거나 마지막 Grab 위치에 남지 않는다.

### 테스트 B: 열린 메시지 재확인 회귀

현재 사용자 테스트에서는 통과했다. 3.2와 3.3 수정 후 회귀 여부만 다시 확인한다.

1. 열린 ClueObject에서 Ray를 치워 `G: 메시지 열기`가 숨는지 확인한다.
2. Ray를 다시 맞춰 G 안내가 나타나는지 확인한다.
3. G로 재Grab하고 컨트롤러 추종을 확인한다.
4. 재Grab 중 B 안내가 숨고 상태가 Interact인지 확인한다.
5. G를 놓아 최초 위치/회전 복귀, MissionReady, B 안내 재표시를 확인한다.
6. 위 과정을 세 번 반복한다.

### 테스트 C: 호흡 측정 Smoke Test

1. Play Mode를 새로 시작해 테스트 A를 완료한다.
2. 열린 ClueObject를 다시 Grab하지 않고 B 안내가 보이는 상태에서 B를 한 번 누른다.
   - Simulator: `2`
   - 실기기: 오른손 B
3. BreathingActive 전환과 Breath Circle 표시를 확인한다.
4. 진단 값에서 `isMeasuring=true`와 유효한 Controller 참조를 확인한다.
5. B를 다시 누르지 않는다. BreathingActive 중 B 재입력은 측정 취소 동작이다.
6. 왼손 또는 오른손 Controller 하나를 시작 자세에서 21° 이상 기울여 0.15초 이상 유지한다.
7. Console의 `[BreathManager] 들숨 감지`와 Breath Circle 값 증가를 확인한다.
8. 같은 Controller를 시작 자세로 되돌려 회전 변화량을 9° 이하로 0.15초 이상 유지한다.
9. `[BreathManager] 호흡 1/3회 성공`과 슬롯 하나 채움을 확인한다.

통과 조건:

- BreathingActive 상태 전환만 되는 것이 아니라 실제 측정값과 Breath Circle이 함께 갱신된다.
- 입력 한 사이클당 성공 이벤트가 한 번만 발생한다.
- Inspector의 `measurementSessionCount`가 B 입력 한 번에 1만 증가한다.

테스트 C를 통과하기 전에는 테스트 D와 E를 완료 처리하지 않는다.

### 테스트 D: 3회 성공과 Guiding Light

1. 테스트 C와 같은 방식으로 들숨/날숨을 총 3회 완료한다.
2. 세 번째 성공 직후 Hierarchy와 Game View를 확인한다.

예상 결과:

- `GuidingLight(Clone)`이 정확히 하나 생성된다.
- `길라잡이` 안내가 표시된다.
- Guiding Light가 첫 Waypoint부터 14개를 순서대로 이동한다.
- 플레이어 상태가 Idle로 복귀한다.
- 이동 잠금과 Breath Circle이 해제된다.

### 테스트 E: 예외, 반복 입력, 취소

- 첫 Grab 직후 매우 빠르게 G를 놓아도 열린 채 원위치로 복귀한다.
- 자동 복귀 중 G를 연타해도 이중 선택, 중복 Coroutine, 위치 튐이 없다.
- 열린 메시지를 여러 번 재확인해도 UI와 `SetMissionZone(true)`가 중복되지 않는다.
- MissionReady에서 B를 한 번 누르고 계속 유지해도 측정 세션은 한 번만 시작한다.
- BreathingActive에서 B를 의도적으로 다시 누르면 취소되어 Idle로 복귀하고 보상이 스폰되지 않는다.
- 호흡 도중 취소한 뒤 Guiding Light가 스폰되지 않는다.
- 취소 후 새 미션을 시작할 수 있고 이전 미션의 성공 횟수가 남지 않는다.
- 전체 과정에서 `NullReferenceException`, `MissingReferenceException`, Missing Serialized Field 경고가 없다.

## 6. 최종 완료 판정 체크리스트

### 현재 사용자 확인 완료

- [x] 최초 Grab에서 닫힌 메시지가 열린다.
- [x] G를 놓으면 열린 상태로 최초 위치와 회전에 복귀한다.
- [x] 첫 Release 후 MissionReady로 전환된다.
- [x] 열린 ClueObject의 Ray Hover 여부에 따라 `G: 메시지 열기`가 표시/숨김된다.
- [x] 열린 ClueObject를 다시 Grab하고 놓으면 MissionReady와 B 안내가 복원된다.
- [x] MissionReady에서 B 입력 시 BreathingActive로 전환된다.
- [x] BreathingActive 진입 시 MissionReady UI와 G 안내가 숨는다.

### 수정 후 재확인 필요

- [ ] 감지 원 안에 ClueObject가 정확히 위치하고 Ray 선택 가능 시 위치 이미지가 사라진다.
- [ ] 첫 Grab과 재Grab 중 ClueObject가 선택한 컨트롤러에 붙어 자연스럽게 따라온다.
- [ ] B 안내가 오른손 컨트롤러 윗부분에 표시되고 상태에 따라 올바르게 숨고 다시 나타난다.
- [ ] BreathingActive 진입 직후 실제 호흡 측정과 Breath Circle 갱신이 시작된다.
- [ ] 들숨/날숨 한 사이클당 성공 이벤트가 한 번만 누적된다.
- [ ] 3회 성공 시 Guiding Light가 한 번만 스폰되고 Waypoint 이동을 시작한다.
- [ ] 성공 후 Idle 복귀와 이동 잠금 해제가 정상이다.
- [ ] 반복 입력과 취소 경로에서 UI, 상태, 측정 횟수, 미션 소유 플래그가 정상 복구된다.
- [ ] Console에 Null/Missing Reference 예외가 없다.

## 7. 관련 파일

### Runtime

- `Assets/_Project/Scripts/ClueObject.cs` (`HologramMessage`)
- `Assets/_Project/Scripts/System/InteractionDetector.cs`
- `Assets/_Project/Scripts/System/PlayerStateManager.cs`
- `Assets/_Project/Scripts/System/PlayerInputHandler.cs`
- `Assets/_Project/Scripts/System/LocomotionStaticController.cs`
- `Assets/_Project/Scripts/Outside/MissionReadyUIController.cs`
- `Assets/_Project/Scripts/Outside/BreathMissionGuideController.cs`
- `Assets/_Project/Scripts/System/BreathManager.cs`
- `Assets/_Project/Scripts/UI/BreathCircleUI.cs`
- `Assets/_Project/Scripts/GuidingLightController.cs`

### Editor / Scene / Prefab

- `Assets/_Project/Editor/Scene03GrabInteractionSetup.cs`
- `Assets/_Project/Editor/Scene03MissionReadyUISetup.cs`
- `Assets/_Project/Editor/Scene03OutsideWiringBuilder.cs`
- `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`
- `Assets/_Project/Prefabs/UI/Scene03_MissionReadyUI.prefab`
- `Assets/PowerUp/Prefabs/PowerUpContainerYellowDollar.prefab`
- `Assets/_Project/Input/Scene03ClueInputActions.asset`

## 8. 이번 문서 수정 범위

이번 단계에서는 Console에서 호흡 입력이 처리되지만 Breath Circle이 보이지 않는 결과를 반영했다. 원인은 Scene 03 전용 복제 UI와 `SoomUI/BreathCircle`의 중복 및 잘못된 컨트롤러 참조였다. 공용 UI 참조, HMD 추종 표시, Scene과 Editor 자동 배선 도구를 수정했다.
