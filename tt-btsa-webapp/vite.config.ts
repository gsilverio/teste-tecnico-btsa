import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";
import { defineConfig, loadEnv } from "vite";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  return {
    base: env.VITE_APP_BASE_URL || '/',
    plugins: [react(), babel({ presets: [reactCompilerPreset()] })],
    server: {
      port: 3001,
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://127.0.0.1:5154',
          changeOrigin: true,
        },
      },
    },
  };
});
