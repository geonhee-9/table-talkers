// Room-level tunables (no magic numbers scattered in code).
export const CONFIG = {
  maxSeats: 10,
  headSyncHz: 15,
  poseLerpSpeed: 12,
  yawClampDeg: 150,
  pitchClampDeg: 75,
  mouseSensitivity: 0.16,
  eyeHeight: 1.15,
  leanEaseSpeed: 8,     // how fast the upper body eases toward the WASD lean target
  leanShift: 0.34,      // metres the head shifts at full lean
  leanMaxDeg: 20,       // max upper-body tilt angle
  speakingThreshold: 0.06,
  stereoPanStrength: 0.45,
  signalUrl: (import.meta as { env?: Record<string, string> }).env?.VITE_SIGNAL_URL
    ?? `ws://${location.hostname}:8787`,
  stunServers: [{ urls: 'stun:stun.l.google.com:19302' }],
} as const;
