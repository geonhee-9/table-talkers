// RoomNetwork: WebRTC mesh with a tiny ws signaling relay. The room creator (first
// joiner) is the HOST and owns seat assignment (host-authoritative). Two data channels
// per peer: 'ctrl' (reliable: names, seats, pings) and 'pose' (lossy: head yaw/pitch).
import { CONFIG } from '../core/config';
import { participants, type Participant } from '../core/participants';
import type { WebRtcVoice } from '../voice/voice';

interface CtrlMsg {
  t: 'hi' | 'seats' | 'ping' | 'pong';
  name?: string;
  seats?: Record<string, number>;
  ts?: number;
}

interface PeerLink {
  pc: RTCPeerConnection;
  ctrl?: RTCDataChannel;
  pose?: RTCDataChannel;
  rttMs: number;
  remoteSet: boolean;             // remote description applied yet?
  pendingIce: RTCIceCandidateInit[]; // ICE that arrived before the remote description
}

export class RoomNetwork {
  private ws!: WebSocket;
  private selfId = '';
  private isHost = false;
  private readonly links = new Map<string, PeerLink>();
  private readonly seats = new Map<string, number>(); // host-authoritative seat map
  private poseTimer = 0;
  private pingTimer = 0;

  onSeated: (() => void) | null = null;
  onPeerCount: (() => void) | null = null;
  onHostLeft: (() => void) | null = null;

  constructor(
    private readonly roomId: string,
    private readonly localName: string,
    private readonly voice: WebRtcVoice,
  ) {}

  get amHost(): boolean {
    return this.isHost;
  }

  rttOf(id: string): number {
    return this.links.get(id)?.rttMs ?? -1;
  }

  connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(CONFIG.signalUrl);
      this.ws.onerror = () => reject(new Error('signal-unreachable'));
      this.ws.onopen = () => {
        this.ws.send(JSON.stringify({ t: 'join', room: this.roomId }));
      };
      this.ws.onmessage = (ev) => {
        const msg = JSON.parse(ev.data as string);
        if (msg.t === 'welcome') {
          this.selfId = msg.id;
          this.isHost = msg.peers.length === 0;
          this.addLocalParticipant();
          // Newcomer initiates offers to everyone already in the room (no glare).
          for (const peerId of msg.peers) void this.createLink(peerId, true);
          resolve();
        } else if (msg.t === 'peer-joined') {
          void this.createLink(msg.id, false);
        } else if (msg.t === 'peer-left') {
          this.dropPeer(msg.id);
        } else if (msg.t === 'signal') {
          void this.handleSignal(msg.from, msg.data);
        }
      };
    });
  }

  private addLocalParticipant(): void {
    const local: Participant = {
      id: this.selfId, isLocal: true, name: this.localName,
      seatIndex: -1, headYaw: 0, headPitch: 0, speaking: false,
    };
    participants.add(local);
    if (this.isHost) {
      this.assignSeats();
    }
  }

  private ensureLink(peerId: string): PeerLink {
    let link = this.links.get(peerId);
    if (link) return link;

    const pc = new RTCPeerConnection({ iceServers: [...CONFIG.stunServers] });
    link = { pc, rttMs: -1, remoteSet: false, pendingIce: [] };
    this.links.set(peerId, link);

    participants.add({
      id: peerId, isLocal: false, name: '',
      seatIndex: this.seats.get(peerId) ?? -1, headYaw: 0, headPitch: 0, speaking: false,
    });
    this.onPeerCount?.();

    const mic = this.voice.micStream;
    if (mic) for (const track of mic.getTracks()) pc.addTrack(track, mic);

    pc.ontrack = (ev) => this.voice.addPeerStream(peerId, ev.streams[0]);
    pc.onicecandidate = (ev) => {
      if (ev.candidate) this.signal(peerId, { ice: ev.candidate });
    };
    pc.onconnectionstatechange = () => {
      console.log(`[net] peer ${peerId}: ${pc.connectionState}`);
      this.onPeerCount?.();
    };
    // Either side may end up receiving channels (robust to who offered).
    pc.ondatachannel = (ev) => {
      if (ev.channel.label === 'ctrl') this.bindCtrl(peerId, ev.channel);
      else if (ev.channel.label === 'pose') this.bindPose(peerId, ev.channel);
    };
    return link;
  }

  private async createLink(peerId: string, initiator: boolean): Promise<void> {
    const link = this.ensureLink(peerId);
    if (initiator) {
      this.bindCtrl(peerId, link.pc.createDataChannel('ctrl'));
      this.bindPose(peerId, link.pc.createDataChannel('pose', { ordered: false, maxRetransmits: 0 }));
      const offer = await link.pc.createOffer();
      await link.pc.setLocalDescription(offer);
      this.signal(peerId, { sdp: link.pc.localDescription });
    }
  }

  private async handleSignal(from: string, data: { sdp?: RTCSessionDescriptionInit; ice?: RTCIceCandidateInit }): Promise<void> {
    const link = this.ensureLink(from); // offer may arrive before our peer-joined event
    if (data.sdp) {
      await link.pc.setRemoteDescription(data.sdp);
      link.remoteSet = true;
      for (const ice of link.pendingIce) await link.pc.addIceCandidate(ice).catch(() => undefined);
      link.pendingIce = [];
      if (data.sdp.type === 'offer') {
        const answer = await link.pc.createAnswer();
        await link.pc.setLocalDescription(answer);
        this.signal(from, { sdp: link.pc.localDescription });
      }
    } else if (data.ice) {
      if (link.remoteSet) {
        await link.pc.addIceCandidate(data.ice).catch(() => undefined);
      } else {
        link.pendingIce.push(data.ice); // queue until the remote description lands
      }
    }
  }

  /** Debug: 'connected' / 'connecting' / etc. + whether the pose channel is open. */
  linkState(peerId: string): string {
    const link = this.links.get(peerId);
    if (!link) return '—';
    const pose = link.pose?.readyState === 'open' ? 'pose✓' : 'pose…';
    return `${link.pc.connectionState} ${pose}`;
  }

  private signal(to: string, data: unknown): void {
    this.ws.send(JSON.stringify({ t: 'signal', to, data }));
  }

  private bindCtrl(peerId: string, ch: RTCDataChannel): void {
    const link = this.links.get(peerId);
    if (link) link.ctrl = ch;
    ch.onopen = () => {
      this.sendCtrl(peerId, { t: 'hi', name: this.localName });
      if (this.isHost) this.assignSeats();
    };
    ch.onmessage = (ev) => this.handleCtrl(peerId, JSON.parse(ev.data as string) as CtrlMsg);
  }

  private bindPose(peerId: string, ch: RTCDataChannel): void {
    const link = this.links.get(peerId);
    if (link) link.pose = ch;
    ch.onmessage = (ev) => {
      const p = participants.get(peerId);
      if (!p) return;
      const [yaw, pitch] = (ev.data as string).split(',');
      p.headYaw = Number(yaw) || 0;
      p.headPitch = Number(pitch) || 0;
    };
  }

  private handleCtrl(peerId: string, msg: CtrlMsg): void {
    const p = participants.get(peerId);
    switch (msg.t) {
      case 'hi':
        if (p && msg.name) p.name = msg.name;
        break;
      case 'seats':
        if (!this.isHost && msg.seats) this.applySeatMap(msg.seats);
        break;
      case 'ping':
        this.sendCtrl(peerId, { t: 'pong', ts: msg.ts });
        break;
      case 'pong': {
        const link = this.links.get(peerId);
        if (link && msg.ts) link.rttMs = performance.now() - msg.ts;
        break;
      }
    }
  }

  /** HOST ONLY: fill seats in join order and broadcast the full map. */
  private assignSeats(): void {
    const everyone = [this.selfId, ...this.links.keys()];
    for (const id of everyone) {
      if (!this.seats.has(id)) {
        for (let s = 0; s < CONFIG.seatCount; s++) {
          if (![...this.seats.values()].includes(s)) {
            this.seats.set(id, s);
            break;
          }
        }
      }
    }
    // Drop seats of departed peers.
    for (const id of [...this.seats.keys()]) {
      if (id !== this.selfId && !this.links.has(id)) this.seats.delete(id);
    }
    this.applySeatMap(Object.fromEntries(this.seats));
    this.broadcastCtrl({ t: 'seats', seats: Object.fromEntries(this.seats) });
  }

  private applySeatMap(map: Record<string, number>): void {
    let localSeated = false;
    for (const [id, seat] of Object.entries(map)) {
      const p = participants.get(id);
      if (p) {
        const wasUnseated = p.seatIndex < 0;
        p.seatIndex = seat;
        if (p.isLocal && wasUnseated) localSeated = true;
      }
    }
    if (localSeated) this.onSeated?.();
  }

  private dropPeer(peerId: string): void {
    const wasHostPeer = !this.isHost && this.isLowestId(peerId);
    this.links.get(peerId)?.pc.close();
    this.links.delete(peerId);
    this.voice.removePeer(peerId);
    participants.remove(peerId);
    this.seats.delete(peerId);
    this.onPeerCount?.();
    if (this.isHost) this.assignSeats();
    else if (wasHostPeer) this.onHostLeft?.(); // host migration is out of MVP scope
  }

  private isLowestId(peerId: string): boolean {
    const ids = [peerId, this.selfId, ...this.links.keys()].map(Number);
    return Number(peerId) === Math.min(...ids);
  }

  /** Call every frame: throttled pose broadcast + 1Hz ping. */
  tick(dt: number, localYaw: number, localPitch: number): void {
    const local = participants.local();
    if (local) {
      local.headYaw = localYaw;
      local.headPitch = localPitch;
    }

    this.poseTimer += dt;
    if (this.poseTimer >= 1 / CONFIG.headSyncHz) {
      this.poseTimer = 0;
      const payload = `${localYaw.toFixed(1)},${localPitch.toFixed(1)}`;
      for (const link of this.links.values()) {
        if (link.pose?.readyState === 'open') link.pose.send(payload);
      }
    }

    this.pingTimer += dt;
    if (this.pingTimer >= 1) {
      this.pingTimer = 0;
      this.broadcastCtrl({ t: 'ping', ts: performance.now() });
    }
  }

  private sendCtrl(peerId: string, msg: CtrlMsg): void {
    const link = this.links.get(peerId);
    if (link?.ctrl?.readyState === 'open') link.ctrl.send(JSON.stringify(msg));
  }

  private broadcastCtrl(msg: CtrlMsg): void {
    for (const id of this.links.keys()) this.sendCtrl(id, msg);
  }

  leave(): void {
    for (const link of this.links.values()) link.pc.close();
    this.links.clear();
    this.ws.close();
    participants.clear();
  }
}
