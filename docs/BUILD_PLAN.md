# BUILD_PLAN.md — Table Talkers

Claude Code에 **한 번에 한 태스크씩** 붙여넣어 진행한다. 각 태스크는 그대로 복사해 명령으로 쓸 수 있다.
규칙·스택·불변식은 `CLAUDE.md` 참조. `🔧 사람:` = 에디터/웹/하드웨어에서 사람이 하는 단계(에이전트가 코드로 시도하지 말 것).

**사용법**
1. `CLAUDE.md`를 리포 루트에, 이 파일과 설계 문서를 `docs/`에 둔다.
2. Phase A → B → C → D 순서로, 태스크를 위에서부터 하나씩 Claude Code에 지시.
3. 에이전트가 코드를 쓰면 → `🔧 사람:` 단계를 에디터에서 처리 → 커밋 → 다음 태스크.
4. 막히거나 불변식과 충돌하면 에이전트에게 "멈추고 옵션을 제시하라"고 지시.

---

## Phase A — 스캐폴딩 (전부 에이전트 가능)

### A1. 프로젝트 뼈대 생성
```
CLAUDE.md의 "코드 구조 & 컨벤션"에 정의된 폴더 구조를 Assets/TableTalkers/Scripts/ 아래에 생성해줘.
각 폴더에 네임스페이스가 일치하는 Assembly Definition(.asmdef)을 만들고, 순환 참조가 없도록
참조 방향을 정리해줘(Core는 아무것도 참조하지 않음). 각 폴더에 빈 README나 placeholder는 만들지 말고,
실제 스크립트가 들어갈 때까지 asmdef만 둬. Packages/manifest.json에 Netcode for GameObjects를
추가하는 항목을 기입해줘(버전은 최신 안정 버전으로).
```
- **완료 기준:** 폴더/asmdef 생성, manifest에 NGO 항목, 순환 참조 없음.
- 🔧 사람: Unity에서 프로젝트 열어 패키지 복원 확인. Facepunch.Steamworks(Package Manager Git URL 또는 릴리스)와 NGO Facepunch Transport를 임포트.

### A2. 핵심 추상화 (불변식의 코드화)
```
Core/에 다음 인터페이스와 지금 필요한 구현만 만들어줘. 불변식은 CLAUDE.md 참조.

- ParticipantKind { Human, Agent }
- IParticipant: Id(string), SeatIndex(int), DisplayName(string), Kind, IsLocal(bool),
  IsSpeaking(bool), HeadOrientation(Quaternion)
- HumanParticipant: IParticipant 구현 (지금은 이것만)
- IRoomObject: Id(string) 만. (미래 공유 표면용 마커. 지금 구현체는 없음)
- SeatManager: 좌석 수 기반으로 참가자↔좌석 배정/해제, 빈 좌석 조회

Voice/에:
- IVoiceService: JoinAsync(roomId), LeaveAsync(), SetInputEnabled(bool),
  SetPeerVolume(participantId, float01), SetPeerMuted(participantId, bool),
  GetPeerSpeakingLevel(participantId)->float, IsSpatial(bool)
  (구현체는 다음 태스크에서. 지금은 인터페이스만)

좌석 수/정원 같은 값은 ScriptableObject(RoomConfig)로 빼줘.
```
- **완료 기준:** 인터페이스/HumanParticipant/SeatManager/RoomConfig 컴파일 통과. 참가자=사람 가정이 IParticipant 밖으로 새지 않음.

### A3. 앱 진입 & 씬 플로우 스켈레톤
```
Bootstrap/에 AppEntry와 SceneFlow를 만들어줘. 씬 전환 상태(부트 → 아바타/닉네임 →
로비 → 인룸)를 관리하는 얇은 상태기계. 실제 씬 로드는 이름 상수로 참조하고,
씬 자체는 사람이 에디터에서 만들 것을 주석으로 명시해줘. 각 상태 진입/이탈 훅만 비워둬.
```
- **완료 기준:** 상태기계 컴파일, 씬 이름 상수 정의.
- 🔧 사람: 에디터에서 Boot / Room 씬 생성, Build Settings에 등록.

---

## Phase B — M0: "두 대에서 서로 보며 대화" (에이전트 + 사람 혼합)

목표: 실제 두 대에서 Steam 릴레이 경유로 두 사람이 원탁에 앉아 서로 고개 방향이 보이고 음성 대화.

### B1. Steam 초기화 & 로비 래퍼
```
Platform/에 SteamLobby를 만들어줘. Facepunch.Steamworks로 Steam 초기화,
로비 생성(정원 지정)·참가·퇴장·초대 콜백을 감싸는 얇은 API.
로비 멤버 변경 이벤트를 상위(세션)로 노출. Steam App ID는 코드에 하드코딩하지 말고
설정(예: steam_appid.txt/ScriptableObject)에서 읽게 해줘. Steam 미실행 환경에서
크래시하지 않게 가드.
```
- **완료 기준:** 로비 생성/참가/초대 API, App ID 외부화, 미실행 가드.
- 🔧 사람: Steam 파트너에 앱 등록해 App ID 확보, `steam_appid.txt` 배치, Steam 클라이언트 로그인 상태에서 테스트.

