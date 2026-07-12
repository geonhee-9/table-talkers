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
    // Face the table (seat forward = anchor yaw + 180 in world), then add mouse look.
    const yawRad = ((a.yawDeg + 180 - this.yaw) * Math.PI) / 180;

    // Forward = toward the table centre. Right = the camera's actual screen-right so that D
    // moves the viewpoint right AND banks right (they used to fight: the old "right" was the
    // seat's anatomical right, which is screen-LEFT once the camera faces the table).
    const fx = -a.x, fz = -a.z;
    const flen = Math.hypot(fx, fz) || 1;
    const fwd = { x: fx / flen, z: fz / flen };
    const camRight = { x: Math.cos(yawRad), z: -Math.sin(yawRad) };

    const shift = CONFIG.leanShift;
    const px = a.x + (fwd.x * this.leanFwd + camRight.x * this.leanRight) * shift;
    const pz = a.z + (fwd.z * this.leanFwd + camRight.z * this.leanRight) * shift;
    // Leaning dips the head a little (you don't just slide — you tip).
    const py = 0.55 + CONFIG.eyeHeight
      - Math.abs(this.leanFwd) * 0.1 - Math.abs(this.leanRight) * 0.05;
    this.camera.position.set(px, py, pz);

    const pitchRad = (this.pitch * Math.PI) / 180;
    const rollRad = (-this.leanRight * CONFIG.leanRollDeg * Math.PI) / 180;
    this.camera.rotation.set(pitchRad, yawRad, rollRad);
  }
}

function clamp(v: number, limit: number): number {
  return Math.max(-limit, Math.min(limit, v));
}
