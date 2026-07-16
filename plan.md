# SOOM 통합 오디오 시스템 구현 계획

## 1. 목표

기존 `SoomAudioManager`를 프로젝트의 단일 오디오 진입점으로 확장한다. 사용자는 Inspector의 공용 오디오 설정 에셋 한 곳에서 클립과 개별 볼륨을 지정하고, Scene 01에서 정상 진입하거나 Scene 03부터 직접 실행해도 동일하게 소리가 재생되어야 한다.

구현 대상은 다음과 같다.

1. 공용 상호작용 효과음
   - Scene 01 시작 버튼 클릭
   - Scene 03의 접힌 메시지를 처음 Grab
   - Scene 03 여우 상호작용 UI의 유효한 버튼 클릭
   - 세 입력은 동일한 `interactionSfx`를 사용한다.
2. Scene 03 배경음
3. Scene 03에서 플레이어가 `PlayerState.Move`일 때 모래 발걸음 효과음
4. 여우 호흡 미션 결과 효과음
   - 집중 호흡 완료 후 여우 상태가 바뀌고 불안의 막이 나타나는 순간
   - 불안의 막 제거 호흡 완료 후 막이 사라지는 순간
5. 호흡 루프 1회 성공마다 원이 채워지는 효과음

이번 오디오 작업은 기존 상호작용, 호흡 판정, 여우 단계 전이 및 이동 상태 규칙을 변경하지 않고, 이미 확정된 이벤트가 발생하는 위치에 재생 호출만 연결한다.

## 2. 현재 구현 분석

### 2.1 기존 오디오 기반

- `Assets/_Project/Scripts/Core/SoomAudioManager.cs`가 이미 존재한다.
- `Master`, `BGM`, `SFX`, `Voice` 네 채널과 `PlayerPrefs` 기반 0~100 볼륨 저장을 지원한다.
- 선택적 `AudioMixer`와 Mixer 미연결 시 사용할 fallback `AudioSource`를 지원한다.
- `PlayBGM`, `PlaySFX`, `PlayVoice` API가 이미 있으므로 새 AudioManager를 병렬로 만들 필요가 없다.
- `DontDestroyOnLoad` 싱글턴이지만 현재 씬 파일상 `SoomAudioManager`는 Scene 03의 `SoomCore`에만 존재한다. Scene 01에서 시작하면 Scene 03 이전에는 `Instance`가 없을 수 있다.
- Scene 03의 기존 매니저가 Scene 01에서 넘어온 영속 인스턴스와 겹쳐도 현재 싱글턴 방어 로직이 중복 인스턴스를 제거할 수 있다.

### 2.2 이벤트 연결 지점

- 시작 버튼: `StartSequenceController.OnStartButtonClicked()`가 중복 실행을 `sequenceStarted`로 막은 뒤 영상을 시작한다.
- 메시지 Grab: `HologramMessage`(`ClueObject.cs`)의 `OnMessageGrabbed()`가 XRI `selectEntered`를 받으며, `progressBeforeViewing == Closed`일 때가 접힌 메시지의 최초 Grab이다.
- 여우 UI: `FoxEncounterController.OnActionButtonClicked()`가 Wary, Revealed, Cleared 단계의 버튼 동작을 한 곳에서 분기한다.
- 여우 상태 변화: `RevealAnxiety()`가 첫 호흡 미션 완료 후 호출되고, `ClearMembrane()`이 두 번째 호흡 미션 완료 후 불안의 막을 제거한다.
- 호흡 1회 성공: `BreathManager`와 `BreathTiltDriver` 모두 직접 소리를 재생하는 대신 `BreathEventsSO.RaiseLoopCompleted()`를 발행한다. 오디오 매니저가 `OnBreathLoopCompleted`를 구독하면 두 입력 구현을 동시에 지원할 수 있다.
- 이동: `PlayerMovementDetector`가 입력 크기에 따라 `PlayerStateManager.SetMoving()`을 호출하고, 상태는 `Idle <-> Move`로 전환된다. `OnStateEnter/OnStateExit`를 사용하면 프레임별 중복 재생을 피할 수 있다.

### 2.3 현재 오디오 에셋

- 프로젝트에 사용할 수 있는 오디오 파일은 제한적이며, 최종 클립은 아직 모두 준비되지 않았다.
- 기존 사막 바람 WAV와 XRI 샘플 Button Pop은 있으나 자동으로 최종 사운드로 확정하지 않는다.
- 모든 슬롯은 null-safe하게 구현하여 클립을 나중에 넣어도 코드 수정 없이 동작하게 한다.

