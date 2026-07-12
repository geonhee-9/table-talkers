// App entry: join flow → scene + network + voice, one render loop.
import * as THREE from 'three';
import { participants, type Participant } from './core/participants';
import { seatLayout } from './core/seats';
import { buildLounge } from './scene/Lounge';
import { Avatar } from './scene/Avatar';
import { SeatedCamera } from './player/SeatedCamera';
import { RoomNetwork } from './net/room';
import { WebRtcVoice } from './voice/voice';
import * as hud from './ui/hud';

const app = document.getElementById('app')!;
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(innerWidth, innerHeight);
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
app.appendChild(renderer.domElement);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(65, innerWidth / innerHeight, 0.05, 60);
buildLounge(scene);

const seated = new SeatedCamera(camera, renderer.domElement);
seated.preview();

window.addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});

// ---- Room id from URL hash (#r=code); creating a table generates one. ----
function roomIdFromHash(): string | null {
  const m = location.hash.match(/r=([a-z0-9]+)/i);
  return m ? m[1] : null;
}

function newRoomId(): string {
  return Math.random().toString(36).slice(2, 8);
}

// ---- Avatars follow the participant registry. ----
const voice = new WebRtcVoice();
const avatars = new Map<string, Avatar>();

participants.onAdded.add((p: Participant) => {
  const avatar = new Avatar(p, voice);
  if (p.isLocal) avatar.setFirstPersonView();
  avatars.set(p.id, avatar);
  scene.add(avatar.group);
  if (!p.isLocal) hud.toast(`${p.name || '누군가'} 참석`);
});

participants.onRemoved.add((p: Participant) => {
  const avatar = avatars.get(p.id);
  if (avatar) {
    scene.remove(avatar.group);
    avatars.delete(p.id);
  }
  hud.toast(`${p.name || '누군가'} 퇴장`);
});

// ---- Join flow ----
let net: RoomNetwork | null = null;
hud.setupDebugToggle();

// Autoplay policy: any first gesture wakes the audio graph (needed after auto-rejoin).
document.addEventListener('pointerdown', () => voice.resumeAudio(), { once: true });

const enterBtn = document.getElementById('enter') as HTMLButtonElement;
const enterNoMicBtn = document.getElementById('enterNoMic') as HTMLButtonElement;

function setJoinBusy(busy: boolean, label = '테이블에 앉기'): void {
  enterBtn.disabled = busy;
  enterNoMicBtn.disabled = busy;
  enterBtn.textContent = busy ? label : '테이블에 앉기';
}

async function joinRoom(withMic: boolean): Promise<void> {
  const name = (document.getElementById('name') as HTMLInputElement).value.trim() || 'Guest';
  localStorage.setItem('tt.name', name);
  hud.showJoinError('');
  voice.initAudio(); // user gesture — lets us hear peers even without a mic

  if (withMic) {
    setJoinBusy(true, '마이크 확인 중…');
    try {
      await voice.requestMic();
    } catch (err) {
      console.error('[TableTalkers] getUserMedia failed:', err);
      const reason = err instanceof Error ? err.name : 'UnknownError';
      hud.showJoinError('micDenied', reason);
      setJoinBusy(false);
      return; // let the user retry or use "마이크 없이 둘러보기"
    }
  }

  const roomId = roomIdFromHash() ?? newRoomId();
  location.hash = `r=${roomId}`;

  net = new RoomNetwork(roomId, name, voice);
  net.onSeated = () => seated.sit();
  net.onHostLeft = () => {
    hud.toast(hud.loc('hostLeft'));
    net?.leave();
    net = null;
    seated.preview();
    location.hash = '';
    hud.showJoin(null);
  };

  net.onEmote = (id, kind) => avatars.get(id)?.playEmote(kind);
  net.onChat = (id, text) => {
    const name = participants.get(id)?.name || '?';
    appendChatLine(name, text);
  };
  net.onRoomFull = () => hud.toast('자리가 다 찼어요 — 구경만 할 수 있어요.');
  net.onVersionSkew = () =>
    hud.toast('상대방 앱이 예전 버전이에요 — 양쪽 모두 새로고침하면 해결됩니다.');
  net.setPoseSource(() => ({
    yaw: seated.yaw, pitch: seated.pitch, leanFwd: seated.leanFwd, leanRight: seated.leanRight,
  }));

  setJoinBusy(true, '방 연결 중…');
  try {
    await net.connect((attempt, max) => {
      setJoinBusy(true, `서버 깨우는 중… (${attempt}/${max})`);
    });
  } catch (err) {
    console.error('[TableTalkers] signaling connect failed:', err);
    hud.showJoinError(err instanceof Error && err.message === 'room-full' ? 'roomFull' : 'signalDown');
    net = null;
    setJoinBusy(false);
    return;
  }

  hud.hideJoin();
  hud.showRoomPanel(roomId, voice.hasMic, () => {
    localStorage.removeItem('tt.micPref'); // explicit leave — next visit asks again
    net?.leave();
    location.hash = '';
    location.reload();
  });
  // Remember how we joined so a refresh can rejoin silently (no popups, no clicks).
  localStorage.setItem('tt.micPref', voice.hasMic ? 'mic' : 'nomic');
  document.getElementById('bar')!.style.display = 'block';
  if (!voice.hasMic) {
    muteBtn.disabled = true;
    muteBtn.textContent = '🎤 없음';
    hud.toast('마이크 없이 입장 — 남의 목소리는 들려요. 초대 링크를 공유하세요.');
  }
}

