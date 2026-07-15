// Seated first-person: mouse-drag head look (yaw/pitch clamped) + WASD upper-body lean
// (lower body stays fixed on the chair).
//
// The camera does NOT compute its own world position/tilt for the lean. It is parented to the
// local avatar's eye anchor (see Avatar.attachCamera), which lives inside the SAME upperBody
// group that visibly tilts for everyone else — so the lean transform is applied exactly once
// and the camera inherits it through the scene graph. Two independently-computed transforms
// (one for what peers see, one for what you see) could only ever approximate each other; a
// shared transform can't drift apart. This file only ever sets the camera's LOCAL look
// rotation (yaw/pitch) on top of whatever the parent chain already contributes.
import * as THREE from 'three';
import { CONFIG } from '../core/config';

export class SeatedCamera {
  yaw = 0;   // degrees
  pitch = 0; // degrees
  leanFwd = 0;   // eased -1..1 (back .. forward) — reported to the network; rendered by Avatar
  leanRight = 0; // eased -1..1 (left .. right)

  private activePointer = -1;
  private lastX = 0;
  private lastY = 0;
  private readonly keys = new Set<string>();

  constructor(private readonly camera: THREE.PerspectiveCamera, dom: HTMLElement) {
    // Pointer events cover mouse AND touch. Deltas are tracked manually because iOS
    // Safari does not report movementX/Y for touch pointers.
    dom.addEventListener('pointerdown', (e) => {
      this.activePointer = e.pointerId;
      this.lastX = e.clientX;
      this.lastY = e.clientY;
      dom.setPointerCapture?.(e.pointerId);
    });
    dom.addEventListener('pointermove', (e) => {
      const locked = document.pointerLockElement === dom;
      if (!locked && e.pointerId !== this.activePointer) return;
      const dx = locked ? e.movementX : e.clientX - this.lastX;
      const dy = locked ? e.movementY : e.clientY - this.lastY;
      this.lastX = e.clientX;
      this.lastY = e.clientY;
      const sens = e.pointerType === 'touch' ? CONFIG.touchSensitivity : CONFIG.mouseSensitivity;
      this.yaw = clamp(this.yaw + dx * sens, CONFIG.yawClampDeg);
      this.pitch = clamp(this.pitch - dy * sens, CONFIG.pitchClampDeg);
    });
    const endDrag = (e: PointerEvent) => {
      if (e.pointerId === this.activePointer) this.activePointer = -1;
    };
    dom.addEventListener('pointerup', endDrag);
    dom.addEventListener('pointercancel', endDrag);
    dom.addEventListener('dblclick', () => { void dom.requestPointerLock(); });

    // WASD lean — ignored while typing in a text field.
    window.addEventListener('keydown', (e) => {
      if (document.activeElement instanceof HTMLInputElement) return;
      const k = e.key.toLowerCase();
      if ('wasd'.includes(k)) this.keys.add(k);
    });
    window.addEventListener('keyup', (e) => this.keys.delete(e.key.toLowerCase()));
    window.addEventListener('blur', () => this.keys.clear());
  }

  sit(): void {
    this.camera.rotation.order = 'YXZ';
    this.yaw = 0;
    this.pitch = 0;
  }

  /** Overview shot before being seated. Detaches the camera in case it was parented to an
   * avatar in a previous room (leaving/host-migration) — position/lookAt are only meaningful
   * in world space, i.e. with no parent. */
  preview(): void {
    this.camera.removeFromParent();
    this.camera.rotation.set(0, 0, 0);
    this.camera.position.set(0, 2.4, -4.6);
    this.camera.lookAt(0, 0.8, 0);
  }

  /** Call every frame while seated. Only the look direction is ours to set — position and the
   * lean tilt come from the parent chain (see the file header). */
  update(seatIndex: number, dt: number): void {
    if (seatIndex < 0) return;

    // Held keys set the lean target; release eases back upright.
    const targetFwd = (this.keys.has('w') ? 1 : 0) - (this.keys.has('s') ? 1 : 0);
    const targetRight = (this.keys.has('d') ? 1 : 0) - (this.keys.has('a') ? 1 : 0);
    const ease = 1 - Math.exp(-CONFIG.leanEaseSpeed * dt);
    this.leanFwd += (targetFwd - this.leanFwd) * ease;
    this.leanRight += (targetRight - this.leanRight) * ease;

    // The parent (group) already contributes the seat's own yaw; we only add "face the table"
    // (180°) plus the user's look yaw/pitch.
    const yawRad = ((180 - this.yaw) * Math.PI) / 180;
    const pitchRad = (this.pitch * Math.PI) / 180;
    this.camera.rotation.set(pitchRad, yawRad, 0);
  }
}

function clamp(v: number, limit: number): number {
  return Math.max(-limit, Math.min(limit, v));
}
