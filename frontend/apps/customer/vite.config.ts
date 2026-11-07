import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  return {
    // Customer web is the site root; nginx serves /admin and /pos separately.
    base: "/",
    plugins: [react()],
    server: {
      port: 5175,
      proxy: { "/api": env.API_PROXY_TARGET || "http://localhost:63842" },
    },
  };
});