## 3. 최종 구조

```text
SoomAudioLibrary.asset                     단일 Inspector 관리 지점
  ├─ interactionSfx + volume
  ├─ scene03Bgm + volume
  ├─ sandFootstepClips[] + volume/cadence/pitch
  ├─ foxRevealSfx + volume
  ├─ membraneClearSfx + volume
  └─ breathLoopSuccessSfx + volume
                  │
                  ▼
SoomAudioManager (DontDestroyOnLoad)
  ├─ Scene 01/03에서 같은 Library 참조
  ├─ Scene load 감지 → Scene 03 BGM 시작/다른 씬에서 정지
  ├─ PlayerState 감지 → Scene 03 Move 동안 발걸음 one-shot
  ├─ BreathEventsSO 구독 → 호흡 루프 성공음
  └─ 의미 기반 public API → 상호작용/여우 효과음
```

클립과 개별 gain을 각 씬의 컴포넌트에 복제하지 않고 `SoomAudioLibrary.asset` 한 곳에 저장한다. Scene 01과 Scene 03의 매니저는 동일 에셋을 참조한다. 채널 전체 음량은 기존 환경설정의 `Master/BGM/SFX/Voice` 슬라이더가 계속 담당하고, Library의 개별 볼륨은 해당 채널 볼륨에 곱해지는 상대 gain으로 사용한다.

## 4. 데이터 설계

### 4.1 `SoomAudioLibrarySO`

신규 파일:

- `Assets/_Project/Scripts/Audio/SoomAudioLibrarySO.cs`
- `Assets/_Project/Settings/SoomAudioLibrary.asset`

Inspector 필드:

| 그룹 | 필드 | 용도 |
|---|---|---|
| Interaction | `interactionSfx` | 시작 버튼, 최초 메시지 Grab, 여우 UI 공용음 |
| Interaction | `interactionVolume` | 공용 상호작용음 상대 볼륨 0~1 |
| Scene 03 BGM | `scene03Bgm` | Scene 03 배경 루프 |
| Scene 03 BGM | `scene03BgmVolume` | BGM 상대 볼륨 0~1 |
| Scene 03 BGM | `bgmFadeDuration` | 씬 진입/이탈 페이드 시간 |
| Sand Footstep | `sandFootstepClips` | 한 개 또는 복수 발걸음 클립. 복수면 직전 클립을 피해서 선택 |
| Sand Footstep | `sandFootstepVolume` | 발걸음 상대 볼륨 0~1 |
| Sand Footstep | `footstepInterval` | Move 상태에서 one-shot 간격 |
| Sand Footstep | `footstepPitchRange` | 반복감 완화를 위한 작은 pitch 범위 |
| Fox | `foxRevealSfx` | 집중 호흡 완료 후 여우 상태/막 노출 효과음 |
| Fox | `foxRevealVolume` | 상태 변화음 상대 볼륨 0~1 |
| Fox | `membraneClearSfx` | 불안의 막이 완전히 사라질 때 효과음 |
| Fox | `membraneClearVolume` | 막 제거음 상대 볼륨 0~1 |
| Breath | `breathLoopSuccessSfx` | 들숨/날숨 한 사이클 성공 후 원이 채워질 때 효과음 |
| Breath | `breathLoopSuccessVolume` | 호흡 루프 성공음 상대 볼륨 0~1 |

두 여우 효과음에 같은 AudioClip을 지정하면 같은 소리를 쓸 수 있고, 서로 다른 클립을 지정하면 각 전환을 구분할 수 있다. 사용하지 않을 이벤트는 클립을 비워둔다.

### 4.2 Mixer와 개별 gain 규칙

- 채널 볼륨: 기존 `SetVolume(AudioChannel, 0~100)` 유지
- 이벤트 볼륨: Library의 0~1 gain
- Mixer 연결 시: Mixer가 채널 볼륨을 담당하고 각 `AudioSource.volume` 또는 `PlayOneShot` 인자가 이벤트 gain을 담당한다.
- Mixer 미연결 시: fallback source에 `channelNormalized * eventGain`이 적용되도록 현재 코드를 보완한다.
- BGM gain과 SFX pitch를 바꾼 뒤 다음 재생에 값이 남지 않도록 재생 종료/호출 직후 기본값을 복원한다.

## 5. `SoomAudioManager` 확장

대상 파일:

