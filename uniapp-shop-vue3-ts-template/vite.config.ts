import { defineConfig } from 'vite'
import uni from '@dcloudio/vite-plugin-uni'

// https://vitejs.dev/config/
export default defineConfig({
  build: {
    // 开发阶段启用源码映射：https://uniapp.dcloud.net.cn/tutorial/migration-to-vue3.html#需主动开启-sourcemap
    sourcemap: process.env.NODE_ENV === 'development',
  },
  plugins: [uni()],
  // H5 开发代理到 BM.Service，避免浏览器跨域
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:20011',
        changeOrigin: true,
      },
    },
  },
})
