// The participant abstraction — the only way gameplay/UI code talks about seat occupants.
// Invariant: never assume a participant is a human peer (future: agents, shared surfaces).

export interface Participant {
  readonly id: string;
  readonly isLocal: boolean;
  name: string;
  seatIndex: number; // -1 = unseated
  headYaw: number;   // degrees, relative to seat forward
  headPitch: number; // degrees, positive = up
  speaking: boolean;
}

type Listener = (p: Participant) => void;

class Registry {
  private readonly participants = new Map<string, Participant>();
  readonly onAdded = new Set<Listener>();
  readonly onRemoved = new Set<Listener>();

  add(p: Participant): void {
    if (this.participants.has(p.id)) return;
    this.participants.set(p.id, p);
    for (const fn of this.onAdded) fn(p);
  }

  remove(id: string): void {
    const p = this.participants.get(id);
    if (!p) return;
    this.participants.delete(id);
    for (const fn of this.onRemoved) fn(p);
  }

  get(id: string): Participant | undefined {
    return this.participants.get(id);
  }

  all(): Participant[] {
    return [...this.participants.values()];
  }

  local(): Participant | undefined {
    return this.all().find((p) => p.isLocal);
  }

  clear(): void {
    for (const p of this.all()) this.remove(p.id);
  }
}

export const participants = new Registry();
