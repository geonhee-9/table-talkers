# CLAUDE.md — Table Talkers

이 파일은 Claude Code가 매 세션 읽는 프로젝트 규칙이다. 짧고 강제적으로 유지한다.
제품/설계 상세는 `docs/table-talkers-design-doc.md`, 실행 태스크는 `docs/BUILD_PLAN.md` 참조.

---

## 프로젝트

3D 공간에서 원탁에 둘러앉아 실시간으로 **대화**하는 소셜 시뮬레이터. PC / Steam. 게임 메커닉 없음.
핵심 가치 = **존재감 있는 실시간 대화.**

## 핵심 원칙 (코드 결정을 제약하는 것만)

1. **대화가 제품이다.** 스코어·승패·미니게임을 추가하지 않는다.
2. **존재감 > 그래픽 충실도.** "누가 누구를 보는지 / 말하는지 / 웃었는지"가 최우선.
3. **지연이 최우선 지표.** 음성/네트워크 결정은 mouth-to-ear 지연(목표 ≤150–200ms)을 기준으로 판단.
4. **입장은 무료.** 게스트가 결제 없이 방에 들어오는 경로를 절대 막지 않는다.
5. **전용 서버 없음.** host-authoritative(listen server) + Steam Relay. 클라우드 상시 서버를 전제한 코드를 만들지 않는다.
6. **초대제 우선.** 공개방/발견 기능은 지금 만들지 않는다.

## 기술 스택 (확정 + 기본값)

