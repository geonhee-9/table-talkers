// App entry: join flow → scene + network + voice, one render loop.
import * as THREE from 'three';
import { participants, type Participant } from './core/participants';
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
hud.showJoin(roomIdFromHash());

const enterBtn = document.getElementById('enter') as HTMLButtonElement;

enterBtn.onclick = async () => {
  const name = (document.getElementById('name') as HTMLInputElement).value.trim() || 'Guest';
  localStorage.setItem('tt.name', name);

  enterBtn.disabled = true;
  enterBtn.textContent = '마이크 확인 중…';
  hud.showJoinError('');

  try {
    await voice.initMic();
  } catch (err) {
    console.error('[TableTalkers] getUserMedia failed:', err);
    const name = err instanceof Error ? err.name : 'UnknownError';
    hud.showJoinError('micDenied', name);
    enterBtn.disabled = false;
    enterBtn.textContent = '테이블에 앉기';
    return;
  }

  const roomId = roomIdFromHash() ?? newRoomId();
  location.hash = `r=${roomId}`;

  net = new RoomNetwork(roomId, name, voice);
  net.onSeated = () => {
    const local = participants.local();
    if (local) seated.sit(local.seatIndex);
  };
  net.onHostLeft = () => {
    hud.toast(hud.loc('hostLeft'));
    net?.leave();
    net = null;
    seated.preview();
    location.hash = '';
    hud.showJoin(null);
  };

  enterBtn.textContent = '방 연결 중…';
  try {
    await net.connect();
  } catch (err) {
    console.error('[TableTalkers] signaling connect failed:', err);
    hud.showJoinError('signalDown');
    net = null;
    enterBtn.disabled = false;
    enterBtn.textContent = '테이블에 앉기';
    return;
  }

  hud.hideJoin();
  hud.showRoomPanel(roomId, () => {
    net?.leave();
    location.hash = '';
    location.reload();
  });
};

// ---- Render loop ----
const clock = new THREE.Clock();

function frame(): void {
  const dt = Math.min(clock.getDelta(), 0.1);
  const now = performance.now() / 1000;

  const local = participants.local();
  if (net && local) {
    net.tick(dt, seated.yaw, seated.pitch);
    seated.update(local.seatIndex);
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