### B2. NGO + Steam Relay 전송 연결
```
Networking/에서 NGO를 Facepunch Transport(Steam Relay)로 구동하도록 세팅해줘.
호스트/게스트 시작 흐름을 SteamLobby와 연결: 로비 생성자가 NGO 호스트가 되고,
로비 참가자가 그 호스트에 릴레이로 접속. host-authoritative. 연결/해제 이벤트를
세션 계층으로 노출. 틱/전송률 관련 상수는 RoomConfig로.
```
- **완료 기준:** 두 클라이언트가 Steam 릴레이로 NGO 세션 형성(로컬 IP 아님).
- 🔧 사람: 두 대(또는 Steam 계정 2개)에서 실제 연결 확인.

### B3. 착석 1인칭 컨트롤러 & 카메라
```
Player/에 SeatedFirstPersonController와 PlayerCamera를 만들어줘. 이동 없음(착석 고정).
마우스/스틱으로 고개 자유 회전(yaw/pitch 클램프). 카메라는 착석 위치에 고정.
로컬 플레이어의 head yaw/pitch(=HeadOrientation)를 프레임마다 노출.
좌석 위치/회전은 SeatManager가 배정한 앵커를 따르게.
```
- **완료 기준:** 로컬에서 앉은 시점으로 고개 회전, HeadOrientation 값 갱신.
- 🔧 사람: 원탁+의자+좌석 앵커(빈 Transform) 프리팹/씬 배치. 플레이어 프리팹에 컨트롤러 부착.

### B4. 헤드 오리엔테이션 & 좌석 네트워크 동기화 (존재감 핵심)
```
Presence/에 HeadOrientationSync를 NGO 컴포넌트로 만들어줘. 각 참가자의 head yaw/pitch를
10~20Hz로 동기화하고 원격 측에서 부드럽게 보간해 아바타 머리에 적용. 좌석 인덱스도 동기화.
동기화 데이터는 CLAUDE.md의 "동기화 상태 최소" 목록을 넘지 말 것.
NetworkParticipant(=IParticipant를 네트워크 상에서 대표)를 만들어 로컬/원격을 구분.
```
- **완료 기준:** 한 클라이언트에서 고개를 돌리면 다른 클라이언트의 해당 아바타 머리가 따라 움직임.
- 🔧 사람: 아바타 프리팹(머리 본 포함)과 NetworkObject 등록, HeadOrientationSync 참조 배선.

### B5. Steam 내장 음성 → IVoiceService 구현
```
Voice/에 SteamVoiceService : IVoiceService를 만들어줘. Steam 음성 API로 캡처/재생,
SetInputEnabled(뮤트/PTT), SetPeerMuted, SetPeerVolume, GetPeerSpeakingLevel을 구현.
IsSpatial=false. 어떤 gameplay/presence 코드도 Steam 음성 API를 직접 부르지 않고
IVoiceService만 쓰게 유지해줘. 발화 여부(IsSpeaking)는 GetPeerSpeakingLevel 임계값으로 산출해
NetworkParticipant에 반영.
```
- **완료 기준:** 두 클라이언트가 서로의 음성을 듣고, 뮤트/개인볼륨이 동작. 발화 시 IsSpeaking=true.
- 🔧 사람: 실제 마이크로 양방향 음성 확인.

### B6. M0 통합 & 디버그 오버레이
```
Bootstrap 흐름을 이어붙여: 로비 생성/참가 → NGO 릴레이 세션 → 착석 → 헤드 동기화 → 음성.
디버그 오버레이(토글 키)에 핑/왕복 지연, 참가자 수, 각 참가자 IsSpeaking을 표시해줘.
지연은 상시 볼 수 있어야 한다(CLAUDE.md 지연 원칙).
```
- **완료 기준(M0 게이트):** 두 대에서, 릴레이 경유로, 서로 앉아 고개 방향이 보이고 대화 가능. 지연 오버레이 표시.
- 🔧 사람: 두 대 플레이테스트로 M0 성공 판정.

---

## Phase C — M1: 자연스러운 4인 대화

### C1. 진폭 립싱크
```
Presence/에 Lipsync를 만들어줘. IVoiceService.GetPeerSpeakingLevel(그리고 로컬 마이크 진폭)로
아바타 턱/입을 여닫는다. viseme는 나중(교체 가능하게 인터페이스로).
```
- 🔧 사람: 아바타 입/턱 본 또는 블렌드셰이프 지정.

### C2. 발화 표시 & 이름표
```
Presence/SpeakingIndicator와 UI/Nameplate: 발화 중이면 이름표/아바타에 은은한 하이라이트.
이름표 상시표시 옵션. 0.1초 내 반응.
```

