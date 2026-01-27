import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"
import topLevelAwait from "vite-plugin-top-level-await"
import viteCompression from "vite-plugin-compression"

// https://vitejs.dev/config/
export default defineConfig({
  base: "/",
  plugins: [
    react(),
    viteCompression({
      verbose: true, // 默认即可
      disable: false, // 开启压缩(不禁用)，默认即可
      deleteOriginFile: false, // 删除源文件
      threshold: 5120, // 压缩前最小文件大小
      algorithm: "gzip", // 压缩算法
      ext: ".gz", // 文件类型
    }),
    topLevelAwait({
      // The export name of top-level await promise for each chunk module
      promiseExportName: "__tla",
      // The function to generate import names of top-level await promise in each chunk module
      promiseImportName: (i) => `__tla_${i}`,
    }),
  ],
  build: {
    minify: true,
  },
})
