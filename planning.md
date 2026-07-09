# SOOM 개발 플래닝

> 기능 명세서(2026-07-07) 기준. 완료 시 체크박스에 표시.
> 베이스: `suryeon-breath`(새 아키텍처) → 작업 브랜치 `youjin-breath-2`
> 진행 방식: Fable 5가 오케스트레이션, 실행은 서브에이전트(Sonnet 등)가 수행.
> ⚠️ 구 PoC 레이어(BreathSignalSO/ClarityPoCDriver/SandstormController/SoomSetup 등)는 새 아키텍처에서 삭제됨.
> 구 기반 1차 작업물은 `claude/nice-varahamihira-d3bfec` 브랜치 커밋 a734402에 보존.

## ✅ 완료 — 팀 (suryeon-breath 새 아키텍처)

- [x] 새 아키텍처 골격 `Assets/_Project/Scripts/System/` — 이벤트 채널(`BreathEventsSO`: OnBreathValueNormalized/OnBreathLoopCompleted/OnMissionSuccess), 싱글턴 매니저 패턴
- [x] 호흡 입력·판정 `BreathManager` — 좌우 컨트롤러 회전차 기반 0~1 정규화, 캘리브레이션, 들숨 0.7/날숨 0.3 상태머신, 3회 클리어 → MissionSuccess
- [x] 플레이어 상태머신 `PlayerStateManager` — Idle/Move/Interact/MissionReady/BreathingActive + 전이 가드
- [x] 사물 감지 (명세 3.2) `InteractionDetector` — OverlapSphere + 시야각 필터 → ShowUI/HideUI (기즈모 포함)
- [x] 상호작용 세션 관리 `InteractionManager` + `IInteractable` 인터페이스
- [x] 단서 오브젝트 (명세 3.3 일부) `ClueObject.cs`(HologramMessage) — XRGrabInteractable로 쪽지 잡기 → 확인 → BreathingActive 진입 → 미션 성공 시 등불 스폰 (StartGuiding 호출은 주석 상태)
- [x] 길잡이 등불 이동 (명세 3.4 일부) `GuidingLightController` — Waypoint 순회 이동
- [x] 씬 분리 — Scene_01_Start / Scene_02_InGame_Inside / Scene_03_InGame_Outside + Breath_Test
- [x] 씬 전환 `SOOMSceneManager` — SceneType enum + 비동기 로드 (페이드는 TODO였음 → 오늘 배선)
- [x] 사막 Terrain (명세 3.1 일부) — 8K 모래 텍스처 + Terrain, Scene_03 적용
- [x] 이동 잠금 `LocomotionStaticController`, 입력 `PlayerInputHandler`, 이동 감지 `PlayerMovementDetector`

## ✅ 완료 — 오늘 (구 기반 작업 → 새 아키텍처 포팅)

- [x] VR 페이드 유틸 `ScreenFader` (카메라 부착 쿼드, 1.1·6.2에서 사용) — 포팅 + `SOOMSceneManager` TODO 배선
- [x] 오디오 기반 `SoomAudioManager` — Master/BGM/SFX/Voice, 볼륨 0~100 + PlayerPrefs (AudioMixer 수동 연결 시 믹서 경유, 미연결 시 폴백)
- [x] 공통 원형 호흡 UI `BreathCircleUI` (명세 2.2.1, 콘텐츠 A~E 공용) — 큰/작은 원 아웃라인 + 팽창·수축 원형 아이콘 + 구슬 3슬롯, 새 이벤트 채널 구독으로 재배선
- [x] HMD 기울기(자이로) 대체 입력 `BreathTiltDriver` (명세 2.2.2) — BreathManager와 동일 계약, 같은 채널 발행 (동시 활성화 금지)

※ 전부 에디터 컴파일/Play 검증 대기. 검증 항목은 하단 "빌드/검증" 참조.

## 0. 공통 기반 (남은 것)

- [ ] 호흡 입력 방식 결정 — 컨트롤러 회전(BreathManager) vs HMD 기울기(BreathTiltDriver) 실기기 비교 후 팀 결정
- [ ] AudioMixer 에셋 수동 생성 + SoomAudioManager 연결 (그룹: BGM/SFX/Voice, 노출 파라미터명 MasterVolume/BGMVolume/SFXVolume/VoiceVolume)
- [ ] 안내 음성/SFX 에셋 확보 및 등록

## 1. 스타팅 화면 (Scene_01_Start) — 씬 스텁만 존재

