<template>
  <view class="face-cam-root">
    <!--
      renderjs 绑定：:change:prop 的 faceCam 是 module 名，TS 报错可忽略
      逻辑层用 Options API methods，保证 App 端 callMethod 能回调
    -->
    <view
      id="face-cam-host"
      class="face-cam-host"
      :prop="cmd"
      :change:prop="faceCam.onCmd"
    />
  </view>
</template>

<script lang="ts">
/**
 * App 内前置摄像头 + 唇部动作识别
 * - 逻辑层：收 renderjs 事件，向上抛 snapshot/error/started
 *   PLUS：使用 plus.io 预读本地模型文件，转 base64 后传给 renderjs
 * - renderjs：真 DOM getUserMedia + MediaPipe Face Mesh
 */
import type { FaceLipSnapshot } from '@/utils/faceLip'

type CamCmd = {
  action: 'idle' | 'start' | 'stop' | 'beginModelTransfer'
  token: number
  ts: number
  /** 模型文件传输：fileName → base64 */
  files?: Record<string, string>
}

type RenderPayload = {
  type: 'snapshot' | 'error' | 'started' | 'status' | 'modelsReceived'
  data?: FaceLipSnapshot
  message?: string
}

/** 需要传给 renderjs 的模型文件名列表（位于 static/ 根目录下） */
const MODEL_FILES = [
  'face_mesh.binarypb',
  'face_mesh_solution_packed_assets.data',
  'face_mesh_solution_simd_wasm_bin.wasm',
  'face_mesh_solution_wasm_bin.wasm',
]

/**
 * 检测当前运行平台类型。
 * 返回 'android' | 'ios' | 'devtools' | 'other'
 */
function detectPlatform(): string {
  try {
    var sysInfo = (uni as any).getSystemInfoSync()
    if (sysInfo && sysInfo.platform) return String(sysInfo.platform).toLowerCase()
  } catch (e) {
    /* ignore */
  }
  // 兜底：检查 plus 是否存在（App 环境一定有 plus）
  try {
    if (typeof (globalThis as any).plus !== 'undefined') return 'android'
  } catch (e) {
    /* ignore */
  }
  return 'other'
}

function hasPlusRuntime(): boolean {
  try {
    return typeof (globalThis as any).plus !== 'undefined'
  } catch {
    return false
  }
}

/**
 * 读取应用内文件并返回 base64 字符串（带超时保护）。
 * - APP 真机（Android/iOS）：禁止进入 uniFS 分支，只走 plus.io
 * - devtools/H5：uniFS 优先，plus.io catch 兜底
 * 优先读取 uni-app 会打包的 src/static/mediapipe/face_mesh 目录。
 */
function readLocalFileAsBase64(fileName: string, timeoutMs: number = 8000): Promise<string> {
  var filePaths = [
    '_www/static/mediapipe/face_mesh/' + fileName,
    '_www/static/' + fileName,
  ]
  var platform = detectPlatform()
  var isRealDevice = platform === 'android' || platform === 'ios'

  console.log('[FaceCam] ========================================')
  console.log('[FaceCam] 平台: ' + platform + ' | 文件: ' + fileName)
  console.log('[FaceCam] 候选路径: ' + filePaths.join(' | '))
  console.log('[FaceCam] 真机: ' + (isRealDevice ? 'YES → 仅 plus.io（禁止 uniFS）' : 'NO → uniFS 优先'))

  // 策略A：uni.getFileSystemManager（仅 devtools/H5 使用，APP 真机禁止进入）
  function tryUniFS(filePath: string): Promise<string> {
    return new Promise(function (resolve, reject) {
      console.log('[FaceCam] uniFS 开始读取: ' + filePath)
      try {
        var fs = (uni as any).getFileSystemManager()
        if (!fs || !fs.readFile) {
          var msg = 'uni.getFileSystemManager 不可用'
          console.warn('[FaceCam] uniFS: ' + msg)
          reject(new Error(msg))
          return
        }
        fs.readFile({
          filePath: filePath,
          encoding: 'base64' as any,
          success: function (res: any) {
            if (res && res.data) {
              var size = (res.data as string).length
              console.log('[FaceCam] uniFS 成功: ' + fileName + ' base64长度=' + size)
              resolve(res.data as string)
            } else {
              console.warn('[FaceCam] uniFS: readFile 返回空数据')
              reject(new Error('readFile 返回空数据'))
            }
          },
          fail: function (err: any) {
            console.warn('[FaceCam] uniFS 失败: ' + fileName + ' err=' + JSON.stringify(err))
            reject(new Error('uni.readFile 失败: ' + JSON.stringify(err)))
          },
        })
      } catch (e: any) {
        console.warn('[FaceCam] uniFS 异常: ' + fileName + ' err=' + (e && e.message))
        reject(new Error('uniFS 异常: ' + (e && e.message)))
      }
    })
  }

  // 策略B：plus.io（APP 真机唯一方案）
  function tryPlusIO(filePath: string): Promise<string> {
    return new Promise(function (resolve, reject) {
      var plus = (globalThis as any).plus
      if (!plus || !plus.io) {
        var msg = 'plus.io 不可用（当前环境无 plus 运行时）'
        console.warn('[FaceCam] plus.io: ' + msg)
        reject(new Error(msg))
        return
      }
      console.log('[FaceCam] plus.io 开始解析路径: ' + filePath)
      plus.io.resolveLocalFileSystemURL(
        filePath,
        function (entry: any) {
          console.log('[FaceCam] plus.io 路径解析成功: ' + filePath + ' | isFile=' + entry.isFile + ' | fullPath=' + (entry.fullPath || 'N/A'))
          entry.file(
            function (file: any) {
              var sizeKB = file.size ? (file.size / 1024).toFixed(1) : '?'
              console.log('[FaceCam] plus.io 获取文件对象: ' + fileName + ' size=' + sizeKB + 'KB')
              var reader = new plus.io.FileReader()
              reader.onloadend = function (e: any) {
                var buf = e.target.result as ArrayBuffer
                if (!buf || buf.byteLength === 0) {
                  console.warn('[FaceCam] plus.io FileReader 返回空数据: ' + fileName)
                  reject(new Error('FileReader 返回空数据'))
                  return
                }
                var bytes = new Uint8Array(buf)
                // Non-final chunks must be a multiple of 3 bytes, otherwise
                // concatenating btoa(chunk) corrupts the binary model on device.
                var chunkSize = 0x6000
                var chunks: string[] = []
                for (var i = 0; i < bytes.length; i += chunkSize) {
                  var chunk = bytes.subarray(i, i + chunkSize)
                  var binary = ''
                  for (var j = 0; j < chunk.length; j++) {
                    binary += String.fromCharCode(chunk[j])
                  }
                  chunks.push(btoa(binary))
                }
                var result = chunks.join('')
                console.log('[FaceCam] plus.io 成功: ' + fileName + ' base64长度=' + result.length + ' 原始=' + (buf.byteLength / 1024).toFixed(1) + 'KB')
                resolve(result)
              }
              reader.onerror = function () {
                console.warn('[FaceCam] plus.io FileReader 读取失败: ' + fileName)
                reject(new Error('FileReader 读取失败'))
              }
              reader.readAsArrayBuffer(file)
            },
            function (e: any) {
              console.warn('[FaceCam] plus.io 获取文件失败: ' + fileName + ' code=' + (e && e.code))
              reject(new Error('获取文件失败: code=' + (e && e.code)))
            },
          )
        },
        function (e: any) {
          console.warn('[FaceCam] plus.io 未找到文件: ' + filePath + ' code=' + (e && e.code))
          reject(new Error('未找到文件: code=' + (e && e.code)))
        },
      )
    })
  }

  // ====== 平台分支 ======
  if (isRealDevice) {
    // Android/iOS：禁止进入 uniFS 分支，只走 plus.io
    console.log('[FaceCam] APP真机模式 → 仅 plus.io（禁止 uniFS）: ' + fileName)
    return new Promise(function (resolve, reject) {
      var settled = false
      var timer = setTimeout(function () {
        if (settled) return
        settled = true
        console.error('[FaceCam] plus.io 超时 (' + timeoutMs + 'ms): ' + fileName)
        reject(new Error('读取超时 (' + timeoutMs + 'ms): ' + fileName))
      }, timeoutMs)

      var lastErr: any = null
      function tryNext(index: number) {
        if (settled) return
        if (index >= filePaths.length) {
          settled = true
          clearTimeout(timer)
          console.error('[FaceCam] ✗ plus.io 全部候选路径失败: ' + fileName + ' err=' + (lastErr && lastErr.message))
          reject(lastErr || new Error('未找到文件: ' + fileName))
          return
        }
        tryPlusIO(filePaths[index]).then(function (result) {
          if (settled) return
          settled = true
          clearTimeout(timer)
          console.log('[FaceCam] ✓ 文件就绪: ' + fileName + ' @ ' + filePaths[index])
          resolve(result)
        }).catch(function (err: any) {
          lastErr = err
          tryNext(index + 1)
        })
      }
      tryNext(0)
    })
  }

  // devtools/H5：uniFS 优先，plus.io catch 兜底
  console.log('[FaceCam] devtools/H5模式 → uniFS 优先: ' + fileName)
  return new Promise(function (resolve, reject) {
    var settled = false
    var timer = setTimeout(function () {
      if (settled) return
      settled = true
      console.error('[FaceCam] 读取超时 (' + timeoutMs + 'ms): ' + fileName)
      reject(new Error('读取超时 (' + timeoutMs + 'ms): ' + fileName))
    }, timeoutMs)

    function done(result: string) {
      if (settled) return
      settled = true
      clearTimeout(timer)
      resolve(result)
    }
    function fallbackToPlusIO(uniErr: any) {
      if (settled) return
      console.warn('[FaceCam] uniFS 失败 → 降级到 plus.io: ' + fileName + ' uniErr=' + (uniErr && uniErr.message))
      var lastPlusErr: any = null
      function tryPlusAt(index: number) {
        if (settled) return
        if (index >= filePaths.length) {
          settled = true
          clearTimeout(timer)
          console.error('[FaceCam] uniFS+plus.io 均失败: ' + fileName)
          reject(new Error('uniFS+plus.io 均失败: ' + fileName + ' err=' + (lastPlusErr && lastPlusErr.message)))
          return
        }
        tryPlusIO(filePaths[index]).then(done).catch(function (plusErr: any) {
          lastPlusErr = plusErr
          tryPlusAt(index + 1)
        })
      }
      tryPlusAt(0)
    }

    function tryUniAt(index: number) {
      if (settled) return
      if (index >= filePaths.length) {
        fallbackToPlusIO(new Error('uniFS 候选路径均失败'))
        return
      }
      tryUniFS(filePaths[index]).then(done).catch(function (uniErr: any) {
        if (settled) return
        console.warn('[FaceCam] uniFS 候选失败: ' + filePaths[index] + ' err=' + (uniErr && uniErr.message))
        tryUniAt(index + 1)
      })
    }

    tryUniAt(0)
  })
}

