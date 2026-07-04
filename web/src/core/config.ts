// Room-level tunables (no magic numbers scattered in code).
export const CONFIG = {
  seatCount: 4,
  headSyncHz: 15,
  poseLerpSpeed: 12,
  yawClampDeg: 150,
  pitchClampDeg: 75,
  mouseSensitivity: 0.16,
  eyeHeight: 1.15,
  speakingThreshold: 0.06,
  stereoPanStrength: 0.45,
  signalUrl: (import.meta as { env?: Record<string, string> }).env?.VITE_SIGNAL_URL
    ?? `ws://${location.hostname}:8787`,
  stunServers: [{ urls: 'stun:stun.l.google.com:19302' }],
} as const;

// Seat anchors around the round table (must match the lounge layout).
export interface SeatAnchor { x: number; z: number; yawDeg: number }
export const SEAT_ANCHORS: SeatAnchor[] = [
  { x: 0, z: -1.6, yawDeg: 0 },
  { x: 1.6, z: 0, yawDeg: -90 },
  { x: 0, z: 1.6, yawDeg: 180 },
  { x: -1.6, z: 0, yawDeg: 90 },
];