- 엔진: **Unity 6** (C#)
- 네트워킹: **Netcode for GameObjects (NGO)** — 기본값. FishNet으로 교체 가능하나 `Networking/` 안에 격리.
- 전송: **Steam Relay** via **Facepunch.Steamworks** + NGO **Facepunch Transport**
- 로비/초대/DLC: **Steam** (Lobbies, Invite, DLC ownership)
- 음성: **M0–M1 = Steam 내장 음성** / **v1 = ODIN**(공간). 반드시 `IVoiceService` 뒤에 숨긴다(아래).
- 아바타/립싱크: 프리셋 → Ready Player Me / uLipSync(viseme). M0는 진폭 기반.

> 스택의 열린 선택(NGO vs FishNet, ODIN vs Vivox)은 위 기본값으로 진행한다. 교체가 필요하면 해당 폴더만 바꾸도록 추상화가 되어 있어야 한다.

## 아키텍처 불변식 (MUST — 위반 금지)

- **참가자 추상화.** 좌석을 채우는 주체는 `IParticipant`로만 다룬다. 지금은 `HumanParticipant`만 구현. **참가자 = 사람이라는 가정을 코드에 박지 말 것**(미래에 에이전트가 좌석을 채운다).
- **음성 추상화.** 게임플레이·존재감 코드는 `IVoiceService`에만 의존한다. Steam Voice / ODIN은 그 구현체일 뿐. **어떤 gameplay 코드도 Steam Voice API를 직접 호출하지 않는다**(v1에서 ODIN으로 무리 없이 교체하기 위함).
- **방 오브젝트 추상화.** 방을 "아바타 + 테이블"로 하드코딩하지 않는다. 방은 참석자 외 **공유 표면 오브젝트**(미래: 화면/문서/보드)를 담을 수 있는 컬렉션으로 모델링한다.
- **host-authoritative.** 방의 진실 소스는 호스트. 게스트는 입력/상태를 보고하고 보간한다.
- **두 개의 독립 경로.** 상태 동기화(NGO→Steam Relay)와 음성(IVoiceService)은 서로 분리. 섞지 않는다.
- **동기화 상태는 최소.** 좌석 인덱스 · 헤드 오리엔테이션 · (선택)상체/손 트랜스폼 · 애니메이션 상태 · 발화 여부 · 아바타 외형 · 이모트 이벤트. 그 외를 네트워크로 보내지 않는다.

### 절대 하지 말 것 (지금 범위 밖)
녹화·전사 / 공개방·발견 / Team·Enterprise / 구독 / 화면공유·공유문서(표면만 열어두고 구현은 안 함) / 전용 서버 / 안티치트 / 자리 이동 기반 이동 로직(착석 고정).

### 처음부터 넣을 것 (나중에 끼워넣기 어려움)
`IVoiceService`에 **뮤트 · 개인볼륨 · 발화레벨 조회**를 초기부터 포함. 지연을 측정하는 훅(디버그 오버레이)을 초기부터.

## 코드 구조 & 컨벤션

```
Assets/TableTalkers/Scripts/
  Core/          # IParticipant, HumanParticipant, IRoomObject, Session, SeatManager
  Networking/    # NGO 설정, Steam Facepunch Transport, NetworkSync 컴포넌트
  Voice/         # IVoiceService, SteamVoiceService (v1: OdinVoiceService)
  Presence/      # HeadOrientationSync, Lipsync, SpeakingIndicator, Emotes, LookAt
  Player/        # SeatedFirstPersonController, PlayerCamera
  UI/            # LobbyUI, InRoomHUD, Onboarding, MicSettings, TextChat
  Moderation/    # MuteController, BlockController, ReportClient
  Platform/      # SteamLobby, SteamInvite, SteamDlc (Pro 소유권 게이팅)
  Bootstrap/     # AppEntry, SceneFlow
Packages/manifest.json
```

- 네임스페이스는 폴더와 일치: `TableTalkers.Core`, `TableTalkers.Networking` …
- 폴더별 **Assembly Definition(.asmdef)** 로 컴파일 격리. 순환 참조 금지(Core는 아무 것도 참조 안 함).
- C#: PascalCase(타입/메서드/프로퍼티), camelCase(지역/파라미터), `_camelCase`(private 필드). `var`는 타입이 자명할 때만.
- MonoBehaviour는 얇게. 로직은 순수 C# 클래스/서비스로 빼고 테스트 가능하게.
- 하드코딩 매직넘버 금지 → ScriptableObject 설정(정원, 좌석 수, 틱레이트 등).
- 주석/식별자는 영어. 대화용 UI 텍스트는 로컬라이즈 키로(하드코딩 금지).

## 작업 분담: Claude Code vs 사람

**Claude Code가 하는 것(코드 레이어):**
C# 스크립트·인터페이스·서비스·netcode 컴포넌트 작성, 폴더/asmdef/manifest 구성, SDK 호출 코드, 에디터 툴링 스크립트, 상태 동기화 로직, UI 로직, ScriptableObject 정의.

**사람이 하는 것(에디터/웹/하드웨어):**
Unity Editor에서 씬 구성·프리팹 배선·컴포넌트 참조 할당·패키지 임포트, Steam 파트너 사이트에서 App ID/DLC 설정, ODIN/Vivox 대시보드 크리덴셜, 아트·아바타·오디오 에셋, **두 대 이상에서의 멀티플레이어 플레이테스트**.

각 태스크에서 사람이 할 일은 `BUILD_PLAN.md`에 `🔧 사람:` 으로 표시돼 있다. Claude Code는 그 단계를 코드로 하려 시도하지 말고, 필요한 프리팹/씬/참조가 무엇인지 명확히 알려줄 것.

## 워크플로우 & 완료 기준 (DoD)

- `BUILD_PLAN.md`를 **위에서부터 태스크 단위**로 진행. 한 번에 한 태스크.
- 태스크 완료 = 컴파일 통과 + 컨벤션 준수 + 불변식 위반 없음 + 해당 태스크의 "완료 기준" 충족.
- 태스크마다 커밋. 커밋 메시지에 태스크 번호.
- 애매하면 추측 말고 질문. 특히 아키텍처 불변식과 충돌하는 요구는 멈추고 확인.
- Unity 에디터가 필요한 검증은 직접 못 하므로, "사람이 에디터에서 확인할 것"을 명시하고 넘긴다.
