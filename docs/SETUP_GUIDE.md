# SETUP_GUIDE — 에디터에서 할 일 (비개발자용)

코드는 전부 작성돼 있다. 이 문서는 **Unity 에디터에서 사람이 클릭으로 해야 하는 일**을 순서대로 안내한다.
막히면 이 문서의 단계 번호를 Claude Code에게 말하면 된다. (예: "3-2에서 막혔어")

---

## 1단계. 프로젝트 열기 & 부품 설치

1-1. **Unity Hub** 실행 → `Add` → 이 폴더(`table talkers`) 선택 → **Unity 6** 버전으로 열기.
     (처음 열 때 몇 분 걸림. Input System 활성화 팝업이 뜨면 **Yes** → 에디터 재시작됨.)

1-2. 열리면 메뉴 **Window > Package Manager** → 왼쪽 위 `+` → **Install package from git URL** →
     아래 주소를 하나씩 입력해 설치:
   - Facepunch.Steamworks: `https://github.com/Facepunch/Facepunch.Steamworks.git` 가 안 되면,
     [Facepunch.Steamworks 릴리스 페이지](https://github.com/Facepunch/Facepunch.Steamworks/releases)에서 zip을 받아
     `Assets/Plugins/Facepunch/` 폴더에 압축 해제(Windows용 DLL 포함).
   - NGO Facepunch Transport: `https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch`

1-3. **콘솔 확인** (Window > Panels > Console): 빨간 에러가 없으면 성공.
     빨간 에러가 있으면 → 전부 복사해서 Claude Code에 붙여넣기. (코드 수정은 에이전트 몫)

## 2단계. 설정 파일(에셋) 만들기

2-1. Project 창에서 `Assets/TableTalkers/` 안에 우클릭 → **Create > TableTalkers > Room Config**
     → 이름 그대로 두기. (좌석 4개 기본값 그대로 OK)
2-2. 같은 방법으로 **Create > TableTalkers > Steam Config** 생성. (App ID 0 = 테스트 번호 480 자동 사용)
2-3. 같은 방법으로 **Create > TableTalkers > Voice Config** 생성.
2-4. 같은 방법으로 **Create > TableTalkers > Moderation Config** 생성. (신고 서버 주소는 나중에)
2-5. 같은 방법으로 **Create > TableTalkers > Ops Config** 생성. (통계/법무 링크 주소는 나중에)

## 3단계. Boot 씬 만들기

3-1. **File > New Scene** → 저장하며 이름 `Boot` (위치: `Assets/TableTalkers/Scenes/` 폴더 만들어 저장).
3-2. Hierarchy 창에서 우클릭 → **Create Empty** → 이름 `App` → 이 오브젝트에 아래 컴포넌트를
     전부 추가 (Inspector의 **Add Component** 버튼에서 이름 검색):
   - `AppEntry`, `SessionController`
   - `SteamLobby`, `SteamDlc`
   - `NetworkSessionService`
   - `SteamVoiceService`, `OdinVoiceService`, `VoiceServiceSelector`, `VoiceInputController`
   - `BlockController`, `ReportClient`, `HostModeration`
   - UI 패널들: `MicSettingsPanel`, `ModerationPanel`, `ChatPanel`, `OnboardingPanel`, `SettingsPanel`, `DebugOverlay`
   - `AnalyticsClient`, `RemoteConfigClient`
3-3. 또 **Create Empty** → 이름 `NetworkManager` → **NetworkManager** 컴포넌트 추가
     → 같은 오브젝트에 **Facepunch Transport** 컴포넌트 추가
     → NetworkManager의 `Network Transport` 칸에 방금 추가한 Facepunch Transport를 드래그.
3-4. `App` 오브젝트의 Inspector에서 빈 칸 채우기 (드래그로):
   - SessionController: App Entry / Steam Lobby / Network / Voice Selector / Host Moderation / Dlc ← 전부 `App`(자기 자신), Room Config←2-1 에셋
   - SteamLobby, SteamDlc: Config←2-2 에셋
   - SteamVoiceService, OdinVoiceService: Config←2-3 에셋
   - VoiceServiceSelector: Config←2-3 에셋, Steam←`App`의 SteamVoiceService, Odin←`App`의 OdinVoiceService
   - NetworkSessionService: Config←2-1 에셋
   - ReportClient: Config←2-4 에셋
   - ModerationPanel: Report Client / Host Moderation←`App`
   - AnalyticsClient, RemoteConfigClient, SettingsPanel, OnboardingPanel: Config←2-5 에셋

## 4단계. Room 씬 만들기 (코지 라운지는 나중에 — 지금은 회색 박스로 OK)

4-1. **File > New Scene** → 저장하며 이름 `Room`.
4-2. 원탁: Hierarchy 우클릭 → **3D Object > Cylinder** → 이름 `Table` →
     Position (0, 0.4, 0), Scale (1.5, 0.4, 1.5).
4-3. 바닥: **3D Object > Plane** → Position (0,0,0).
4-4. 좌석 앵커 4개: **Create Empty** 를 4번 → 이름 `Seat0`~`Seat3` →
     테이블 주위 4방향에 배치하고 **각각 테이블 중심을 바라보게 회전**:
   - Seat0: Position (0, 0.55, -1.6), Rotation (0, 0, 0)
   - Seat1: Position (1.6, 0.55, 0), Rotation (0, -90, 0)
   - Seat2: Position (0, 0.55, 1.6), Rotation (0, 180, 0)
   - Seat3: Position (-1.6, 0.55, 0), Rotation (0, 90, 0)
4-5. **Create Empty** → 이름 `SeatAnchors` → `SeatAnchorRegistry` 컴포넌트 추가 →
     Anchors 리스트 크기 4 → Seat0~3을 순서대로 드래그.
4-6. 씬에 기본으로 있는 **Main Camera 삭제** (플레이어 프리팹의 카메라를 쓸 것).

## 5단계. 플레이어 프리팹 만들기

5-1. Hierarchy 우클릭 → **Create Empty** → 이름 `Player`.
5-2. `Player`에 컴포넌트 추가: `NetworkObject`, `NetworkParticipant`, `HeadOrientationSync`,
     `SeatedFirstPersonController`, `PingProbe`, `EmoteSync`, `ChatChannel`.
5-3. 임시 아바타: `Player` 우클릭 → **3D Object > Capsule** → 이름 `Body` → Position (0, 0.3, 0), Scale (0.4, 0.5, 0.4).
     `Player` 우클릭 → **3D Object > Sphere** → 이름 `Head` → Position (0, 1.0, 0), Scale (0.35, 0.35, 0.35).
5-4. `Head` 오브젝트에 컴포넌트 추가: `SpeakingIndicator`, `Nameplate`, `AmplitudeLipsync`.
5-5. `Player`의 `HeadOrientationSync` → Head Bone 칸에 `Head` 드래그.
5-6. `Player` 밑에 우클릭 → **Camera** 추가 → `PlayerCamera` 컴포넌트 추가 → Controller 칸에 `Player`의
     `SeatedFirstPersonController` 드래그. **Audio Listener는 이 카메라에만** 있어야 함.
5-7. `Player` 오브젝트를 Project 창의 `Assets/TableTalkers/` 폴더로 **드래그** → 프리팹 됨 →
     Hierarchy에 남은 `Player`는 삭제.
5-8. Boot 씬으로 돌아가서 `NetworkManager`의 **Default Player Prefab** 칸에 이 프리팹 드래그.

## 6단계. 빌드 설정 & 실행

6-1. **File > Build Profiles**(또는 Build Settings) → Boot 씬과 Room 씬을 **Add Open Scenes** 로 등록 (Boot이 첫 번째).
6-2. **Steam 클라이언트를 로그인 상태로 실행**해 두기.
6-3. 에디터에서 Boot 씬 열고 ▶️ Play → 왼쪽 위 "Create Room" 버튼 → 우측 상단 디버그 창(F1)에
     자기 이름이 좌석 0으로 뜨면 **1차 성공**.

## 7단계. 진짜 테스트 (M0 게이트) — 컴퓨터 2대 + Steam 계정 2개

7-1. **File > Build** 로 Windows 빌드 생성 → 두 번째 컴퓨터에 복사.
7-2. A 컴퓨터: 방 만들기 → "Invite Friends" → Steam 친구인 B 초대.
7-3. B 컴퓨터: Steam 알림으로 초대 수락 → 자동 입장.
7-4. ✅ 서로의 아바타가 보이고, 고개를 돌리면 상대 화면에서 내 머리가 움직이고, 음성 대화가 되면 **M0 통과.**

---

## 조작키 정리
| 키 | 기능 |
|---|---|
| 마우스 | 고개 돌리기 (Esc: 마우스 해제, 클릭: 다시 잠금) |
| F1 | 디버그 창 (지연시간·참가자·발화) |
| M | 마이크 설정 창 (PTT/음성감지, 상대별 볼륨·뮤트) |
| V (누르고 있기) | Push-to-talk (설정에서 PTT 모드일 때) |
| 1~5 | 이모트 (끄덕임/웃음/손들기/엄지/박수) — 애니메이션 클립은 나중에 |
| Tab | 텍스트 채팅 |
| K | 안전 창 (차단·신고, 호스트: 강퇴·방 잠금) |
| O | 설정 창 (이름표·법무 링크) |

## 첫 실행 시
처음 켜면 **마이크 체크 창**이 자동으로 뜬다 (장치 확인 → 말해보기 → 완료). 한 번 완료하면 다시 안 뜸.

## 알려진 한계 (MVP라서 의도된 것)
- 아바타 = 캡슐+공 (프리셋 아바타는 다음 단계)
- UI = 임시 회색 패널 (진짜 UI는 다음 단계)
- 음성은 비공간(모두 같은 볼륨) — ODIN 넣으면 설정 하나로 공간 음성 전환 (자리는 마련됨)
- 신고/통계/법무 링크는 **주소만 비워져 있음** — 서버·문서가 생기면 Config 에셋에 주소만 입력
- Pro DLC는 스팀 파트너 등록 후 Steam Config에 DLC 번호 입력하면 활성화
- 웹 브라우저 게스트 접속 — 서버 비용 문제와 함께 MVP 이후 논의
