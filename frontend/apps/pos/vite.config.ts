import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  return {
    // Served by nginx under /pos/ (see frontend/nginx.conf).
    base: "/pos/",
    plugins: [react()],
    server: {
      port: 5174,
      proxy: { "/api": env.API_PROXY_TARGET || "http://localhost:63842" },
    },
  };
});
