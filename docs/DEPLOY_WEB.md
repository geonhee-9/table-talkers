# DEPLOY_WEB.md — Vercel(웹) + Render(시그널링) 배포

목표: `https://xxx.vercel.app/#r=코드` 링크 하나로 아무나 브라우저에서 들어올 수 있게 만들기.

두 조각을 각각 다른 무료 서비스에 올린다:
- **Vercel** — 정적 웹앱(3D 화면). `web/` 폴더를 빌드해서 호스팅.
- **Render** — 시그널링 서버(`web/server/signal.js`). 누가 누구와 연결할지 중개만 하는 초경량 서버.

둘 다 **GitHub 저장소를 통해 자동 배포**된다 (코드를 push하면 알아서 다시 배포됨).

---

## 0단계. GitHub 저장소 만들기 (한 번만)

0-1. https://github.com/new 접속 (로그인 필요, 없으면 무료 가입).
0-2. **Repository name**: `table-talkers` (원하는 이름으로 바꿔도 됨).
0-3. **Public / Private**: 아무거나 (Private 추천 — 아직 미완성 프로젝트라서).
0-4. **"Add a README file" 체크 해제**, **.gitignore 없음**, **license 없음** — 전부 그대로 빈 채로.
0-5. **Create repository** 클릭.
0-6. 다음 화면에 나오는 저장소 주소 복사 (`https://github.com/아이디/table-talkers.git` 형태) →
     이 주소를 Claude Code에게 붙여넣어주면 코드를 push한다.

---

## 1단계. Render에 시그널링 서버 올리기

1-1. https://render.com 가입 (GitHub 계정으로 로그인하면 저장소 연결이 쉬움).
1-2. 대시보드 → **New +** → **Web Service**.
1-3. 방금 만든 GitHub 저장소 선택 → **Connect**.
1-4. 설정값 입력:
   - **Name**: `table-talkers-signal` (아무 이름)
   - **Root Directory**: `web`
   - **Runtime**: Node
   - **Build Command**: `npm install`
   - **Start Command**: `npm run signal`
   - **Instance Type**: **Free**
1-5. **Create Web Service** 클릭 → 배포 시작 (2~3분).
1-6. 완료되면 화면 위쪽에 주소가 뜬다: `https://table-talkers-signal.onrender.com` 같은 형태.
     **이 주소를 복사해두기** (2단계에서 씀).

> ⚠️ 무료 티어 특성: 15분간 아무도 안 쓰면 서버가 잠들고, 다음 접속자가 깨우는 데 30초~1분 걸릴 수 있음.
> 방을 만들 때 "연결 중…"이 좀 오래 걸리면 이 때문 — 정상이다.

---

## 2단계. Vercel에 웹앱 올리기

2-1. https://vercel.com 가입 (역시 GitHub 계정으로).
2-2. 대시보드 → **Add New** → **Project**.
2-3. 같은 GitHub 저장소 선택 → **Import**.
2-4. 설정값:
   - **Framework Preset**: Vite (자동 감지될 것)
   - **Root Directory**: `web` ← **반드시 이걸로 바꿔야 함** (Edit 버튼)
   - **Build Command**: `npm run build` (자동으로 채워짐)
   - **Output Directory**: `dist` (자동)
2-5. **Environment Variables** 펼치기 → 추가:
   - **Key**: `VITE_SIGNAL_URL`
   - **Value**: `wss://table-talkers-signal.onrender.com` ← 1-6에서 복사한 주소에서
     `https://` 를 **`wss://`** 로 바꿔서 입력 (철자 중요!)
2-6. **Deploy** 클릭 → 1~2분 대기.
2-7. 완료되면 `https://table-talkers-xxxx.vercel.app` 같은 링크가 생긴다.

---

## 3단계. 확인

3-1. Vercel이 준 링크를 브라우저로 열기.
3-2. 이름 입력 → 테이블에 앉기.
3-3. 그 링크(주소창 그대로, `#r=코드` 포함)를 **다른 사람에게 보내기** — 그 사람이 클릭만 하면
     같은 방에 들어온다. 계정도, 설치도 필요 없음.

---

## 이후 코드가 바뀌면?

Claude Code가 `git push` 하는 순간, Vercel과 Render **둘 다 자동으로 새로 배포**된다.
사람이 다시 할 일 없음.

## 알려진 한계
- **TURN 서버 없음**: 회사망처럼 엄격한 방화벽 환경에서는 연결이 안 될 수 있음 (나중에 필요시 추가).
- **Render 무료 티어 슬립**: 위 1단계 참고.
- **호스트 이탈 시 방 종료**: 방장이 나가면 그 방은 끝남 (후순위 기능).