- `Assets/_Project/Scripts/Core/SoomAudioManager.cs`

### 5.1 Inspector 참조

- `SoomAudioLibrarySO audioLibrary`
- `BreathEventsSO breathEventsChannel`
- 기존 Mixer와 세 AudioSource 필드는 유지한다.
- 디버깅이 쉽도록 현재 씬, 현재 BGM, 발걸음 재생 여부를 read-only 성격의 runtime 상태로 분리한다.

### 5.2 의미 기반 재생 API

호출자가 AudioClip과 볼륨을 직접 알지 않도록 다음 API를 제공한다.

```csharp
PlayInteractionSfx()
PlayFoxRevealSfx()
PlayMembraneClearSfx()
```

내부 범용 API는 기존 `PlaySFX`를 유지하되 pitch가 필요한 발걸음용 helper와 BGM fade helper를 추가한다. 모든 API는 `Instance`, Library, AudioClip 또는 AudioSource가 없을 때 예외 없이 반환한다.

### 5.3 씬 생명주기

- `SceneManager.sceneLoaded`를 구독한다.
- `Scene_03_InGame_Outside` 로드 시 `scene03Bgm`을 fade-in 및 loop 재생한다.
- 다른 씬 로드 시 Scene 03 BGM이면 fade-out 후 정지한다.
- 같은 Scene 03 재통지 또는 additive load로 BGM이 중복 시작되지 않게 현재 clip/isPlaying을 확인한다.
- 씬 로드 다음 프레임에 현재 `PlayerStateManager.Instance`를 다시 찾아 상태 이벤트를 구독한다.
- 이전 PlayerStateManager 구독을 반드시 해제하여 씬 전환 뒤 유실 객체 콜백을 막는다.

### 5.4 모래 발걸음

- 조건은 `activeScene == Scene_03_InGame_Outside && PlayerState == Move` 두 가지를 모두 만족해야 한다.
- `Move` 진입 시 발걸음 coroutine을 시작하고 즉시 또는 짧은 첫 간격 후 one-shot을 재생한다.
- 지정된 interval마다 `sandFootstepClips` 중 하나를 선택한다.
- `Move` 이탈, Scene 03 이탈, 매니저 비활성화 시 coroutine을 즉시 중지한다.
- `Update()`에서 매 프레임 소리를 재생하지 않는다.
- 호흡/상호작용 상태가 이동 상태를 막는 기존 규칙을 그대로 따르므로 미션 중 발걸음은 자동으로 멈춘다.

### 5.5 호흡 루프 성공음

- 영속 싱글턴 한 개만 `BreathEventsSO.OnBreathLoopCompleted`를 구독한다.
- count가 이전에 처리한 값보다 증가했을 때 한 번 재생한다.
- `ResetSession()` 후 count가 0으로 돌아오면 중복 방지 카운터도 초기화한다.
- `OnMissionSuccess`에서는 루프 성공음을 추가 재생하지 않는다. 마지막 루프의 `OnBreathLoopCompleted`에서 이미 한 번 재생되므로 겹침을 방지한다.

## 6. 기존 스크립트 연결

### 6.1 Scene 01 시작 버튼

대상:

- `Assets/_Project/Scripts/System/StartSequenceController.cs`

`OnStartButtonClicked()`에서 `sequenceStarted` 검사를 통과한 직후 `PlayInteractionSfx()`를 호출한다. 같은 프레임에 UI가 비활성화되어도 AudioManager는 영속 객체이므로 소리가 끊기지 않는다. 버튼의 Inspector `onClick`에 별도 AudioSource 이벤트를 추가하지 않아 이중 재생을 방지한다.

### 6.2 접힌 메시지 Grab

대상:

- `Assets/_Project/Scripts/ClueObject.cs`의 `HologramMessage.OnMessageGrabbed()`

`progressBeforeViewing == MessageProgress.Closed` 분기 안에서만 `PlayInteractionSfx()`를 호출한다. 이미 열린 메시지를 재확인하는 Grab에서는 재생하지 않아 “접힌 메시지를 잡을 때”라는 요구사항과 일치시킨다.

### 6.3 여우 UI 클릭

대상:

- `Assets/_Project/Scripts/Fox/FoxEncounterController.cs`

`OnActionButtonClicked()`에서 현재 phase에 해당하는 유효 동작이 실제 실행될 때만 `PlayInteractionSfx()`를 호출한다. 숨겨진 버튼이나 잘못된 단계에서 들어온 호출에는 소리를 내지 않는다. `NotifyInteractBegin()`도 같은 메서드로 합류하므로 향후 UI 외 입력도 동일한 피드백을 얻는다.

