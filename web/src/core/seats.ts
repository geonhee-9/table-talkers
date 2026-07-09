// Dynamic round-table seat layout: N seated participants → N chairs evenly spaced on a
// circle, always facing the centre (2 people sit opposite each other, 3 at 120°, …).
// The chair circle and the table itself grow with the party. Single source of truth —
// camera, avatars, voice panning and the scene all read from here.
import { CONFIG } from './config';

export interface SeatAnchor { x: number; z: number; yawDeg: number }

class SeatLayoutStore {
  private count = 2; // a table is never smaller than two places (an empty chair invites)

  /** Fired when the layout (seat count) changes — the scene rebuilds chairs/table. */
  readonly onChanged = new Set<() => void>();

  get seatCount(): number {
    return this.count;
  }

  setCount(n: number): void {
    const next = Math.max(2, Math.min(CONFIG.maxSeats, n));
    if (next === this.count) return;
    this.count = next;
    for (const fn of this.onChanged) fn();
  }

  /** Chair circle radius — grows past 5 people so nobody rubs shoulders. */
  chairRadius(): number {
    return this.count <= 5 ? 1.6 : 1.6 + (this.count - 5) * 0.14;
  }

  /** Table radius — keeps a constant reach gap to the chairs. */
  tableRadius(): number {
    return Math.max(0.75, this.chairRadius() - 0.85);
  }

  /** World transform of a seat: position on the circle, yaw facing the centre. */
  anchor(index: number): SeatAnchor {
    const theta = (index / this.count) * Math.PI * 2;
    const r = this.chairRadius();
    return {
      x: r * Math.sin(theta),
      z: -r * Math.cos(theta),
      yawDeg: -(theta * 180) / Math.PI,
    };
  }
}

export const seatLayout = new SeatLayoutStore();
