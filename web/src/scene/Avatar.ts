// Primitive avatar with presence built in: eyes for gaze, mouth driven by voice level,
// blink/breathing, seat colors, speaking ring, name sprite. All locally animated —
// only head yaw/pitch and the name arrive over the network.
import * as THREE from 'three';
import { CONFIG, SEAT_ANCHORS } from '../core/config';
import type { Participant } from '../core/participants';
import type { VoiceService } from '../voice/voice';

const PALETTE = [0xe8735f, 0x5a9e99, 0xedb75c, 0x9e8cc7, 0x8cad73, 0x709ecc, 0xd98fa5, 0xb8a380];

export class Avatar {
  readonly group = new THREE.Group();
  private readonly head: THREE.Group;
  private readonly mouth: THREE.Mesh;
  private readonly eyeL: THREE.Mesh;
  private readonly eyeR: THREE.Mesh;
  private readonly body: THREE.Mesh;
  private readonly ring: THREE.Mesh;
  private readonly nameSprite: THREE.Sprite;
  private smoothYaw = 0;
  private smoothPitch = 0;
  private mouthOpen = 0;
  private blinkAt = performance.now() / 1000 + 2;
  private blinkStart = -1;
  private readonly breathePhase = Math.random() * Math.PI * 2;
  private lastName = '';
  private readonly handL: THREE.Mesh;
  private readonly handR: THREE.Mesh;
  private emoteKind = -1;
  private emoteT = 0;
  private emoteLabel: THREE.Sprite | null = null;

  constructor(private readonly participant: Participant, private readonly voice: VoiceService) {
    const skin = new THREE.MeshStandardMaterial({ color: 0xeacbad, roughness: 0.7 });
    const dark = new THREE.MeshStandardMaterial({ color: 0x1f1a1a, roughness: 0.4 });
    const bodyMat = new THREE.MeshStandardMaterial({ color: 0xbfb5a8, roughness: 0.8 });

    this.body = new THREE.Mesh(new THREE.CapsuleGeometry(0.2, 0.5, 4, 12), bodyMat);
    this.body.position.y = 0.35;
    this.body.castShadow = true;

    this.head = new THREE.Group();
    this.head.position.y = 1.0;
    const skull = new THREE.Mesh(new THREE.SphereGeometry(0.175, 24, 18), skin);
    skull.castShadow = true;
    this.eyeL = new THREE.Mesh(new THREE.SphereGeometry(0.028, 10, 8), dark);
    this.eyeL.position.set(-0.055, 0.03, 0.15);
    this.eyeR = this.eyeL.clone();
    this.eyeR.position.x = 0.055;
    this.mouth = new THREE.Mesh(new THREE.SphereGeometry(0.045, 10, 8), dark);
    this.mouth.position.set(0, -0.055, 0.155);
    this.mouth.scale.set(1.2, 0.35, 0.5);
    this.head.add(skull, this.eyeL, this.eyeR, this.mouth);

    this.ring = new THREE.Mesh(
      new THREE.RingGeometry(0.32, 0.42, 32),
      new THREE.MeshBasicMaterial({ color: 0xffcc55, transparent: true, opacity: 0 }),
    );
    this.ring.rotation.x = -Math.PI / 2;
    this.ring.position.y = 0.02;

    this.nameSprite = makeNameSprite('');
    this.nameSprite.position.y = 1.45;

    // Hands for emotes (hidden until one plays).
    this.handL = new THREE.Mesh(new THREE.SphereGeometry(0.055, 10, 8), skin);
    this.handR = this.handL.clone();
    this.handL.visible = false;
    this.handR.visible = false;

    this.group.add(this.body, this.head, this.ring, this.nameSprite, this.handL, this.handR);
  }

  /** One-shot procedural emote: 0 nod · 1 laugh · 2 raise hand · 3 thumbs up · 4 clap. */
  playEmote(kind: number): void {
    this.emoteKind = kind;
    this.emoteT = 0;
    const labels = ['끄덕끄덕', 'ㅎㅎㅎ', '손들기!', '좋아요', '짝짝짝'];
    if (this.emoteLabel) this.group.remove(this.emoteLabel);
    this.emoteLabel = makeNameSprite(labels[kind] ?? '!');
    this.emoteLabel.position.y = 1.7;
    this.group.add(this.emoteLabel);
  }

  /** First-person: hide my own head/name so they never block my camera. */
  setFirstPersonView(): void {
    this.head.visible = false;
    this.nameSprite.visible = false;
  }

  applySeat(): void {
    const seat = this.participant.seatIndex;
    if (seat < 0 || seat >= SEAT_ANCHORS.length) return;
    const a = SEAT_ANCHORS[seat];
    this.group.position.set(a.x, 0.55, a.z);
    this.group.rotation.y = (a.yawDeg * Math.PI) / 180;
    (this.body.material as THREE.MeshStandardMaterial).color.setHex(PALETTE[seat % PALETTE.length]);
  }

