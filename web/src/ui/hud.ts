// Minimal HUD glue over the static panels in index.html. UI strings via a loc stub.
import { participants } from '../core/participants';
import type { RoomNetwork } from '../net/room';

const L: Record<string, string> = {
  createRoom: '새 테이블 만들기',
  joinRoom: '초대받은 테이블에 참여합니다',
  roomLine: '방 코드: ',
  copied: '복사됨!',
  hostLeft: '호스트가 자리를 떠났어요. 새 테이블을 만들어주세요.',
  micDenied: '마이크 권한이 필요해요. 브라우저 주소창의 자물쇠에서 허용해주세요.',
  signalDown: '연결 서버에 닿을 수 없어요 (시그널링 서버 확인).',
};

export const loc = (k: string): string => L[k] ?? k;

const el = (id: string): HTMLElement => document.getElementById(id)!;

export function showJoin(roomId: string | null): void {
  el('joinMode').textContent = roomId ? loc('joinRoom') : loc('createRoom');
  el('join').style.display = 'block';
  (el('name') as HTMLInputElement).value = localStorage.getItem('tt.name') ?? '';
}

export function hideJoin(): void {
  el('join').style.display = 'none';
}

export function showJoinError(key: string): void {
  el('joinErr').textContent = loc(key);
}

export function showRoomPanel(roomId: string, onLeave: () => void): void {
  el('room').style.display = 'block';
  el('roomLine').innerHTML = `${loc('roomLine')}<code>${roomId}</code>`;
  el('hint').style.display = 'block';
  el('copyLink').onclick = () => {
    void navigator.clipboard.writeText(location.href);
    el('copyLink').textContent = loc('copied');
    setTimeout(() => { el('copyLink').textContent = '초대 링크 복사'; }, 1500);
  };
  el('leave').onclick = onLeave;
}

export function toast(text: string): void {
  const t = el('toast');
  t.textContent = text;
  t.style.display = 'block';
  setTimeout(() => { t.style.display = 'none'; }, 3500);
}

let debugVisible = false;

export function setupDebugToggle(): void {
  window.addEventListener('keydown', (e) => {
    if (e.key === 'F1') {
      e.preventDefault();
      debugVisible = !debugVisible;
      el('debug').style.display = debugVisible ? 'block' : 'none';
    }
  });
}

export function updateDebug(net: RoomNetwork | null): void {
  if (!debugVisible || !net) return;
  const lines = [`host: ${net.amHost ? 'me' : 'peer'}`];
  for (const p of participants.all()) {
    const rtt = p.isLocal ? 0 : net.rttOf(p.id);
    lines.push(
      `seat ${p.seatIndex} ${p.name || '?'}${p.isLocal ? ' (me)' : ''}` +
      `${p.speaking ? ' *' : ''}  ${rtt >= 0 ? Math.round(rtt) + 'ms' : ''}`,
    );
  }
  el('debug').textContent = lines.join('\n');
}