export default {
  name: 'FaceCam',
  props: {
    /** >0 启动；0 停止；变化且 >0 可重启 */
    runToken: {
      type: Number,
      default: 0,
    },
  },
  emits: ['snapshot', 'error', 'started', 'status'],
  data() {
    return {
      cmd: { action: 'idle', token: 0, ts: 0 } as CamCmd,
      /** 正在传输模型文件时忽略后续的 stop/start */
      transferring: false,
    }
  },
  watch: {
    runToken: {
      immediate: true,
      handler(token: number, prev?: number) {
        if (token > 0 && token !== prev) {
          // ★ 先启动摄像头，不等待模型文件
          this.cmd = { action: 'start', token, ts: Date.now() }
          // App 真机直接由 renderjs 读取包内 static 资源；plus.io 读大模型再 base64 传输很容易超时。
          if (!hasPlusRuntime()) {
            this._preloadModelsBackground(token)
          }
        } else if (!token || token <= 0) {
          this.cmd = { action: 'stop', token: 0, ts: Date.now() }
        }
      },
    },
  },
  beforeUnmount() {
    this.cmd = { action: 'stop', token: 0, ts: Date.now() }
  },
  methods: {
    /**
     * 后台加载本地模型文件（不阻塞摄像头启动）
     * 每个文件最多等 4 秒，加载成功则通过 beginModelTransfer 传给 renderjs
     */
    async _preloadModelsBackground(token: number) {
      await Promise.all(MODEL_FILES.map(async (fileName) => {
        try {
          const base64 = await readLocalFileAsBase64(fileName, 10000)
          // 文件加载成功 → 传给 renderjs（它会在 ensureModel 中使用）
          this.cmd = {
            action: 'beginModelTransfer',
            token,
            ts: Date.now(),
            files: { [fileName]: base64 },
          }
        } catch (err: any) {
          // 超时或 error 都跳过，renderjs 会回退到 CDN
          console.warn('[FaceCam 逻辑层] 模型文件 ' + fileName + ' 加载失败: ' + (err && err.message))
        }
      }))
    },

    /** 必须是 methods，App 端 renderjs 的 callMethod 才能调到 */
    onRenderEvent(payload: RenderPayload) {
      if (!payload || !payload.type) return
      if (payload.type === 'snapshot' && payload.data) {
        this.$emit('snapshot', payload.data)
      } else if (payload.type === 'error') {
        this.$emit('error', payload.message || '摄像头启动失败')
      } else if (payload.type === 'started') {
        this.$emit('started')
      } else if (payload.type === 'status' && payload.message) {
        this.$emit('status', payload.message)
      }
      // modelsReceived 仅用于内部通信，不向上抛出
    },
  },
}
</script>

<script module="faceCam" lang="renderjs">
/**
 * App WebView / H5 真 DOM
 * 不使用 ESM dynamic import（App 内经常失败），改用 <script> 加载 Face Mesh
 *
 * 关键修复（手机端一直「寻找人脸」）：
 * 1. 不要直接把 video 丢给 MediaPipe（部分 App WebView 读不到帧）→ 先画到 canvas 再 send
 * 2. send 卡住时 sending 永久 true → 超时强制释放
 * 3. 长时间无 onResults 时给出明确状态提示
 * 4. 置信度放宽 + selfieMode
 *
 * 本轮修复：
 * 5. emitToLogic 优先使用 $ownerInstance（uni-app renderjs 标准通道）
 * 6. grabFrame canvas 兼容性增强（不依赖 willReadFrequently）
 * 7. sending 超时清理逻辑修复（避免双定时器泄露）
 * 8. loadFaceMeshLib 本地路径优先 + 诊断日志
 * 9. ensureModel WASM 加载路径修复
 */

