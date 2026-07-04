// VoiceService seam: presence/UI code depends on this interface only — the WebRTC
// implementation hides behind it (same invariant as the Unity prototype's IVoiceService).

import { CONFIG, SEAT_ANCHORS } from '../core/config';
import { participants } from '../core/participants';

export interface VoiceService {
  setInputEnabled(enabled: boolean): void;
  setPeerVolume(participantId: string, volume01: number): void;
  setPeerMuted(participantId: string, muted: boolean): void;
  getLevel(participantId: string): number; // 0..1
}

interface PeerAudio {
  source: MediaStreamAudioSourceNode;
  analyser: AnalyserNode;
  gain: GainNode;
  panner: StereoPannerNode;
  el: HTMLAudioElement; // Chrome workaround: WebRTC audio must hit a media element to flow
  muted: boolean;
  volume: number;
  buf: Float32Array<ArrayBuffer>;
}

export class WebRtcVoice implements VoiceService {
  private ctx: AudioContext | null = null;
  private mic: MediaStream | null = null;
  private localAnalyser: AnalyserNode | null = null;
  private localBuf: Float32Array<ArrayBuffer> | null = null;
  private readonly peers = new Map<string, PeerAudio>();

  /** Create the audio graph (needs a user gesture). Enables hearing peers even without a mic. */
  initAudio(): void {
    if (!this.ctx) this.ctx = new AudioContext();
    void this.ctx.resume();
  }

  get hasMic(): boolean {
    return this.mic !== null;
  }

  /**
   * Ask for the mic with browser AEC/NS/AGC on (the free voice-quality pipeline).
   * Rejects on denial OR after timeoutMs (some browsers hang on the permission prompt) —
   * so the join flow never gets stuck.
   */
  async requestMic(timeoutMs = 10000): Promise<void> {
    this.initAudio();
    const stream = await withTimeout(
      navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      }),
      timeoutMs,
    );
    this.mic = stream;
    const src = this.ctx!.createMediaStreamSource(stream);
    this.localAnalyser = this.ctx!.createAnalyser();
    this.localAnalyser.fftSize = 256;
    this.localBuf = new Float32Array(this.localAnalyser.fftSize);
    src.connect(this.localAnalyser); // analysis only — no local playback (no echo)
  }

  get micStream(): MediaStream | null {
    return this.mic;
  }

  addPeerStream(participantId: string, stream: MediaStream): void {
    if (!this.ctx || this.peers.has(participantId)) return;
    const el = new Audio();
    el.srcObject = stream;
    el.muted = true; // audible path goes through WebAudio below
    void el.play().catch(() => undefined);

    const source = this.ctx.createMediaStreamSource(stream);
    const analyser = this.ctx.createAnalyser();
    analyser.fftSize = 256;
    const gain = this.ctx.createGain();
    const panner = this.ctx.createStereoPanner();
    source.connect(analyser);
    analyser.connect(gain);
    gain.connect(panner);
    panner.connect(this.ctx.destination);

    this.peers.set(participantId, {
      source, analyser, gain, panner, el,
      muted: false, volume: 1,
      buf: new Float32Array(analyser.fftSize),
    });
  }

  removePeer(participantId: string): void {
    const peer = this.peers.get(participantId);
    if (!peer) return;
    peer.source.disconnect();
    peer.el.srcObject = null;
    this.peers.delete(participantId);
  }

  setInputEnabled(enabled: boolean): void {
    this.mic?.getAudioTracks().forEach((t) => { t.enabled = enabled; });
  }

  setPeerVolume(participantId: string, volume01: number): void {
    const peer = this.peers.get(participantId);
    if (!peer) return;
    peer.volume = Math.max(0, Math.min(1, volume01));
    peer.gain.gain.value = peer.muted ? 0 : peer.volume;
  }

  setPeerMuted(participantId: string, muted: boolean): void {
    const peer = this.peers.get(participantId);
    if (!peer) return;
    peer.muted = muted;
    peer.gain.gain.value = muted ? 0 : peer.volume;
  }

  getLevel(participantId: string): number {
    const local = participants.local();
    if (local && participantId === local.id) {
      return this.readLevel(this.localAnalyser, this.localBuf);
    }
    const peer = this.peers.get(participantId);
    return peer ? this.readLevel(peer.analyser, peer.buf) : 0;
  }

  /** Seat-direction stereo pan, relative to the local listener's head. */
  updatePanning(): void {
    const local = participants.local();
    if (!local || local.seatIndex < 0) return;
    const la = SEAT_ANCHORS[local.seatIndex];
    const headYawRad = ((la.yawDeg + local.headYaw) * Math.PI) / 180;

    for (const [id, peer] of this.peers) {
      const p = participants.get(id);
      if (!p || p.seatIndex < 0) { peer.panner.pan.value = 0; continue; }
      const pa = SEAT_ANCHORS[p.seatIndex];
      const dx = pa.x - la.x;
      const dz = pa.z - la.z;
      const len = Math.hypot(dx, dz) || 1;
      // Right vector of the listener's head in the XZ plane.
      const rx = Math.cos(headYawRad);
      const rz = -Math.sin(headYawRad);
      const pan = ((dx / len) * rx + (dz / len) * rz) * CONFIG.stereoPanStrength;
      peer.panner.pan.value = Math.max(-1, Math.min(1, pan));
    }
  }

  private readLevel(analyser: AnalyserNode | null, buf: Float32Array<ArrayBuffer> | null): number {
    if (!analyser || !buf) return 0;
    analyser.getFloatTimeDomainData(buf);
    let peak = 0;
    for (let i = 0; i < buf.length; i++) {
      const a = Math.abs(buf[i]);
      if (a > peak) peak = a;
    }
    return Math.min(1, peak);
  }
}

function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => reject(new DOMException('mic request timed out', 'TimeoutError')), ms);
    promise.then(
      (v) => { clearTimeout(timer); resolve(v); },
      (e) => { clearTimeout(timer); reject(e); },
    );
  });
}