  update(dt: number, now: number): void {
    const p = this.participant;

    // Head pose: remote values interpolate; local is driven directly by the camera.
    const k = 1 - Math.exp(-CONFIG.poseLerpSpeed * dt);
    this.smoothYaw += (p.headYaw - this.smoothYaw) * k;
    this.smoothPitch += (p.headPitch - this.smoothPitch) * k;
    // Yaw sign is negated to match the seated camera's look convention (a right turn
    // must read as a right turn on the remote avatar, not a mirror image).
    this.head.rotation.set(
      (-this.smoothPitch * Math.PI) / 180,
      (-this.smoothYaw * Math.PI) / 180,
      0,
    );
    // Upper body follows the head a little, like real seated posture.
    this.body.rotation.y = (-this.smoothYaw * 0.25 * Math.PI) / 180;

    // Mouth from voice level (works for local and remote alike).
    const level = this.voice.getLevel(p.id);
    const target = Math.min(1, level / 0.3);
    this.mouthOpen += (target - this.mouthOpen) * (1 - Math.exp(-14 * dt));
    this.mouth.scale.y = 0.35 * (1 + 4 * this.mouthOpen);
    p.speaking = level >= CONFIG.speakingThreshold;

    // Speaking ring fade.
    const ringMat = this.ring.material as THREE.MeshBasicMaterial;
    ringMat.opacity += ((p.speaking ? 0.85 : 0) - ringMat.opacity) * (1 - Math.exp(-16 * dt));

    // Blink.
    let lid = 1;
    if (this.blinkStart >= 0) {
      const t = (now - this.blinkStart) / 0.12;
      if (t >= 1) {
        this.blinkStart = -1;
        this.blinkAt = now + 1.5 + Math.random() * 3.5;
      } else {
        lid = 1 - Math.sin(t * Math.PI);
      }
    } else if (now >= this.blinkAt) {
      this.blinkStart = now;
    }
    this.eyeL.scale.y = Math.max(0.1, lid);
    this.eyeR.scale.y = Math.max(0.1, lid);

    // Breathing.
    this.body.scale.y = 1 + 0.015 * Math.sin(now * 1.9 + this.breathePhase);

    this.updateEmote(dt);

    // Name (arrives async over ctrl channel).
    if (p.name !== this.lastName) {
      this.lastName = p.name;
      updateNameSprite(this.nameSprite, p.name);
    }
  }

  /** Code-driven emote motion — no animation assets needed for primitive avatars. */
  private updateEmote(dt: number): void {
    if (this.emoteKind < 0) return;
    this.emoteT += dt;
    const t = this.emoteT;
    const raiseHold = 5; // ✋ stays up (turn-taking aid), others are short
    const duration = this.emoteKind === 2 ? raiseHold : this.emoteKind === 4 ? 1.4 : 1.0;

    switch (this.emoteKind) {
      case 0: // nod: head pitch bob (composes on top of the synced pose)
        this.head.rotation.x += Math.sin((t / 1.0) * Math.PI * 4) * (16 * Math.PI / 180);
        break;
      case 1: { // laugh: bounce + head roll shake
        const wave = Math.sin(t * 22);
        this.body.position.y = 0.35 + Math.abs(wave) * 0.03;
        this.head.rotation.z = wave * (5 * Math.PI / 180);
        break;
      }
      case 2: { // raise hand: right hand up beside the head, hold, drop when done/speaking
        this.handR.visible = true;
        const up = Math.min(1, t / 0.25);
        this.handR.position.set(0.3, 0.5 + up * 0.85, 0.05);
        if (this.participant.speaking && t > 0.5) this.emoteT = raiseHold; // lower on speak
        break;
      }
      case 3: { // thumbs up: hand pops in front of the chest
        this.handR.visible = true;
        const pop = 1 + 0.35 * Math.exp(-5 * t) * Math.sin(t * 16);
        this.handR.position.set(0.2, 0.95, 0.3);
        this.handR.scale.setScalar(pop);
        break;
      }
      case 4: { // clap: hands meet repeatedly
        this.handL.visible = true;
        this.handR.visible = true;
        const spread = 0.05 + 0.14 * Math.abs(Math.sin(t * 14));
        this.handL.position.set(-spread, 0.95, 0.3);
        this.handR.position.set(spread, 0.95, 0.3);
        break;
      }
    }

    // Float + fade the label.
    if (this.emoteLabel) {
      this.emoteLabel.position.y = 1.7 + Math.min(t, 1.4) * 0.3;
      this.emoteLabel.material.opacity = Math.max(0, 1 - t / Math.min(duration, 1.6));
    }

    if (t >= duration) {
      this.emoteKind = -1;
      this.handL.visible = false;
      this.handR.visible = false;
      this.handR.scale.setScalar(1);
      this.body.position.y = 0.35;
      this.head.rotation.z = 0;
      if (this.emoteLabel) {
        this.group.remove(this.emoteLabel);
        this.emoteLabel = null;
      }
    }
  }
}

function makeNameSprite(text: string): THREE.Sprite {
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ transparent: true }));
  sprite.scale.set(0.9, 0.22, 1);
  updateNameSprite(sprite, text);
  return sprite;
}

function updateNameSprite(sprite: THREE.Sprite, text: string): void {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 64;
  const ctx = canvas.getContext('2d')!;
  ctx.fillStyle = 'rgba(20,14,10,0.55)';
  ctx.roundRect(28, 8, 200, 48, 12);
  ctx.fill();
  ctx.font = 'bold 30px system-ui, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = '#ffe9cc';
  ctx.fillText(text || '…', 128, 34);
  const mat = sprite.material;
  mat.map?.dispose();
  mat.map = new THREE.CanvasTexture(canvas);
  mat.needsUpdate = true;
}