/**
 * 模型路径：本地 static 优先（App 内无需外网），CDN 仅作兜底
 * 文件位置：src/static/mediapipe/face_mesh/*
 */
function buildPackageRoots() {
  var roots = []

  // ⚠️ 关键：不要使用 plus.io.convertLocalFileSystemURL，它会返回绝对文件系统路径
  // （如 /storage/emulated/0/.../www/...），Android WebView 的 XHR 无法加载这种路径！
  //
  // 另外：_www/ 前缀仅对 uni-app 拦截的 <script> 加载有效，对 XHR/fetch 无效。
  // locateFile 会被 MediaPipe WASM 加载器内部用于 XHR，所以必须使用真实相对路径。
  // 幸运的是，HTML 页面位于 www/ 目录，因此 "static/..." 相对路径对 script 和 XHR 均有效。

  var isApp = false
  try {
    if (typeof plus !== 'undefined') isApp = true
  } catch (e) { /* ignore */ }

  // 真实相对路径（优先）：HTML 页面在 www/ 目录，文件在 static/mediapipe/face_mesh
  // 该路径对 <script> 和 XHR 均有效
  roots.push('static/mediapipe/face_mesh')
  roots.push('./static/mediapipe/face_mesh')
  // 兼容旧包：曾经把 mediapipe 文件放在 static 根目录
  roots.push('static')
  roots.push('./static')

  // App 环境：_www/ 路径可用于 <script> 标签（uni-app 拦截），但 XHR 无法使用
  // 将其保留在候选列表中，以便在 XHR 不支持时回退
  if (isApp) {
    roots.push('_www/static/mediapipe/face_mesh')
    roots.push('_www/static')
  }

  // 从当前页面 URL 推断路径（H5 开发常见场景）
  try {
    if (typeof location !== 'undefined' && location.href) {
      var href = String(location.href).split('#')[0].split('?')[0]
      var base = href.replace(/\/[^/]*$/, '/')
      var fromBase = base + 'static/mediapipe/face_mesh'
      if (roots.indexOf(fromBase) < 0) roots.push(fromBase)
      var legacyFromBase = base + 'static'
      if (roots.indexOf(legacyFromBase) < 0) roots.push(legacyFromBase)

      if (location.origin) {
        var fromOrigin = String(location.origin).replace(/\/$/, '') + '/static/mediapipe/face_mesh'
        if (roots.indexOf(fromOrigin) < 0) roots.push(fromOrigin)
        var legacyFromOrigin = String(location.origin).replace(/\/$/, '') + '/static'
        if (roots.indexOf(legacyFromOrigin) < 0) roots.push(legacyFromOrigin)
      }
    }
  } catch (e2) {
    /* ignore */
  }

  // 外网兜底（无本地文件或 H5 调试时，script 和 XHR 均可使用）
  var cdn = [
    'https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4.1633559619',
    'https://unpkg.com/@mediapipe/face_mesh@0.4.1633559619',
  ]
  for (var j = 0; j < cdn.length; j++) {
    var r = String(cdn[j] || '').replace(/\/$/, '')
    if (r && roots.indexOf(r) < 0) roots.push(r)
  }
  return roots
}

const PACKAGE_ROOTS = buildPackageRoots()
const LOCAL_MODEL_FILES = [
  'face_mesh.binarypb',
  'face_mesh_solution_packed_assets.data',
  'face_mesh_solution_simd_wasm_bin.wasm',
  'face_mesh_solution_wasm_bin.wasm',
]

function getMediapipeFileName(url) {
  var raw = String(url || '')
  if (!raw) return ''
  var clean = raw.split('#')[0].split('?')[0]
  var fileName = clean.replace(/^.*[\\/]/, '')
  for (var i = 0; i < LOCAL_MODEL_FILES.length; i++) {
    if (fileName === LOCAL_MODEL_FILES[i]) return fileName
  }
  return ''
}

function readPackagedMediapipeFile(fileName) {
  if (!fileName) return Promise.reject(new Error('empty mediapipe file name'))
  if (typeof window !== 'undefined') {
    window.__faceCamFileCache = window.__faceCamFileCache || {}
    if (window.__faceCamFileCache[fileName]) return window.__faceCamFileCache[fileName]
  }

  var promise = new Promise(function (resolve, reject) {
    try {
      if (typeof plus === 'undefined' || !plus.io) {
        reject(new Error('plus.io unavailable'))
        return
      }
      var path = '_www/static/mediapipe/face_mesh/' + fileName
      plus.io.resolveLocalFileSystemURL(
        path,
        function (entry) {
          entry.file(
            function (file) {
              var reader = new plus.io.FileReader()
              reader.onloadend = function (e) {
                var buf = e && e.target ? e.target.result : null
                if (!buf || !buf.byteLength) {
                  reject(new Error('empty package file: ' + fileName))
                  return
                }
                console.log('[FaceCam] plus.io package ArrayBuffer: ' + fileName + ' ' + Math.round(buf.byteLength / 1024) + 'KB')
                resolve(buf)
              }
              reader.onerror = function () {
                reject(new Error('read package file failed: ' + fileName))
              }
              reader.readAsArrayBuffer(file)
            },
            function (e) {
              reject(new Error('entry.file failed: ' + fileName + ' code=' + (e && e.code)))
            },
          )
        },
        function (e) {
          reject(new Error('resolve package file failed: ' + fileName + ' code=' + (e && e.code)))
        },
      )
    } catch (e) {
      reject(e)
    }
  })

  if (typeof window !== 'undefined') window.__faceCamFileCache[fileName] = promise
  return promise
}

/**
 * App WebView 禁止 fetch(file://...)，MediaPipe 的 wasm/graph 会踩到这里。
 * 用 plus.io 按需读取包内文件，再包装成 Response。
 */
;(function patchFetchForFileUrls() {
  if (typeof window === 'undefined') return
  if (typeof fetch === 'undefined') return

  var originalFetch = window.fetch
  if (originalFetch._patchedForFile) return
  window.__faceCamOriginalFetch = originalFetch

  window.fetch = function (input, init) {
    var url = ''
    if (typeof input === 'string') {
      url = input
    } else if (input && typeof input.url === 'string') {
      url = input.url
    }

    var fileName = getMediapipeFileName(url)
    if (fileName && (url.indexOf('file://') === 0 || url.indexOf('static/') === 0 || url.indexOf('./static/') === 0)) {
      console.log('[FaceCam] fetch plus.io intercept: ' + fileName)
      return readPackagedMediapipeFile(fileName).then(function (buf) {
        return new Response(buf, { status: 200, statusText: 'OK' })
      })
    }

    return originalFetch.call(window, input, init)
  }
  window.fetch._patchedForFile = true
  console.log('[FaceCam] fetch file:// 补丁已安装 (plus.io ArrayBuffer)')
})()

