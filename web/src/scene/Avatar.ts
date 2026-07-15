// Primitive avatar with presence built in: eyes for gaze, mouth driven by voice level,
// blink/breathing, seat colors, speaking ring, name sprite, WASD upper-body lean, and two
// hand emotes (raise hand / clap). The upper body (torso+head+hands) sits in one group that
// tilts for lean while the lower body stays on the chair. Only head yaw/pitch, lean, and the
// name travel over the network — everything else is animated locally.
import * as THREE from 'three';
import { CONFIG } from '../core/config';
import { seatLayout } from '../core/seats';
import type { Participant } from '../core/participants';
import type { VoiceService } from '../voice/voice';

const PALETTE = [0xe8735f, 0x5a9e99, 0xedb75c, 0x9e8cc7, 0x8cad73, 0x709ecc, 0xd98fa5, 0xb8a380];

// Emote ids (match the 1/2 hotkeys and bottom-bar buttons).
const RAISE_HAND = 0;
const CLAP = 1;

export class Avatar {
  readonly group = new THREE.Group();
  private readonly upperBody = new THREE.Group(); // tilts for lean; lower body stays put
  // Where the local player's camera attaches (sibling of head/body under upperBody, so it
  // inherits ONLY the lean tilt — not head's own network-smoothed look rotation, which would
  // add input lag). This is what makes the camera move in lockstep with the torso: same
  // rotation, same pivot, not two independently-computed transforms that can drift apart.
  private readonly eyeAnchor = new THREE.Object3D();
  private readonly head: THREE.Group;
  private readonly mouth: THREE.Mesh;
  private readonly eyeL: THREE.Mesh;
  private readonly eyeR: THREE.Mesh;
  private readonly body: THREE.Mesh;
  private readonly ring: THREE.Mesh;
  private readonly nameSprite: THREE.Sprite;
  private readonly handL: THREE.Mesh;
  private readonly handR: THREE.Mesh;
  private smoothYaw = 0;
  private smoothPitch = 0;
  private smoothLeanFwd = 0;
  private smoothLeanRight = 0;
  private mouthOpen = 0;
  private blinkAt = performance.now() / 1000 + 2;
  private blinkStart = -1;
  private readonly breathePhase = Math.random() * Math.PI * 2;
  private lastName = '';
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

    // Hands (hidden until an emote plays).
    this.handL = new THREE.Mesh(new THREE.SphereGeometry(0.085, 14, 12), skin);
    this.handL.scale.set(1, 0.88, 1.05);
    this.handR = this.handL.clone();
    this.handL.visible = false;
    this.handR.visible = false;

    this.eyeAnchor.position.set(0, CONFIG.eyeHeight, 0);
    this.upperBody.add(this.body, this.head, this.handL, this.handR, this.eyeAnchor);

    this.ring = new THREE.Mesh(
      new THREE.RingGeometry(0.32, 0.42, 32),
      new THREE.MeshBasicMaterial({ color: 0xffcc55, transparent: true, opacity: 0 }),
    );
    this.ring.rotation.x = -Math.PI / 2;
    this.ring.position.y = 0.02;

    this.nameSprite = makeNameSprite('');
    this.nameSprite.position.y = 1.5;

