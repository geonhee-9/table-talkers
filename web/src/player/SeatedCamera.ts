// Seated first-person look: no locomotion, mouse-drag (or pointer lock) head rotation
// with yaw/pitch clamps relative to the seat's forward. Position/orientation follow the
// dynamic seat layout every frame, so the camera glides when the table rearranges.
import * as THREE from 'three';
import { CONFIG } from '../core/config';
import { seatLayout } from '../core/seats';

export class SeatedCamera {
  yaw = 0;   // degrees
  pitch = 0; // degrees
  private dragging = false;

  constructor(private readonly camera: THREE.PerspectiveCamera, dom: HTMLElement) {
    dom.addEventListener('mousedown', () => { this.dragging = true; });
    window.addEventListener('mouseup', () => { this.dragging = false; });
    window.addEventListener('mousemove', (e) => {
      if (!this.dragging && document.pointerLockElement !== dom) return;
      this.yaw = clamp(this.yaw + e.movementX * CONFIG.mouseSensitivity, CONFIG.yawClampDeg);
      this.pitch = clamp(this.pitch - e.movementY * CONFIG.mouseSensitivity, CONFIG.pitchClampDeg);
    });
    dom.addEventListener('dblclick', () => {
      void dom.requestPointerLock();
    });
  }

  /** Reset the gaze when first taking a seat. */
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

  /** Call every frame while seated — tracks the (possibly resized) layout. */
  update(seatIndex: number): void {
    if (seatIndex < 0) return;
    const a = seatLayout.anchor(seatIndex);
    this.camera.position.set(a.x, 0.55 + CONFIG.eyeHeight, a.z);
    // Face the table (seat forward = anchor yaw + 180 in world), then add look offsets.
    const yawRad = ((a.yawDeg + 180 - this.yaw) * Math.PI) / 180;
    const pitchRad = (this.pitch * Math.PI) / 180;
    this.camera.rotation.set(pitchRad, yawRad, 0);
  }
}

function clamp(v: number, limit: number): number {
  return Math.max(-limit, Math.min(limit, v));
}