enterBtn.onclick = () => void joinRoom(true);
enterNoMicBtn.onclick = () => void joinRoom(false);

/**
 * Refresh-in-room: rejoin the same table automatically with the saved name and mic choice.
 * The mic is only reused when the browser already granted permission — an auto-rejoin must
 * never surprise the user with a permission popup (it falls back to muted instead).
 */
async function tryAutoRejoin(): Promise<boolean> {
  const roomId = roomIdFromHash();
  const savedName = localStorage.getItem('tt.name');
  const micPref = localStorage.getItem('tt.micPref');
  if (!roomId || !savedName || !micPref) return false;

  let withMic = micPref === 'mic';
  if (withMic) {
    try {
      const status = await navigator.permissions.query({ name: 'microphone' as PermissionName });
      if (status.state !== 'granted') withMic = false;
    } catch {
      // Safari has no 'microphone' query; a granted permission still resolves silently.
    }
  }

  (document.getElementById('name') as HTMLInputElement).value = savedName;
  await joinRoom(withMic);
  return true;
}

hud.showJoin(roomIdFromHash());
void tryAutoRejoin();

// ---- Bottom bar: mute, emotes, chat ----
const muteBtn = document.getElementById('muteBtn') as HTMLButtonElement;
let micOn = true;

muteBtn.onclick = () => {
  micOn = !micOn;
  voice.setInputEnabled(micOn);
  muteBtn.textContent = micOn ? '🎤 켜짐' : '🔇 꺼짐';
  muteBtn.classList.toggle('active', !micOn);
};

for (const btn of document.querySelectorAll<HTMLButtonElement>('.emoteBtn')) {
  btn.onclick = () => net?.sendEmote(Number(btn.dataset.kind));
}

const chatPanel = document.getElementById('chat')!;
const chatInput = document.getElementById('chatInput') as HTMLInputElement;
const chatLog = document.getElementById('chatLog')!;

function toggleChat(): void {
  const open = chatPanel.style.display !== 'block';
  chatPanel.style.display = open ? 'block' : 'none';
  if (open) chatInput.focus();
}

document.getElementById('chatBtn')!.onclick = toggleChat;

function appendChatLine(name: string, text: string): void {
  const div = document.createElement('div');
  div.textContent = `${name}: ${text}`;
  chatLog.appendChild(div);
  while (chatLog.children.length > 50) chatLog.removeChild(chatLog.firstChild!);
  chatLog.scrollTop = chatLog.scrollHeight;
  if (chatPanel.style.display !== 'block') hud.toast(`💬 ${name}: ${text.slice(0, 40)}`);
}

chatInput.onkeydown = (e) => {
  e.stopPropagation();
  if (e.key === 'Enter' && chatInput.value.trim()) {
    net?.sendChat(chatInput.value);
    chatInput.value = '';
  } else if (e.key === 'Escape') {
    toggleChat();
  }
};

window.addEventListener('keydown', (e) => {
  const typing = document.activeElement instanceof HTMLInputElement;
  if (e.key === 'Tab' && net) {
    e.preventDefault();
    toggleChat();
  } else if (!typing && net && e.key >= '1' && e.key <= '3') {
    net.sendEmote(Number(e.key) - 1);
  }
});

// Dev/diagnostic handle (used by agent-driven preview tests; harmless in prod).
Object.assign(window as unknown as Record<string, unknown>, {
  __tt: { participants, avatars, seatLayout, net: () => net },
});

// ---- Render loop ----
const clock = new THREE.Clock();

function frame(): void {
  const dt = Math.min(clock.getDelta(), 0.1);
  const now = performance.now() / 1000;

  const local = participants.local();
  if (net && local) {
    seated.update(local.seatIndex, dt);
    net.tick(dt);
    voice.updatePanning();
  }

  for (const [id, avatar] of avatars) {
    const p = participants.get(id);
    avatar.update(dt, now);
    if (p && p.seatIndex >= 0) avatar.applySeat();
  }

  hud.updateDebug(net);
  renderer.render(scene, camera);
  requestAnimationFrame(frame);
}

frame();