### 6.4 여우 상태/불안의 막

- `RevealAnxiety()`가 실제 상태 전환을 적용하는 순간 `PlayFoxRevealSfx()`를 호출한다.
- `ClearMembrane()`에서 alpha를 0으로 만들고 오브젝트를 제거하는 순간 `PlayMembraneClearSfx()`를 호출한다.
- `HandleMissionSuccess()`에 직접 재생을 넣지 않는다. 단계 전이 메서드에 배치해야 향후 다른 진입 경로가 추가되어도 시각 변화와 소리가 항상 같이 발생한다.

### 6.5 호흡 UI

`BreathCircleUI`나 `BreathManager`, `BreathTiltDriver`에는 개별 AudioSource를 추가하지 않는다. 공용 이벤트 채널 구독만 사용하여 두 호흡 입력 경로와 모든 호흡 미션에서 동일한 성공음을 보장한다.

## 7. Scene 01/03 배치

대상 씬:

- `Assets/_Project/Scenes/Scene_01_Start.unity`
- `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`

배치 규칙:

1. Scene 01에는 `SoomAudioManager`가 포함된 `SoomAudioRoot`를 추가하고, Scene 03의 기존 `SoomCore/SoomAudioManager`는 재사용한다.
2. 두 컴포넌트 모두 동일한 `SoomAudioLibrary.asset`과 `BreathEventsChannel.asset`을 참조한다.
3. Scene 01에서 시작하면 Scene 01 인스턴스가 `DontDestroyOnLoad`로 계속 사용된다.
4. Scene 02에는 별도 매니저를 추가하지 않으며, 정상 진행에서는 Scene 01의 영속 인스턴스를 계속 사용한다.
5. Scene 01에서 Scene 03으로 넘어갈 때 Scene 03의 중복 인스턴스는 현재 싱글턴 방어로 제거된다.
6. Scene 03부터 에디터에서 직접 실행하면 기존 `SoomCore/SoomAudioManager`가 초기 싱글턴이 된다.

씬 배선을 반복 가능하게 하기 위해 다음 Editor 도구를 추가한다.

- `Assets/_Project/Editor/SoomAudioSetup.cs`
- 메뉴: `SOOM/Audio/Setup Audio Manager In Scenes 01-03`

도구의 책임:

- Library 에셋 생성 또는 기존 에셋 재사용
- Scene 01에 AudioManager 생성, Scene 03의 기존 AudioManager 갱신
- 같은 이름/컴포넌트가 있으면 중복 생성하지 않음
- Library/BreathEvents 참조 연결
- AudioSource의 loop/playOnAwake/2D 설정 적용
- 두 씬 저장 및 결과 요약 출력
- 사용자 지정 AudioClip과 volume은 재실행해도 덮어쓰지 않음

## 8. 수정 파일 범위

### 신규

- `Assets/_Project/Scripts/Audio/SoomAudioLibrarySO.cs`
- `Assets/_Project/Settings/SoomAudioLibrary.asset`
- `Assets/_Project/Editor/SoomAudioSetup.cs`

### 수정

- `Assets/_Project/Scripts/Core/SoomAudioManager.cs`
- `Assets/_Project/Scripts/System/StartSequenceController.cs`
- `Assets/_Project/Scripts/ClueObject.cs`
- `Assets/_Project/Scripts/Fox/FoxEncounterController.cs`
- `Assets/_Project/Scenes/Scene_01_Start.unity`
- `Assets/_Project/Scenes/Scene_03_InGame_Outside.unity`

### 유지

- `PlayerStateManager`의 상태 전이 규칙
- `PlayerMovementDetector`의 이동 판정
- `BreathManager`와 `BreathTiltDriver`의 호흡 판정
- `BreathEventsSO` 이벤트 계약
- `BreathCircleUI` 진행 표시
- 여우 EncounterPhase 전이 및 불안의 막 VFX 로직
- 기존 환경설정 채널 볼륨 UI

## 9. 구현 순서

