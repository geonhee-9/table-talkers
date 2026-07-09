import { defineConfig } from 'vite';

// Static build for Vercel. The signaling server (web/server/signal.js) deploys
// separately to Render — its URL is injected at build time via VITE_SIGNAL_URL.
export default defineConfig({
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
