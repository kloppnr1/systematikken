import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// In development the API runs on :5180 and Vite forwards /api and /auth to it.
// The production build goes straight into the API's wwwroot, so one container serves both.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5180",
      "/auth": "http://localhost:5180",
    },
  },
  build: {
    outDir: "../src/Systematikken.Api/wwwroot",
    emptyOutDir: true,
  },
});
