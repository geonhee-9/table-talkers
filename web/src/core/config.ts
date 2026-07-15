// Room-level tunables (no magic numbers scattered in code).
export const CONFIG = {
  maxSeats: 10,
  headSyncHz: 15,
  protocolVersion: 2, // bump when the wire format changes; peers toast on mismatch
  poseLerpSpeed: 12,
  yawClampDeg: 150,
  pitchClampDeg: 75,
  mouseSensitivity: 0.16,
  touchSensitivity: 0.3,
  eyeHeight: 1.15,
  leanEaseSpeed: 8,     // how fast the upper body eases toward the WASD lean target
  // Max upper-body tilt angle. The camera now rotates around the actual hip pivot (it's parented
  // to the avatar, ~1.7m lever arm to the eye), so this angle is amplified into a much bigger
  // visual swing than the old position-only camera ever had — 20° here reads as a dramatic swoop,
  // not a conversational lean. Keep this modest for that reason.
  leanMaxDeg: 9,
  speakingThreshold: 0.06,
  stereoPanStrength: 0.45,
  signalUrl: (import.meta as { env?: Record<string, string> }).env?.VITE_SIGNAL_URL
    ?? `ws://${location.hostname}:8787`,
  stunServers: [{ urls: 'stun:stun.l.google.com:19302' }],
} as const;
