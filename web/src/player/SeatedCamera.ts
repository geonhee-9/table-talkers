// Seated first-person: mouse-drag head look (yaw/pitch clamped) + WASD upper-body lean
// (lower body stays fixed on the chair). Position/orientation follow the dynamic seat
// layout every frame, so the camera glides when the table rearranges.
import * as THREE from 'three';
import { CONFIG } from '../core/config';
import { seatLayout } from '../core/seats';

export class SeatedCamera {
  yaw = 0;   // degrees
  pitch = 0; // degrees
  leanFwd = 0;   // smoothed -1..1 (back .. forward)
  leanRight = 0; // smoothed -1..1 (left .. right)

  private dragging = false;
  private readonly keys = new Set<string>();

  constructor(private readonly camera: THREE.PerspectiveCamera, dom: HTMLElement) {
    dom.addEventListener('mousedown', () => { this.dragging = true; });
    window.addEventListener('mouseup', () => { this.dragging = false; });
    window.addEventListener('mousemove', (e) => {
      if (!this.dragging && document.pointerLockElement !== dom) return;
      this.yaw = clamp(this.yaw + e.movementX * CONFIG.mouseSensitivity, CONFIG.yawClampDeg);
      this.pitch = clamp(this.pitch - e.movementY * CONFIG.mouseSensitivity, CONFIG.pitchClampDeg);
    });
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

  /** Overview shot before being seated. */
  preview(): void {
    this.camera.position.set(0, 2.4, -4.6);
    this.camera.lookAt(0, 0.8, 0);
  }

  /** Call every frame while seated — tracks lean input + the (possibly resized) layout. */
  update(seatIndex: number, dt: number): void {
    if (seatIndex < 0) return;

    // Held keys set the lean target; release eases back upright.
    const targetFwd = (this.keys.has('w') ? 1 : 0) - (this.keys.has('s') ? 1 : 0);
    const targetRight = (this.keys.has('d') ? 1 : 0) - (this.keys.has('a') ? 1 : 0);
    const ease = 1 - Math.exp(-CONFIG.leanEaseSpeed * dt);
    this.leanFwd += (targetFwd - this.leanFwd) * ease;
    this.leanRight += (targetRight - this.leanRight) * ease;

    const a = seatLayout.anchor(seatIndex);
    // Seat frame: forward points at the table centre, right is 90° clockwise of it.
    const fx = -a.x, fz = -a.z;
    const flen = Math.hypot(fx, fz) || 1;
    const fwd = { x: fx / flen, z: fz / flen };
    const right = { x: fwd.z, z: -fwd.x };

    const shift = CONFIG.leanShift;
    const px = a.x + (fwd.x * this.leanFwd + right.x * this.leanRight) * shift;
    const pz = a.z + (fwd.z * this.leanFwd + right.z * this.leanRight) * shift;
    // Leaning dips the head a little (you don't just slide — you tip).
    const py = 0.55 + CONFIG.eyeHeight
      - Math.abs(this.leanFwd) * 0.1 - Math.abs(this.leanRight) * 0.05;
    this.camera.position.set(px, py, pz);

    // Face the table (seat forward = anchor yaw + 180 in world), then add look + a subtle
    // roll into the sideways lean.
    const yawRad = ((a.yawDeg + 180 - this.yaw) * Math.PI) / 180;
    const pitchRad = (this.pitch * Math.PI) / 180;
    const rollRad = (-this.leanRight * 7 * Math.PI) / 180;
    this.camera.rotation.set(pitchRad, yawRad, rollRad);
  }
}

function clamp(v: number, limit: number): number {
  return Math.max(-limit, Math.min(limit, v));
}
