import tailwindcss from "@tailwindcss/vite";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import viteReact from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import tsConfigPaths from "vite-tsconfig-paths";

// Project Pages URL: https://gixx.github.io/snowrunner-tuning-shop/
const BASE = "/snowrunner-tuning-shop/";

export default defineConfig({
  base: BASE,
  plugins: [
    tsConfigPaths({ projects: ["./tsconfig.json"] }),
    tanstackStart({
      spa: {
        enabled: true,
        prerender: {
          outputPath: "/index.html",
          crawlLinks: true,
        },
      },
    }),
    viteReact(),
    tailwindcss(),
  ],
});