;(function patchXHRForPackedAssets() {
  if (typeof window === 'undefined') return
  if (typeof XMLHttpRequest === 'undefined') return
  var NativeXHR = window.XMLHttpRequest
  if (NativeXHR._patchedForFaceCam) return

  function PatchedXHR() {
    this._native = null
    this._url = ''
    this._fileName = ''
    this.readyState = 0
    this.status = 0
    this.statusText = ''
    this.response = null
    this.responseText = ''
    this.responseType = ''
    this.responseURL = ''
    this.onreadystatechange = null
    this.onload = null
    this.onerror = null
    this.onprogress = null
  }

  PatchedXHR.prototype.open = function (method, url, async, user, password) {
    this._url = String(url || '')
    this._fileName = getMediapipeFileName(this._url)
    if (!this._fileName || (this._url.indexOf('http://') === 0 || this._url.indexOf('https://') === 0)) {
      this._native = new NativeXHR()
      this._bindNativeEvents()
      return this._native.open(method, url, async, user, password)
    }
    this.readyState = 1
    this.responseURL = this._url
    if (typeof this.onreadystatechange === 'function') this.onreadystatechange({ target: this })
  }

  PatchedXHR.prototype._bindNativeEvents = function () {
    var self = this
    var native = this._native
    if (!native) return
    native.onreadystatechange = function (event) {
      if (typeof self.onreadystatechange === 'function') self.onreadystatechange(event)
    }
    native.onload = function (event) {
      if (typeof self.onload === 'function') self.onload(event)
    }
    native.onerror = function (event) {
      if (typeof self.onerror === 'function') self.onerror(event)
    }
    native.onprogress = function (event) {
      if (typeof self.onprogress === 'function') self.onprogress(event)
    }
  }

  PatchedXHR.prototype.send = function (body) {
    var self = this
    if (this._native) {
      this._native.responseType = this.responseType
      return this._native.send(body)
    }
    readPackagedMediapipeFile(this._fileName).then(function (buf) {
      var total = buf.byteLength || 0
      self.status = 200
      self.statusText = 'OK'
      self.response = buf
      self.readyState = 4
      if (typeof self.onprogress === 'function') {
        self.onprogress({ target: self, lengthComputable: true, loaded: total, total: total })
      }
      if (typeof self.onreadystatechange === 'function') self.onreadystatechange({ target: self })
      if (typeof self.onload === 'function') self.onload({ target: self })
    }).catch(function (err) {
      self.status = 0
      self.statusText = err && err.message ? err.message : 'plus.io read failed'
      self.readyState = 4
      if (typeof self.onerror === 'function') self.onerror({ target: self, error: err })
    })
  }

  PatchedXHR.prototype.setRequestHeader = function (name, value) {
    if (this._native) return this._native.setRequestHeader(name, value)
  }
  PatchedXHR.prototype.getResponseHeader = function (name) {
    if (this._native) return this._native.getResponseHeader(name)
    return null
  }
  PatchedXHR.prototype.getAllResponseHeaders = function () {
    if (this._native) return this._native.getAllResponseHeaders()
    return ''
  }
  PatchedXHR.prototype.abort = function () {
    if (this._native) return this._native.abort()
  }

  ;['readyState', 'status', 'statusText', 'response', 'responseText', 'responseURL'].forEach(function (prop) {
    var privateName = '_' + prop
    Object.defineProperty(PatchedXHR.prototype, prop, {
      configurable: true,
      get: function () {
        return this._native ? this._native[prop] : this[privateName]
      },
      set: function (value) {
        this[privateName] = value
      },
    })
  })

  window.XMLHttpRequest = PatchedXHR
  window.XMLHttpRequest._patchedForFaceCam = true
  console.log('[FaceCam] XMLHttpRequest static 模型补丁已安装')
})()


const UPPER_LIP = 13
const LOWER_LIP = 14
const MOUTH_LEFT = 61
const MOUTH_RIGHT = 291

function dist(a, b) {
  if (!a || !b) return 0
  const dx = a.x - b.x
  const dy = a.y - b.y
  return Math.sqrt(dx * dx + dy * dy)
}

function mouthOpenRatio(lm) {
  if (!lm || lm.length < 292) return 0
  const u = lm[UPPER_LIP]
  const d = lm[LOWER_LIP]
  const l = lm[MOUTH_LEFT]
  const r = lm[MOUTH_RIGHT]
  const vertical = dist(u, d)
  const horizontal = dist(l, r) || 1e-6
  return Math.min(1, vertical / horizontal)
}

function loadScript(src, timeoutMs) {
  return new Promise(function (resolve, reject) {
    try {
      if (typeof document === 'undefined') {
        reject(new Error('无 document'))
        return
      }
      var existed = document.querySelector('script[data-face-cam="' + src + '"]')
      if (existed) {
        resolve()
        return
      }
      if (typeof window !== 'undefined' && window.FaceMesh) {
        resolve()
        return
      }
      var s = document.createElement('script')
      s.src = src
      s.async = true
      s.setAttribute('data-face-cam', src)
      var fired = false
      var timer = setTimeout(function () {
        if (fired) return
        fired = true
        s.onload = s.onerror = null
        reject(new Error('脚本加载超时: ' + src))
      }, timeoutMs || 15000)
      s.onload = function () {
        if (fired) return
        fired = true
        clearTimeout(timer)
        resolve()
      }
      s.onerror = function () {
        if (fired) return
        fired = true
        clearTimeout(timer)
        reject(new Error('脚本加载失败: ' + src))
      }
      ;(document.head || document.documentElement).appendChild(s)
    } catch (e) {
      reject(e)
    }
  })
}

