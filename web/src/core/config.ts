// Room-level tunables (no magic numbers scattered in code).
export const CONFIG = {
  maxSeats: 10,
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
