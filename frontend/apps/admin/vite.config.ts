import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  return {
    plugins: [react()],
    server: {
      port: 5173,
      // Same-origin proxy so the browser never needs CORS against the .NET API.
      proxy: {
        "/api": env.API_PROXY_TARGET || "http://localhost:63842",
      },
    },
  };
});