1. `SoomAudioLibrarySO`와 공용 Library 에셋을 추가한다.
2. `SoomAudioManager`에 Library 참조, 이벤트별 API, 씬/Breath/PlayerState 구독을 구현한다.
3. BGM fade와 fallback/Mixer 양쪽의 채널 볼륨 × 이벤트 gain 계산을 정리한다.
4. Scene 03 전용 BGM과 Move 상태 발걸음 coroutine을 구현한다.
5. 시작 버튼, 최초 메시지 Grab, 여우 유효 UI 동작에 공용 상호작용음을 연결한다.
6. 여우 Reveal/Clear 단계에 각각의 전환 효과음을 연결한다.
7. Editor setup 도구로 Scene 01/03을 배선한다.
8. Inspector에 임시 또는 최종 클립을 지정하고 Editor에서 기능을 검증한다.
9. Quest에서 양안 XR 실행 중 씬 전환, 중복 매니저, 볼륨 및 프레임 성능을 확인한다.

## 10. 검증 계획

### 10.1 싱글턴과 씬 전환

- Scene 01부터 시작했을 때 AudioManager가 하나만 존재한다.
- Scene 03부터 직접 Play해도 기존 AudioManager가 초기화되고 모든 Scene 03 이벤트가 동작한다.
- Scene 01 → 02 → 03 전환마다 AudioSource 또는 이벤트 구독이 중복되지 않는다.
- Scene 03 진입 시 BGM이 한 번만 시작하고 다른 씬으로 나가면 정지한다.

### 10.2 공용 상호작용음

- 시작 버튼의 첫 유효 클릭에서 한 번 재생된다.
- 빠른 중복 Trigger 입력으로 영상 시작과 소리가 중복되지 않는다.
- 접힌 메시지 최초 Grab에서 한 번 재생되고 열린 메시지 재확인 Grab에서는 재생되지 않는다.
- 여우의 호흡 시작, 막 제거 시작, 동료 되기 버튼에서 같은 클립이 각각 한 번 재생된다.

### 10.3 이동음

- Scene 03에서 Idle → Move 진입 후 지정 간격으로 발걸음이 난다.
- 조이스틱을 놓아 Move → Idle이 되면 즉시 새 발걸음 예약이 중지된다.
- Scene 02의 Move 상태에서는 모래 발걸음이 나지 않는다.
- Interact, MissionReady, BreathingActive 상태에서는 발걸음이 나지 않는다.

### 10.4 호흡/여우 효과음

- 호흡 한 사이클 성공마다 `OnBreathLoopCompleted` 한 번당 성공음 한 번이 난다.
- 마지막 성공에서 loop 성공음과 mission success 때문에 같은 성공음이 두 번 나지 않는다.
- 첫 여우 호흡 완료 시 Reveal 효과음이 한 번 난다.
- 두 번째 여우 호흡 완료 시 막 제거 효과음이 한 번 나며 불안의 막 소멸과 타이밍이 일치한다.
- 호흡 취소 또는 실패 경로에서는 완료 효과음이 나지 않는다.

### 10.5 볼륨

- Library의 개별 volume 0에서 해당 소리만 들리지 않는다.
- SFX 채널을 낮추면 interaction, footstep, fox, breath 효과음이 함께 낮아진다.
- BGM 채널은 Scene 03 배경음에만 적용된다.
- Master 0에서는 모든 채널이 음소거되고 기존 값 복구 시 정상 재생된다.
- Mixer 연결/미연결 양쪽에서 개별 gain과 채널 volume이 중복 곱 또는 무시되지 않는다.

## 11. 완료 기준

- 오디오 클립과 이벤트별 상대 볼륨을 `SoomAudioLibrary.asset` Inspector 한 곳에서 관리할 수 있다.
- Scene 01과 Scene 03 어느 곳에서 실행해도 `SoomAudioManager.Instance`가 유효하다.
- 다섯 요구 영역의 모든 재생 시점이 기존 게임 이벤트와 연결되어 있다.
- Scene 03 BGM과 모래 발걸음이 씬/상태 조건에 맞춰 시작·정지한다.
- 호흡 루프 및 여우 단계 효과음이 중복 없이 정확히 한 번 재생된다.
- AudioClip 미지정 상태에서도 NullReferenceException이나 MissingReferenceException이 발생하지 않는다.
- Unity Console에 컴파일 오류가 없고 Scene 01 → 02 → 03 및 Scene 03 직접 실행 경로가 모두 정상이다.

## 12. 이번 단계 결과

이번 단계에서는 기존 오디오 매니저, 씬 배치, 버튼/Grab/여우/호흡/이동 이벤트 연결 지점을 분석하고 구현 계획만 작성한다. 런타임 스크립트, AudioClip, 설정 에셋 및 Scene 01/03 배선은 다음 구현 단계에서 변경한다.