    this.group.add(this.upperBody, this.ring, this.nameSprite);
  }

  /** One-shot emote: 0 raise hand · 1 clap. */
  playEmote(kind: number): void {
    this.emoteKind = kind;
    this.emoteT = 0;
    if (this.participant.isLocal) return; // own floating label would sit at eye level
    const labels = ['손들기!', '짝짝짝'];
    if (this.emoteLabel) this.group.remove(this.emoteLabel);
    this.emoteLabel = makeNameSprite(labels[kind] ?? '!');
    this.emoteLabel.position.y = 1.75;
    this.group.add(this.emoteLabel);
  }

  /**
   * First-person: hide my own head/torso/name so they never block my camera or let me see my
   * own neck-stump (hands stay visible — that's the point of the earlier emote-visibility fix).
   */
  setFirstPersonView(): void {
    this.head.visible = false;
    this.body.visible = false;
    this.nameSprite.visible = false;
  }

  /**
   * Parent the given camera to this avatar's eye position. Once attached, the camera inherits
   * this avatar's seat position AND upper-body lean tilt automatically via the scene graph —
   * there is no separate camera transform to keep in sync, so it cannot drift from how the body
   * actually moves (which is what made leaning look like "only the head moves" before).
   */
  attachCamera(camera: THREE.Object3D): void {
    this.eyeAnchor.add(camera);
    camera.position.set(0, 0, 0);
  }

  applySeat(): void {
    const seat = this.participant.seatIndex;
    if (seat < 0 || seat >= seatLayout.seatCount) return;
    const a = seatLayout.anchor(seat);
    this.group.position.set(a.x, 0.55, a.z);
    this.group.rotation.y = (a.yawDeg * Math.PI) / 180;
    (this.body.material as THREE.MeshStandardMaterial).color.setHex(PALETTE[seat % PALETTE.length]);
  }

  update(dt: number, now: number): void {
    const p = this.participant;

    // Unseated participants (spectators, still-connecting peers) stay hidden, not at the origin.
    this.group.visible = p.seatIndex >= 0;
    if (!this.group.visible) return;

    const k = 1 - Math.exp(-CONFIG.poseLerpSpeed * dt);
    this.smoothYaw += (p.headYaw - this.smoothYaw) * k;
    this.smoothPitch += (p.headPitch - this.smoothPitch) * k;
    this.smoothLeanFwd += (p.leanFwd - this.smoothLeanFwd) * k;
    this.smoothLeanRight += (p.leanRight - this.smoothLeanRight) * k;

    // Head pose (yaw negated to match the seated camera's look convention).
    this.head.rotation.set(
      (-this.smoothPitch * Math.PI) / 180,
      (-this.smoothYaw * Math.PI) / 180,
      0,
    );
    this.body.rotation.y = (-this.smoothYaw * 0.25 * Math.PI) / 180;

    // Upper-body lean: tilt the whole torso group from the hips; lower body stays on the chair.
    const maxRad = (CONFIG.leanMaxDeg * Math.PI) / 180;
    this.upperBody.rotation.x = this.smoothLeanFwd * maxRad;
    this.upperBody.rotation.z = -this.smoothLeanRight * maxRad;

    // Mouth from voice level (local and remote alike).
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
      if (t >= 1) { this.blinkStart = -1; this.blinkAt = now + 1.5 + Math.random() * 3.5; }
      else lid = 1 - Math.sin(t * Math.PI);
    } else if (now >= this.blinkAt) {
      this.blinkStart = now;
    }
    this.eyeL.scale.y = Math.max(0.1, lid);
    this.eyeR.scale.y = Math.max(0.1, lid);

    // Breathing.
    this.body.scale.y = 1 + 0.015 * Math.sin(now * 1.9 + this.breathePhase);

    this.updateEmote(dt);

    if (p.name !== this.lastName) {
      this.lastName = p.name;
      updateNameSprite(this.nameSprite, p.name);
    }
  }

  /**
   * Code-driven hand emotes. Positions sit forward and near eye level so the player sees their
   * OWN hands in first person, while still reading clearly to everyone across the table.
   */
  private updateEmote(dt: number): void {
    if (this.emoteKind < 0) return;
    this.emoteT += dt;
    const t = this.emoteT;
    const raiseHold = 5; // ✋ holds up (turn-taking); clap is quick
    const duration = this.emoteKind === RAISE_HAND ? raiseHold : 1.5;

    switch (this.emoteKind) {
      case RAISE_HAND: {
        this.handR.visible = true;
        const up = Math.min(1, t / 0.3);
        // Checked against the fist's own radius (not just its centre point) on a true
        // mobile-portrait aspect (375×812) using the camera's actual screen-right/up vectors —
        // that's what caught this: the centre point tested fine, but the near, wide fist's edge
        // was clipped off the left of the frame. Depth 0.9 (vs 0.68 for clap) shrinks its angular
        // size enough to give the edge headroom. It originally sat 41° off horizontal axis and
        // 0.4m above eye level at a close z=0.55: technically "in front of" the camera but outside
        // the viewport — invisible to the local player despite reading fine across the table.
        const wave = t > 0.3 ? Math.sin(t * 5) * 0.025 : 0;
        this.handR.position.set(0.09 + wave, 0.6 + up * 0.65, 0.9);
        if (this.participant.speaking && t > 0.6) this.emoteT = raiseHold; // lower when you speak
        break;
      }
      case CLAP: {
        this.handL.visible = true;
        this.handR.visible = true;
        const spread = 0.04 + 0.15 * Math.abs(Math.sin(t * 14));
        this.handL.position.set(-spread, 1.05, 0.68);
        this.handR.position.set(spread, 1.05, 0.68);
        break;
      }
    }

    if (this.emoteLabel) {
      this.emoteLabel.position.y = 1.75 + Math.min(t, 1.4) * 0.3;
      this.emoteLabel.material.opacity = Math.max(0, 1 - t / Math.min(duration, 1.6));
    }

    if (t >= duration) {
      this.emoteKind = -1;
      this.handL.visible = false;
      this.handR.visible = false;
      this.handR.scale.setScalar(1);
      if (this.emoteLabel) { this.group.remove(this.emoteLabel); this.emoteLabel = null; }
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