export default {
  data() {
    return {
      video: null,
      stream: null,
      canvas: null,
      faceMesh: null,
      raf: 0,
      running: false,
      lastOpen: 0,
      openHistory: [],
      lastPopAt: 0,
      lastError: '',
      // 手机端光线/角度差，阈值略放宽，便于抿唇蓄力
      pursedMax: 0.16,
      popFromMax: 0.18,
      popToMin: 0.2,
      popCooldownMs: 600,

      owner: null,
      sending: false,
      packageRoot: '',
      lastEmitAt: 0,
      lastResultAt: 0,
      sendFailCount: 0,
      frameCount: 0,
      // 新增：sending 超时 id，便于清理
      _sendTimerId: 0,
      _emitFailCount: 0,
      // 首次结果标记，用于诊断
      _hadResults: false,
    }
  },
  methods: {
    /** uni-app: (newVal, oldVal, ownerInstance, instance) */
    onCmd(newVal, oldVal, ownerInstance, instance) {
      // 存储 ownerInstance（带 fallback）
      if (ownerInstance) this.owner = ownerInstance
      // 有些 uni-app 版本把 ownerInstance 放在第4个参数(instance)里
      if (instance && !this.owner) this.owner = instance
      // 确保 $ownerInstance 也能访问（uni-app renderjs 标准属性）
      if (!this.$ownerInstance && ownerInstance) {
        try { this.$ownerInstance = ownerInstance } catch (e) { /* ignore */ }
      }

      if (!newVal || !newVal.action) return
      if (newVal.action === 'beginModelTransfer') {
        // 接收逻辑层传来的模型文件（base64）→ 解码为 blob URL
        this._receiveModelFiles(newVal)
      } else if (newVal.action === 'start') {
        this.startCamera()
      } else if (newVal.action === 'stop') {
        this.stopCamera()
      }
    },

    /**
     * 将逻辑层传来的 base64 模型文件解码为 blob URL，存入 this._modelBlobs
     * @param {Object} cmd - { files: { fileName: base64String } }
     */
    _receiveModelFiles(cmd) {
      if (!cmd || !cmd.files) return
      var files = cmd.files
      // 初始化（首次接收时清理旧数据）
      if (!this._modelBlobs) this._modelBlobs = {}
      var self = this
      Object.keys(files).forEach(function (fileName) {
        var base64 = files[fileName]
        if (!base64) return
        try {
          // 解码 base64 → ArrayBuffer → Blob → blob URL
          var binaryString = atob(base64)
          var bytes = new Uint8Array(binaryString.length)
          for (var i = 0; i < binaryString.length; i++) {
            bytes[i] = binaryString.charCodeAt(i) & 0xff
          }
          var blob = new Blob([bytes.buffer], { type: 'application/octet-stream' })
          // 释放旧 blob URL（如果有）
          if (self._modelBlobs[fileName] && self._modelBlobs[fileName].indexOf('blob:') === 0) {
            try { URL.revokeObjectURL(self._modelBlobs[fileName]) } catch (e) { /* ignore */ }
          }
          self._modelBlobs[fileName] = URL.createObjectURL(blob)
          console.log('[FaceCam] 本地模型 blob: ' + fileName + ' (' + (bytes.length / 1024).toFixed(0) + 'KB)')
        } catch (e) {
          console.warn('[FaceCam] 解码模型文件失败: ' + fileName + ' - ' + (e && e.message))
        }
      })
      if (this._hasEnoughLocalModels()) {
        this.emitToLogic('status', { message: '本地人脸模型已加载完成' })
      }
    },

    _hasEnoughLocalModels() {
      var blobs = this._modelBlobs || {}
      return !!(
        blobs['face_mesh.binarypb'] &&
        blobs['face_mesh_solution_packed_assets.data'] &&
        (blobs['face_mesh_solution_simd_wasm_bin.wasm'] ||
          blobs['face_mesh_solution_wasm_bin.wasm'])
      )
    },

    waitForModelFiles(timeoutMs) {
      var self = this
      return new Promise(function (resolve) {
        if (self._hasEnoughLocalModels()) {
          resolve(true)
          return
        }
        var startedAt = Date.now()
        var lastCount = -1
        var timer = setInterval(function () {
          var blobs = self._modelBlobs || {}
          var count = 0
          for (var i = 0; i < LOCAL_MODEL_FILES.length; i++) {
            if (blobs[LOCAL_MODEL_FILES[i]]) count += 1
          }
          if (count !== lastCount) {
            lastCount = count
            self.emitToLogic('status', {
              message: '正在加载本地人脸模型 ' + count + '/' + LOCAL_MODEL_FILES.length,
            })
          }
          if (self._hasEnoughLocalModels()) {
            clearInterval(timer)
            resolve(true)
            return
          }
          if (Date.now() - startedAt >= timeoutMs) {
            clearInterval(timer)
            console.warn('[FaceCam] 等待本地模型超时，已收到 ' + count + '/' + LOCAL_MODEL_FILES.length + ' 个，继续尝试初始化')
            resolve(false)
          }
        }, 120)
      })
    },

    emitToLogic(type, extra) {
      var raw = Object.assign({ type: type }, extra || {})
      var payload
      try {
        payload = JSON.parse(JSON.stringify(raw))
      } catch (e0) {
        payload = { type: type, message: (extra && extra.message) || '' }
      }
      var ok = false
      // 优先使用 uni-app 内置的 $ownerInstance（renderjs 标准通信方式）
      try {
        if (this.$ownerInstance && typeof this.$ownerInstance.callMethod === 'function') {
          this.$ownerInstance.callMethod('onRenderEvent', payload)
          ok = true
        }
      } catch (e1) {
        /* fallthrough */
      }
      if (!ok) {
        try {
          if (this.owner && typeof this.owner.callMethod === 'function') {
            this.owner.callMethod('onRenderEvent', payload)
            ok = true
          }
        } catch (e2) {
          /* ignore */
        }
      }
      if (!ok) {
        try {
          var inst = this.$ownerInstance || this.owner
          if (inst && inst.$vm && typeof inst.$vm.onRenderEvent === 'function') {
            inst.$vm.onRenderEvent(payload)
            ok = true
          }
        } catch (e3) {
          /* ignore */
        }
      }
      // 调试：记录通信失败次数（每 30 次报一次避免刷屏）
      if (!ok) {
        this._emitFailCount = (this._emitFailCount || 0) + 1
        if (this._emitFailCount % 30 === 1) {
          console.warn('[FaceCam renderjs] emitToLogic 失败 ' + this._emitFailCount + ' 次, type=' + type)
        }
      } else {
        this._emitFailCount = 0
      }
    },

    emitSnapshot(s) {
      var now = Date.now()
      var force = s.isPop || s.error || !s.ready
      if (!force && now - this.lastEmitAt < 80) {
        if (!s.isPursed && !s.isPop) {
          if (now - this.lastEmitAt < 120) return
        }
      }
      this.lastEmitAt = now
      this.emitToLogic('snapshot', { data: s })
    },

    getHost() {
      try {
        if (typeof document === 'undefined') return null
        return document.getElementById('face-cam-host')
      } catch (e) {
        return null
      }
    },

    mountVideo(host) {
      while (host.firstChild) host.removeChild(host.firstChild)
      var video = document.createElement('video')
      video.setAttribute('autoplay', 'true')
      video.setAttribute('muted', 'true')
      video.setAttribute('playsinline', 'true')
      video.setAttribute('webkit-playsinline', 'true')
      video.controls = false
      video.muted = true
      video.playsInline = true
      try {
        video.webkitPlaysInline = true
      } catch (e) {
        /* ignore */
      }
      // 仅预览镜像；送入 MediaPipe 的帧不要再镜像（selfieMode 已处理）
      video.style.cssText =
        'width:100%;height:100%;object-fit:cover;transform:scaleX(-1);background:#12082e;display:block;position:absolute;inset:0;z-index:0;'
      host.appendChild(video)

      // 离屏 canvas：部分 App WebView 直接对 video send 读不到像素
      var canvas = document.createElement('canvas')
      canvas.width = 320
      canvas.height = 240
      canvas.style.cssText =
        'display:none;position:absolute;width:1px;height:1px;opacity:0;pointer-events:none;left:-9999px;top:-9999px;'
      host.appendChild(canvas)
      this.canvas = canvas

      return video
    },


    async ensureGetUserMedia() {
      if (typeof navigator === 'undefined') throw new Error('当前环境无 navigator')
      if (!navigator.mediaDevices) navigator.mediaDevices = {}
      if (!navigator.mediaDevices.getUserMedia) {
        var legacy =
          navigator.getUserMedia ||
          navigator.webkitGetUserMedia ||
          navigator.mozGetUserMedia ||
          navigator.msGetUserMedia
        if (legacy) {
          navigator.mediaDevices.getUserMedia = function (constraints) {
            return new Promise(function (resolve, reject) {
              legacy.call(navigator, constraints, resolve, reject)
            })
          }
        }
      }
      if (!navigator.mediaDevices.getUserMedia) {
        throw new Error('当前环境不支持摄像头')
      }
      return navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices)
    },

    async openStream() {
      var getUserMedia = await this.ensureGetUserMedia()
      // 手机端优先低分辨率保证流畅
      var tryList = [
        {
          audio: false,
          video: {
            facingMode: { ideal: 'user' },
            width: { ideal: 480 },
            height: { ideal: 640 },
          },
        },
        { audio: false, video: { facingMode: 'user' } },
        { audio: false, video: true },
      ]
      var lastErr = null
      for (var i = 0; i < tryList.length; i++) {
        try {
          return await getUserMedia(tryList[i])
        } catch (e) {
          lastErr = e
        }
      }
      var name = lastErr && lastErr.name ? String(lastErr.name) : ''
      if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
        throw new Error('请允许使用摄像头后重试')
      }
      if (name === 'NotFoundError') throw new Error('未找到摄像头设备')
      if (name === 'NotReadableError') throw new Error('摄像头被占用，请关闭其他应用后重试')
      throw new Error('无法打开摄像头，请检查应用权限')
    },

    /**
     * 补丁：强制禁用 SIMD WASM，改为使用更兼容的非 SIMD 版本。
     *
     * 背景：face_mesh.js 内部会通过 WebAssembly.instantiate(Sb) 检测 SIMD 支持
     * （Sb 是一个 31 字节的测试模块，包含 0xFD 0x0F SIMD 前缀指令）。
     * 若检测通过，则加载 face_mesh_solution_simd_wasm_bin.js + .wasm，
     * 否则加载 face_mesh_solution_wasm_bin.js + .wasm。
     *
     * 问题：uni-app WebView 和部分 devServer 环境下，SIMD WASM instantiate
     * 会卡在 "still waiting on run dependencies: wasm-instantiate"，
     * 无法完成初始化。非 SIMD 版本在所有环境下都能正常工作，
     * 且人脸检测性能差异可忽略。
     */
    _patchSimdDisable() {
      if (typeof WebAssembly === 'undefined') return
      if (WebAssembly._faceCamPatched) return
      try {
        var _origInstantiate = WebAssembly.instantiate
        WebAssembly.instantiate = function (bufferSource, importObject) {
          // SIMD 检测模块 Sb 为 31 字节的 Uint8Array，以 WASM magic number 0x00 0x61 0x73 0x6D 开头
          if (bufferSource && typeof bufferSource.length === 'number' && bufferSource.length < 100) {
            var arr = bufferSource.length <= 31
              ? new Uint8Array(bufferSource instanceof ArrayBuffer ? bufferSource : bufferSource.buffer || bufferSource)
              : null
            // 确认是 WASM 二进制（magic: \0asm = [0,97,115,109]）
            if (arr && arr[0] === 0 && arr[1] === 97 && arr[2] === 115 && arr[3] === 109) {
              console.log('[FaceCam] 已拦截 SIMD WASM 检测模块，强制使用非 SIMD 版本')
              return Promise.reject(new Error('SIMD disabled for uni-app compatibility'))
            }
          }
          return _origInstantiate.call(WebAssembly, bufferSource, importObject)
        }
        WebAssembly.instantiate._faceCamOriginal = _origInstantiate
        WebAssembly._faceCamPatched = true
        console.log('[FaceCam] SIMD 禁用补丁已安装')
      } catch (e) {
        console.warn('[FaceCam] SIMD 补丁安装失败: ' + (e && e.message))
      }
    },

    async loadFaceMeshLib() {
      if (typeof window !== 'undefined' && window.FaceMesh) {
        return window.FaceMesh
      }
      // ★ 必须在加载 face_mesh.js 之前安装，否则 SIMD 检测已经执行完毕
      this._patchSimdDisable()
      // 每次加载时重建路径（App 上 plus 可能比脚本更晚就绪）
      var roots = buildPackageRoots()
      // 把本地路径排到最前面（本地更快且离线可用）
      var localRoots = []
      var cdnRoots = []
      for (var ri = 0; ri < roots.length; ri++) {
        var rt = String(roots[ri] || '').replace(/\/$/, '')
        if (!rt) continue
        if (rt.indexOf('http://') === 0 || rt.indexOf('https://') === 0) {
          cdnRoots.push(rt)
        } else {
          localRoots.push(rt)
        }
      }
      // 本地优先，CDN 兜底
      var ordered = localRoots.concat(cdnRoots)

      var lastErr = null
      this.emitToLogic('status', { message: '正在加载本地人脸模型…' })

      // 诊断：打印尝试路径
      console.log('[FaceCam] 模型路径候选: ' + JSON.stringify(ordered.slice(0, 5)))

      for (var i = 0; i < ordered.length; i++) {
        var root = String(ordered[i] || '').replace(/\/$/, '')
        if (!root) continue
        var src = root + '/face_mesh.js'
        var isLocal = src.indexOf('http://') !== 0 && src.indexOf('https://') !== 0
        try {
          console.log('[FaceCam] 尝试加载: ' + src + (isLocal ? ' (本地)' : ' (CDN)'))
          await loadScript(src, isLocal ? 8000 : 20000)
          if (window.FaceMesh) {
            this.packageRoot = root
            this.emitToLogic('status', {
              message: isLocal ? '本地模型已就绪' : '在线模型已就绪',
            })
            console.log('[FaceCam] 模型加载成功: ' + src)
            return window.FaceMesh
          }
          // 等待一小段时间让脚本完成初始化
          await new Promise(function (r) { setTimeout(r, 200) })
          if (window.FaceMesh) {
            this.packageRoot = root
            console.log('[FaceCam] 模型延迟就绪: ' + src)
            return window.FaceMesh
          }
          lastErr = new Error('已加载脚本但无 FaceMesh 全局对象: ' + src)
          console.warn('[FaceCam] ' + lastErr.message)
        } catch (e) {
          lastErr = e
          console.warn('[FaceCam] 加载失败: ' + src + ' - ' + (e && e.message))
        }
      }

      console.error('[FaceCam] 所有模型路径加载失败')
      throw new Error(
        (lastErr && lastErr.message) ||
          '人脸模型加载失败：请重新打包安装，确保包含 static/mediapipe',
      )
    },

    async ensureModel() {
      if (this.faceMesh) return
      this.emitToLogic('status', { message: '正在加载人脸识别模型…' })
      this.emitSnapshot({
        ready: false,
        faceDetected: false,
        mouthOpen: 0,
        isPursed: false,
        isPop: false,
        statusText: '正在加载人脸识别模型…',
      })

      var FaceMeshCtor = await this.loadFaceMeshLib()
      var rootsNow = buildPackageRoots()
      var root = (this.packageRoot || rootsNow[0] || PACKAGE_ROOTS[0] || '').replace(
        /\/$/,
        '',
      )
      var self = this

      console.log('[FaceCam] 初始化 FaceMesh，资源根路径: ' + root)

      if (this._modelBlobs && Object.keys(this._modelBlobs).length > 0) {
        console.log('[FaceCam] 使用本地模型文件（blob URL），共 ' + Object.keys(this._modelBlobs).length + ' 个')
      } else {
        console.log('[FaceCam] 未收到 blob 模型，直接使用包内 static 相对路径')
      }

      // CDN base（兜底用）
      var CDN_FALLBACK = 'https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4.1633559619'
      var isLocalRoot = root.indexOf('http://') !== 0 && root.indexOf('https://') !== 0

      var fm = new FaceMeshCtor({
        locateFile: function (file) {
          var cleanName = String(file).replace(/^\//, '')

          // 优先：逻辑层传来的本地 blob URL（二进制文件）
          if (self._modelBlobs && self._modelBlobs[cleanName]) {
            console.log('[FaceCam] locateFile blob: ' + cleanName)
            return self._modelBlobs[cleanName]
          }

          // JS 文件（.js）：MediaPipe 通过 <script> 标签加载，本地路径可用
          // uni-app 会拦截 <script> 标签的本地文件请求，无需 CDN
          if (cleanName.slice(-3) === '.js') {
            var jsUrl = root + '/' + cleanName
            console.log('[FaceCam] locateFile JS(本地): ' + cleanName + ' -> ' + jsUrl)
            return jsUrl
          }

          // 二进制文件（.wasm / .data / .binarypb）：App 包内相对路径优先。
          if (isLocalRoot) {
            var localUrl = root + '/' + cleanName
            console.log('[FaceCam] locateFile local: ' + cleanName + ' -> ' + localUrl)
            return localUrl
          }

          // root 本身就是 CDN/HTTP URL
          var directUrl = root + '/' + cleanName
          console.log('[FaceCam] locateFile HTTP: ' + cleanName + ' -> ' + directUrl)
          return directUrl
        },
      })
      // 置信度放宽；selfieMode=true 适配前置摄像头（不要再对 canvas 水平翻转）
      fm.setOptions({
        maxNumFaces: 1,
        refineLandmarks: false,
        minDetectionConfidence: 0.25,
        minTrackingConfidence: 0.25,
        selfieMode: true,
      })


      fm.onResults(function (results) {
        if (!self._hadResults) {
          self._hadResults = true
          console.log('[FaceCam] 首次人脸识别结果到达')
        }
        self.onMeshResults(results)
      })

      // await 模型初始化完成（新版 MediaPipe 需要显式初始化）
      if (typeof fm.initialize === 'function') {
        try {
          console.log('[FaceCam] 调用 faceMesh.initialize()...')
          await fm.initialize()
          console.log('[FaceCam] faceMesh.initialize() 完成')
        } catch (e) {
          // 如果初始化失败是因为 fetch 问题，此时 blob URL 已在 locateFile 中返回；
          // 若仍然失败，可能是 initialize 内部不走 locateFile（旧版行为），
          // 此时 fetch 补丁（plus.io）会兜底
          console.warn('[FaceCam] faceMesh.initialize() 失败: ' + (e && e.message) + '，尝试继续')
        }
      }

      // 给 WASM 加载一点时间
      await new Promise(function (r) { setTimeout(r, 300) })

      this.faceMesh = fm
      this.lastResultAt = 0
      this.sendFailCount = 0
      this._hadResults = false
      console.log('[FaceCam] 模型初始化完成，等待视频帧送入')
    },

    pickLandmarks(results) {
      if (!results) return null
      var multi = results.multiFaceLandmarks
      if (multi && multi.length > 0) return multi[0]
      if (results.faceLandmarks && results.faceLandmarks.length > 0) {
        var fl = results.faceLandmarks
        if (fl[0] && typeof fl[0].x === 'number') return fl
        if (fl[0] && fl[0].length) return fl[0]
      }
      return null
    },

    onMeshResults(results) {
      if (!this.running) return
      this.lastResultAt = Date.now()
      this.sendFailCount = 0

      var faceDetected = false
      var mouthOpen = 0
      var isPursed = false
      var isPop = false
      var statusText = '请把脸对准画面'

      try {
        var lm = this.pickLandmarks(results)
        if (lm && lm.length >= 15) {
          faceDetected = true
          mouthOpen = mouthOpenRatio(lm)
          this.openHistory.push(mouthOpen)
          if (this.openHistory.length > 14) this.openHistory.shift()

          isPursed = mouthOpen <= this.pursedMax

          var recentMin = Math.min.apply(null, this.openHistory)
          var now = Date.now()
          if (
            this.lastOpen <= this.popFromMax &&
            mouthOpen >= this.popToMin &&
            recentMin <= this.pursedMax + 0.04 &&
            now - this.lastPopAt >= this.popCooldownMs
          ) {
            isPop = true
            this.lastPopAt = now
          }

          if (isPursed) statusText = '已识别抿唇 ✓ 请保持'
          else if (mouthOpen > 0.28) statusText = '嘴巴张开中…（弹唇要「啵」一下）'
          else statusText = '请再抿紧一点小嘴巴'

          this.lastOpen = mouthOpen
        } else {
          this.openHistory = []
          this.lastOpen = 0
          statusText = '未检测到人脸，请正对镜头、光线充足、整张脸入画'
        }
      } catch (e) {
        statusText = '识别中…'
      }

      this.emitSnapshot({
        ready: true,
        faceDetected: faceDetected,
        mouthOpen: mouthOpen,
        isPursed: isPursed,
        isPop: isPop,
        statusText: statusText,
        error: this.lastError || undefined,
      })
    },

    async startCamera() {
      this.stopCamera(false)
      this.running = true
      this.lastError = ''
      this.openHistory = []
      this.lastOpen = 0
      this.lastPopAt = 0
      this.sending = false
      this.frameCount = 0
      this.lastResultAt = 0
      this.sendFailCount = 0
      this._hadResults = false
      this._emitFailCount = 0
      if (this._sendTimerId) {
        clearTimeout(this._sendTimerId)
        this._sendTimerId = 0
      }

      try {
        var host = this.getHost()
        if (!host) throw new Error('未找到摄像头区域，请返回重试')

        host.style.position = host.style.position || 'relative'
        host.style.width = '100%'
        host.style.height = '100%'
        host.style.overflow = 'hidden'

        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '正在打开前置摄像头…',
        })

        this.video = this.mountVideo(host)
        this.stream = await this.openStream()
        if (!this.running) {
          this.stopCamera()
          return
        }

        this.video.srcObject = this.stream
        this.video.muted = true
        this.video.playsInline = true
        try {
          await this.video.play()
        } catch (e) {
          await new Promise(function (r) { setTimeout(r, 150) })
          try {
            await this.video.play()
          } catch (e2) {
            /* continue */
          }
        }

        await new Promise(
          function (resolve) {
            var v = this.video
            if (!v) {
              resolve()
              return
            }
            if (v.readyState >= 2 && v.videoWidth > 0) {
              resolve()
              return
            }
            var done = function () {
              v.removeEventListener('loadeddata', done)
              v.removeEventListener('loadedmetadata', done)
              resolve()
            }
            v.addEventListener('loadeddata', done)
            v.addEventListener('loadedmetadata', done)
            setTimeout(resolve, 2000)
          }.bind(this),
        )

        if (!this.running) {
          this.stopCamera()
          return
        }

        // 摄像头已出图：立刻通知逻辑层结束「启动中」
        this.emitToLogic('started')
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '摄像头已开启，正在加载识别模型…',
        })

        var isAppRuntime = false
        try {
          isAppRuntime = typeof plus !== 'undefined'
        } catch (e) {
          isAppRuntime = false
        }
        if (!isAppRuntime) {
          var waitMs = 1500
          console.log('[FaceCam] 等待本地模型文件到达，最长 ' + waitMs + 'ms')
          await this.waitForModelFiles(waitMs)
        } else {
          console.log('[FaceCam] App 真机跳过 plus.io 模型传输，使用包内 static 相对路径')
        }

        await this.ensureModel()
        if (!this.running) {
          this.stopCamera()
          return
        }

        this.emitSnapshot({
          ready: true,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '请把脸对准画面中央，抿住嘴唇',
        })

        this.loop()
      } catch (e) {
        var msg = e && e.message ? e.message : '摄像头/人脸模型启动失败'
        console.error('[FaceCam] startCamera 失败: ' + msg, e)
        this.lastError = msg
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: msg,
          error: msg,
        })
        this.emitToLogic('error', { message: msg })
        this.stopCamera(false)
      }
    },

    stopCamera(emitStatus) {
      this.running = false
      this.sending = false
      // 清理 sending 超时定时器
      if (this._sendTimerId) {
        clearTimeout(this._sendTimerId)
        this._sendTimerId = 0
      }
      if (this.raf) {
        cancelAnimationFrame(this.raf)
        this.raf = 0
      }
      if (this.stream) {
        try {
          this.stream.getTracks().forEach(function (t) {
            try {
              t.stop()
            } catch (e) {
              /* ignore */
            }
          })
        } catch (e) {
          /* ignore */
        }
        this.stream = null
      }
      if (this.video) {
        try {
          this.video.srcObject = null
          if (this.video.parentNode) this.video.parentNode.removeChild(this.video)
        } catch (e) {
          /* ignore */
        }
        this.video = null
      }
      // canvas 跟 host 一起被清，这里只松引用
      this.canvas = null
      // 保留 faceMesh，避免每次重启都重新拉 WASM（失败率高）
      this.openHistory = []
      this.lastOpen = 0
      this.lastResultAt = 0
      this.frameCount = 0
      if (emitStatus !== false) {
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '',
        })
      }
    },

    destroyModel() {
      if (this.faceMesh) {
        try {
          if (typeof this.faceMesh.close === 'function') this.faceMesh.close()
        } catch (e) {
          /* ignore */
        }
        this.faceMesh = null
      }
      // 释放预加载的 blob URL，避免内存泄露
      if (this._modelBlobs) {
        try {
          var urls = this._modelBlobs
          for (var key in urls) {
            if (urls.hasOwnProperty(key) && urls[key].indexOf('blob:') === 0) {
              URL.revokeObjectURL(urls[key])
            }
          }
        } catch (e) {
          /* ignore */
        }
        this._modelBlobs = null
      }
    },

    loop() {
      if (!this.running) return
      this.raf = requestAnimationFrame(this.loop.bind(this))
      this.tick()
    },

    /**
     * 取帧策略：
     * - 优先 canvas 拷贝（不翻转；selfieMode 负责前置）
     * - 若长时间无结果，交替直接送 video
     */
    grabFrame() {
      var video = this.video
      var canvas = this.canvas
      if (!video || !video.videoWidth || !video.videoHeight) return null

      // 无结果时：偶数帧直接 video，奇数帧 canvas，提高兼容性
      var preferVideo =
        this.lastResultAt === 0 && this.frameCount > 8 && this.frameCount % 2 === 0
      if (preferVideo) return video

      if (canvas) {
        try {
          var maxW = 400
          var vw = video.videoWidth
          var vh = video.videoHeight
          var w = Math.min(maxW, vw)
          var h = Math.max(1, Math.round((vh / vw) * w))
          if (canvas.width !== w) canvas.width = w
          if (canvas.height !== h) canvas.height = h

          // 尝试多种方式获取 canvas context（兼容不同 WebView）
          var ctx = null
          try {
            ctx = canvas.getContext('2d', { willReadFrequently: true })
          } catch (e1) {
            /* 不支持此选项 */
          }
          if (!ctx) {
            try {
              ctx = canvas.getContext('2d')
            } catch (e2) {
              /* ignore */
            }
          }
          if (ctx) {
            // 不翻转：与 selfieMode:true 配合；CSS 镜像只影响预览
            ctx.drawImage(video, 0, 0, w, h)
            return canvas
          }
        } catch (e) {
          /* fallthrough to video */
        }
      }
      return video
    },

    tick() {
      var video = this.video
      var fm = this.faceMesh
      if (!video || !fm || video.readyState < 2) return
      if (this.sending) return
      if (!video.videoWidth || !video.videoHeight) return

      var image = this.grabFrame()
      if (!image) return

      this.sending = true
      this.frameCount += 1
      var self = this
      var released = false
      var release = function () {
        if (released) return
        released = true
        self.sending = false
        // 清理超时定时器
        if (self._sendTimerId) {
          clearTimeout(self._sendTimerId)
          self._sendTimerId = 0
        }
      }

      try {
        var p = fm.send({ image: image })
        if (p && typeof p.then === 'function') {
          // send 返回 Promise：等待完成，最多等 2500ms
          self._sendTimerId = setTimeout(function () {
            if (!released) {
              self.sendFailCount += 1
              console.warn('[FaceCam] fm.send() 超时 (2500ms)')
            }
            release()
          }, 2500)
          p.then(function () {
            release()
          }).catch(function (err) {
            self.sendFailCount += 1
            console.warn('[FaceCam] fm.send() 失败: ' + (err && err.message))
            release()
          })
        } else {
          // send 同步返回：短延时释放（给 onResults 回调时间）
          self._sendTimerId = setTimeout(release, 60)
        }
      } catch (e) {
        this.sendFailCount += 1
        release()
      }

      // 长时间无 onResults：诊断与提示
      if (this.frameCount > 15 && this.lastResultAt === 0) {
        var msg =
          '人脸识别尚未出结果。请正对镜头、光线充足；若一直如此请点右上角暂停再继续'
        if (this.frameCount === 16 || this.frameCount % 50 === 0) {
          console.warn('[FaceCam] ' + this.frameCount + ' 帧无识别结果, sendFail=' + this.sendFailCount)
          this.emitToLogic('status', { message: msg })
          this.emitSnapshot({
            ready: true,
            faceDetected: false,
            mouthOpen: 0,
            isPursed: false,
            isPop: false,
            statusText: msg,
          })
        }
      } else if (
        this.lastResultAt > 0 &&
        Date.now() - this.lastResultAt > 4000 &&
        this.frameCount % 30 === 0
      ) {
        this.emitSnapshot({
          ready: true,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '识别暂停，请靠近镜头并保证光线充足',
        })
      }
    },

  },
  beforeDestroy() {
    this.stopCamera(false)
    this.destroyModel()
  },
  unmounted() {
    this.stopCamera(false)
    this.destroyModel()
  },
}
</script>

<style scoped lang="scss">
.face-cam-root,
.face-cam-host {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #12082e;
}
</style>
