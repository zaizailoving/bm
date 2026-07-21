/**
 * 后端 BM.Service 地址配置
 *
 * H5 开发：留空字符串，请求走相对路径 /api/*，由 Vite proxy 转发到后端（见 vite.config.ts）
 * 小程序 / App 真机：改为电脑局域网 IP，例如 http://192.168.1.10:20011
 * 生产：改为正式域名
 */
export const BASE_URL = ''

/** 非 H5 时可在此覆盖（小程序调试请改这里） */
// export const BASE_URL = 'http://localhost:20011'

/** 请求超时（毫秒） */
export const REQUEST_TIMEOUT = 15000