- [ ] 1.1 씬 진입 카메라 페이드 인 (ScreenFader 사용)
- [ ] 1.2.1 메인 UI — 게임 시작
- [ ] 1.2.2 환경 설정 (Recenter / 볼륨(SoomAudioManager) / Continuous↔Snap Turn)
- [ ] 1.2.3 팀 소개 패널
- [ ] 1.2.4 닫기 (Application.Quit)
- [ ] 1.3.1 우주선 추락 컷신 (Timeline + Perlin 카메라 셰이크)
- [ ] 1.3.2 CLI 터미널 로딩 UI (타이핑 효과) — SOOMSceneManager 로딩 TODO와 연계
- [ ] 1.3.3 추락 완료 연출 (탑뷰 + 연기/스파크 파티클)

## 2. 우주선 내부 (Scene_02_InGame_Inside) — 씬 스텁만 존재

- [ ] 2.1.1 기상 연출 (Vignette 핑퐁 + "???별 불시착" UI)
- [ ] 2.1.2 지시문 + 안내 음성
- [ ] 2.2 호흡 캘리브레이션 (콘텐츠 A) — BreathManager/BreathTiltDriver + BreathCircleUI 배선, 성공 안내 UI
- [ ] 2.3 해치 개방 (Rotation 애니메이션 + 빛 연출 + 안내 UI) — 우주선 내부 모델 에셋 필요

## 3. 우주선 외부 (Scene_03_InGame_Outside) — 팀이 상당 부분 진행

- [ ] 3.1 마무리 — 내부→외부 씬 전환 배선 (SOOMSceneManager + 페이드)
- [ ] 3.2 마무리 — 상호작용 UI 문구("UNKNOWN DEVICE DETECTED / Origin: Unknown") World Space UI 프리팹 제작, InteractableDataSO 실제 배선 (현재 참조처 0건)
- [ ] 3.3 마무리 — 트리거 버튼 입력 → InteractionManager.BeginInteraction 연결 (XRI SelectEntered, 현재 미배선), ClueObject를 씬 오브젝트에 실제 부착
- [ ] 3.4 마무리 — 콘텐츠 B에 BreathCircleUI 연결, ClueObject의 StartGuiding 주석 해제 + Waypoint 경로 배치, 지시문 UI

## 4. 인게임 모래바람 구역 — 구 PoC 재이식 필요

- [ ] 모래폭풍 비주얼 재이식 — 구 SandstormController/DustVignette/파티클(a734402 이전 커밋에 있음)을 새 아키텍처에 맞게 이식하거나 재구현 (Clarity → 이벤트 채널 기반으로)
- [ ] 4.1 Box Collider 트리거 존 → 폭풍 시작 + 챕터/퀘스트 UI (PlayerStateManager.SetMissionZone 활용)
- [ ] 4.2 바람 SFX(SoomAudioManager), 등불 빛 강도 Lerp→0 (GuidingLightController에 Light 제어 추가)
- [ ] 4.3 호흡 수행 (콘텐츠 C) — 호흡 루프 완료마다 파티클/Fog 감소 배선

## 5. 여우와의 조우

- [ ] 5.1 동물 감지 — InteractionDetector 재사용, 여우 프리팹 ⚠️ 모델/애니메이션 에셋 확보 필요
- [ ] 5.2 경계 상태 — LookAt 상태 UI + Animator `Sit_Growl`
- [ ] 5.3 호흡 수행 (콘텐츠 D)
- [ ] 5.4 상태 전환 — 상태창 갱신 + 불안의 막 VFX (Fresnel + 노이즈 셰이더)
- [ ] 5.5 호흡 수행 (콘텐츠 E) — 루프당 막 Alpha 1→0, 3회 시 Destroy
- [ ] 5.6 동료 합류 — `Stand_Joy` + NavMeshAgent 플레이어 추종

## 6. 엔딩 화면

- [ ] 6.1 등불 시선 트래킹 (플레이어 위치 고정)
- [ ] 6.2 페이드 아웃 (ScreenFader)
- [ ] 6.3 프로젝트명 + 크레딧 UI

## 빌드 / 검증

- [ ] 오늘 포팅분 에디터 컴파일 확인 + Breath_Test 씬에서 Play 검증 (BreathCircleUI 스케일/구슬, ScreenFader 페이드, SOOMSceneManager 전환)
- [ ] XR Plug-in Management 토글 (Android OpenXR 로더 + Oculus Touch + Meta Quest 기능 그룹) — 수동
- [ ] Quest 실기기 Build & Run 검증
- [ ] 한글 폰트 대응 — 새 브랜치에는 구 폰트 헬퍼가 없음. UI에 한글 텍스트 넣기 전에 Noto Sans KR 등 TMP 폰트 에셋 추가 필요
- [ ] 구 브랜치(claude/nice-varahamihira-d3bfec)의 나머지 자산 중 재활용할 것 검토 (PopupSystem/WorldPopup 등 팝업 시스템)
