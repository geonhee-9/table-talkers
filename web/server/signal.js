// Tiny room-based WebRTC signaling relay. No state beyond live sockets — media and
// room state stay P2P per the "minimal always-on server" principle.
// Run: node server/signal.js  (port 8787)
import { WebSocketServer } from 'ws';

const PORT = process.env.PORT || 8787;
const MAX_PEERS_PER_ROOM = 10; // matches the client's max table size
const rooms = new Map(); // roomId -> Map<peerId, ws>
let nextId = 1;

const wss = new WebSocketServer({ port: PORT });
console.log(`[signal] listening on :${PORT}`);

wss.on('connection', (ws) => {
  let roomId = null;
  let peerId = null;

  ws.on('message', (raw) => {
    let msg;
    try { msg = JSON.parse(raw); } catch { return; }

    if (msg.t === 'join' && typeof msg.room === 'string' && !roomId) {
      const requested = msg.room.slice(0, 32);
      if ((rooms.get(requested)?.size ?? 0) >= MAX_PEERS_PER_ROOM) {
        ws.send(JSON.stringify({ t: 'full' }));
        ws.close();
        return;
      }
      roomId = requested;
      peerId = String(nextId++);
      if (!rooms.has(roomId)) rooms.set(roomId, new Map());
      const room = rooms.get(roomId);

      // Tell the newcomer who they are and who is already here (first joiner = host).
      ws.send(JSON.stringify({ t: 'welcome', id: peerId, peers: [...room.keys()] }));
      for (const other of room.values()) {
        other.send(JSON.stringify({ t: 'peer-joined', id: peerId }));
      }
      room.set(peerId, ws);
      return;
    }

    // Relay SDP/ICE to one target peer.
    if (msg.t === 'signal' && roomId && msg.to) {
      const target = rooms.get(roomId)?.get(String(msg.to));
      if (target) {
        target.send(JSON.stringify({ t: 'signal', from: peerId, data: msg.data }));
      }
    }
  });

  ws.on('close', () => {
    if (!roomId || !peerId) return;
    const room = rooms.get(roomId);
    if (!room) return;
    room.delete(peerId);
    for (const other of room.values()) {
      other.send(JSON.stringify({ t: 'peer-left', id: peerId }));
    }
    if (room.size === 0) rooms.delete(roomId);
  });
});