### C3. 이모트/제스처
```
Presence/Emotes: 끄덕임/웃음/손 들기/엄지/박수를 이벤트(RPC)로 동기화하고 애니메이션 트리거.
일회성 이벤트로 처리(상태 동기화 아님).
```
- 🔧 사람: 이모트 애니메이션 클립/트리거 세팅.

### C4. 마이크 설정 UI (PTT/개인볼륨/뮤트)
```
UI/MicSettings: 입력 장치 선택, PTT vs 음성감지 토글, 참가자별 개인볼륨/뮤트 UI.
IVoiceService에만 의존. 설정은 로컬 저장.
```

### C5. 기본 재접속
```
Networking: 일시 끊김 후 같은 좌석으로 복귀하고 상태 재동기화하는 흐름. 호스트 마이그레이션은 M2.
```
- **완료 기준(M1 게이트):** 4명이 15분 이상 자연스럽게 대화(립싱크·발화표시·이모트·개인볼륨 동작).

---

## Phase D — M2: 출시 준비 (v1)

### D1. 공간 음성 = ODIN (IVoiceService 교체)
```
Voice/에 OdinVoiceService : IVoiceService를 추가해줘. 3D 공간 음성으로 아바타 위치 기반
어테뉴에이션. gameplay/presence 코드는 손대지 말 것 — IVoiceService 구현체 교체만으로 되게.
IsSpatial=true. 런타임/설정으로 SteamVoiceService↔OdinVoiceService 선택 가능하게.
```
- 🔧 사람: ODIN 대시보드 크리덴셜/설정, 실제 공간감 튜닝.

### D2. 오디오 품질 (필수)
```
Voice: 에코 캔슬(AEC)·노이즈 억제·자동 게인(AGC) 적용/노출. 하울링/노이즈 케이스 대응.
(ODIN 제공 기능 우선 활용, 부족분 보완)
```

### D3. 모더레이션 + 신고 수신 백엔드
```
Moderation/: 차단(Block, 이후 상호 비노출/비가청)과 신고(Report)를 구현하고,
ReportClient가 경량 백엔드 엔드포인트로 신고를 전송하게 해줘(엔드포인트 URL은 설정에서).
호스트 킥/방 잠금도. "신고 버튼만 있고 뒤가 없음"은 금지.
```
- 🔧 사람: 신고 수신용 경량 백엔드(엔드포인트/큐) 마련.

### D4. 기본 텍스트 채팅
```
UI/TextChat: 방 내 텍스트 채널(링크 공유·조용한 참여). NGO RPC로 동기화, 로컬라이즈 키 사용.
```

### D5. 온보딩
```
UI/Onboarding: 첫 실행 시 마이크 권한 요청 → 장치 선택 → 마이크 테스트 → "다들 들려요?" 체크.
최초 1회, 이후 스킵. 이탈 최소화가 목표.
```

### D6. 호스트 마이그레이션 & 버전 게이트
```
Networking: 호스트 이탈 시 다른 클라이언트로 권한 이양하고 세션 유지.
그리고 P2P라 같은 버전끼리만 접속하도록 버전 게이트/강제 업데이트 안내를 추가해줘.
```

### D7. Pro DLC 소유권 게이팅
```
Platform/SteamDlc: 호스트가 Pro DLC를 소유하면 방 전체를 격상(정원 상향, 시간 무제한,
예약/링크 초대, 커스터마이즈 잠금 해제). 소유권은 Steam으로 확인. 코스메틱 소유도 조회.
게스트는 결제 없이 격상된 방 이용 가능.
```
- 🔧 사람: Steam 파트너에서 Pro DLC/코스메틱 상품 설정.

### D8. 애널리틱스 & 크래시 & 리모트 컨피그
```
경량 백엔드로 세션 길이/방 크기/크래시, 그리고 Pro 전환 퍼널 이벤트를 전송해줘.
리모트 컨피그로 정원/기능 플래그를 원격 조절 가능하게. 엔드포인트/키는 설정에서.
```
- 🔧 사람: 애널리틱스/크래시 서비스 선정·연동 크리덴셜.

### D9. 법무 링크 & 로컬라이즈 마감
```
UI: 개인정보처리방침/이용약관/EULA 링크를 온보딩·설정에 노출. 하드코딩된 UI 문자열이
남아있지 않게 로컬라이즈 키로 마감.
```
- 🔧 사람: 개인정보처리방침·약관·EULA 문서 작성(PIPA/GDPR), Steam 상점 페이지/도전과제.

- **완료 기준(M2 게이트):** 낯선 사람이 튜토리얼 없이 초대 방에 들어와 대화하고, 문제 참석자를 스스로 차단하고, 호스트가 Pro를 사서 방을 격상할 수 있다.

---

## 진행 중 에이전트에게 상기시킬 것
- 불변식(참가자/음성/방 오브젝트 추상화, host-authoritative, 두 경로 분리)과 충돌하면 멈추고 옵션 제시.
- Unity 에디터 검증이 필요한 부분은 코드로 우기지 말고 `🔧 사람:` 단계로 넘길 것.
- 태스크 단위 커밋, 지연 오버레이는 항상 살아있게.
