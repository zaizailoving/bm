<template>
  <view class="face-cam-root">
    <view
      id="face-cam-host"
      class="face-cam-host"
      :prop="cmd"
      :change:prop="faceCam.onCmd"
    />
  </view>
</template>

<script lang="ts">
import type { FaceLipSnapshot } from '@/utils/faceLip'

type CamCmd = {
  action: 'idle' | 'start' | 'stop'
  token: number
  ts: number
}

type RenderPayload = {
  type: 'snapshot' | 'error' | 'started' | 'status'
  data?: FaceLipSnapshot
  message?: string
}

export default {
  name: 'FaceCam',
  props: {
    runToken: {
      type: Number,
      default: 0,
    },
  },
  emits: ['snapshot', 'error', 'started', 'status'],
  data() {
    return {
      cmd: { action: 'idle', token: 0, ts: 0 } as CamCmd,
    }
  },
  watch: {
    runToken: {
      immediate: true,
      handler(token: number, prev?: number) {
        if (token > 0 && token !== prev) {
          this.cmd = { action: 'start', token, ts: Date.now() }
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
    },
  },
}
</script>

<script module="faceCam" lang="renderjs">
var MODEL_ROOT = 'static/mediapipe/face_mesh'
var APP_MODEL_ROOT = '_www/static/mediapipe/face_mesh'
var CDN_ROOT = 'https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4.1633559619'

var RESOURCE_TYPES = {
  'face_mesh.js': 'text/javascript',
  'face_mesh.binarypb': 'application/octet-stream',
  'face_mesh_solution_packed_assets_loader.js': 'text/javascript',
  'face_mesh_solution_packed_assets.data': 'application/octet-stream',
  'face_mesh_solution_simd_wasm_bin.js': 'text/javascript',
  'face_mesh_solution_simd_wasm_bin.wasm': 'application/wasm',
  'face_mesh_solution_wasm_bin.js': 'text/javascript',
  'face_mesh_solution_wasm_bin.wasm': 'application/wasm',
}

var BINARY_RESOURCE_NAMES = [
  'face_mesh.binarypb',
  'face_mesh_solution_packed_assets.data',
  'face_mesh_solution_wasm_bin.wasm',
]

var SCRIPT_RESOURCE_NAMES = [
  'face_mesh_solution_packed_assets_loader.js',
  'face_mesh_solution_wasm_bin.js',
]

var UPPER_LIP = 13
var LOWER_LIP = 14
var MOUTH_LEFT = 61
var MOUTH_RIGHT = 291
var FACE_LEFT = 234
var FACE_RIGHT = 454
var rawFaceMesh = null

function isAppRuntime() {
  try {
    return typeof plus !== 'undefined' && !!plus.io
  } catch (e) {
    return false
  }
}

function dist(a, b) {
  if (!a || !b) return 0
  var dx = a.x - b.x
  var dy = a.y - b.y
  return Math.sqrt(dx * dx + dy * dy)
}

function mouthMetrics(lm) {
  if (!lm || lm.length < 292) {
    return { open: 0, width: 0 }
  }
  var mouthWidth = dist(lm[MOUTH_LEFT], lm[MOUTH_RIGHT]) || 1e-6
  var faceWidth = dist(lm[FACE_LEFT], lm[FACE_RIGHT]) || mouthWidth
  return {
    open: Math.min(1, dist(lm[UPPER_LIP], lm[LOWER_LIP]) / mouthWidth),
    width: Math.min(1, mouthWidth / faceWidth),
  }
}

function clamp01(value) {
  return Math.max(0, Math.min(1, value))
}

function tongueUpMetrics(lm, canvas) {
  if (!lm || lm.length < 292 || !canvas || !canvas.width || !canvas.height) {
    return { isTongueUp: false, score: 0 }
  }

  try {
    var ctx = canvas.getContext('2d')
    if (!ctx) return { isTongueUp: false, score: 0 }

    var left = lm[MOUTH_LEFT]
    var right = lm[MOUTH_RIGHT]
    var upper = lm[UPPER_LIP]
    var lower = lm[LOWER_LIP]
    if (!left || !right || !upper || !lower) return { isTongueUp: false, score: 0 }

    var cx = ((left.x + right.x) / 2) * canvas.width
    var cy = ((upper.y + lower.y) / 2) * canvas.height
    var mouthW = Math.max(18, Math.abs(right.x - left.x) * canvas.width)
    var mouthH = Math.max(12, Math.abs(lower.y - upper.y) * canvas.height)
    var sampleW = Math.min(canvas.width, Math.max(20, mouthW * 0.78))
    var sampleH = Math.min(canvas.height, Math.max(10, mouthH * 1.2))
    var x = Math.round(Math.max(0, cx - sampleW / 2))
    var y = Math.round(Math.max(0, cy - sampleH * 0.75))
    var w = Math.round(Math.min(sampleW, canvas.width - x))
    var h = Math.round(Math.min(sampleH, canvas.height - y))
    if (w <= 2 || h <= 2) return { isTongueUp: false, score: 0 }

    var data = ctx.getImageData(x, y, w, h).data
    var tonguePixels = 0
    var darkPixels = 0
    var total = 0

    for (var py = 0; py < h; py += 2) {
      for (var px = 0; px < w; px += 2) {
        var idx = (py * w + px) * 4
        var r = data[idx]
        var g = data[idx + 1]
        var b = data[idx + 2]
        var max = Math.max(r, g, b)
        var min = Math.min(r, g, b)
        var sat = max <= 0 ? 0 : (max - min) / max
        var bright = (r + g + b) / 3
        total += 1

        if (bright < 75) {
          darkPixels += 1
        }
        if (
          r > 105 &&
          g > 42 &&
          b > 42 &&
          r > g * 1.12 &&
          r > b * 1.08 &&
          sat > 0.18 &&
          bright > 72 &&
          bright < 230
        ) {
          tonguePixels += 1
        }
      }
    }

    var tongueRatio = total ? tonguePixels / total : 0
    var darkRatio = total ? darkPixels / total : 0
    var score = clamp01(tongueRatio * 4.2 + darkRatio * 0.45)
    return {
      isTongueUp: score >= 0.32 && tongueRatio >= 0.055,
      score: score,
    }
  } catch (e) {
    return { isTongueUp: false, score: 0 }
  }
}

function loadScript(src, timeoutMs) {
  return new Promise(function (resolve, reject) {
    if (typeof document === 'undefined') {
      reject(new Error('当前环境无 document'))
      return
    }
    if (typeof window !== 'undefined' && window.FaceMesh && src.indexOf('face_mesh.js') >= 0) {
      resolve()
      return
    }
    var existed = document.querySelector('script[data-face-cam-src="' + src + '"]')
    if (existed) {
      resolve()
      return
    }

    var script = document.createElement('script')
    script.src = src
    script.async = true
    script.setAttribute('data-face-cam-src', src)

    var done = false
    var timer = setTimeout(function () {
      if (done) return
      done = true
      script.onload = script.onerror = null
      reject(new Error('脚本加载超时: ' + src))
    }, timeoutMs || 15000)

    script.onload = function () {
      if (done) return
      done = true
      clearTimeout(timer)
      resolve()
    }
    script.onerror = function () {
      if (done) return
      done = true
      clearTimeout(timer)
      reject(new Error('脚本加载失败: ' + src))
    }
    ;(document.head || document.documentElement).appendChild(script)
  })
}

function enterBrowserGlobalScriptMode() {
  if (typeof window === 'undefined') {
    return function () {}
  }

  var keys = ['module', 'exports', 'define']
  var saved = {}
  for (var i = 0; i < keys.length; i++) {
    var key = keys[i]
    try {
      saved[key] = {
        exists: Object.prototype.hasOwnProperty.call(window, key),
        value: window[key],
      }
      window[key] = undefined
    } catch (e) {
      saved[key] = { failed: true }
    }
  }

  return function () {
    for (var j = 0; j < keys.length; j++) {
      var restoreKey = keys[j]
      var item = saved[restoreKey]
      if (!item || item.failed) continue
      try {
        if (item.exists) window[restoreKey] = item.value
        else delete window[restoreKey]
      } catch (e2) {
        /* ignore */
      }
    }
  }
}

function readAppFileAsBlobUrl(fileName, timeoutMs) {
  return new Promise(function (resolve, reject) {
    if (!isAppRuntime()) {
      resolve(MODEL_ROOT + '/' + fileName)
      return
    }

    var done = false
    var timer = setTimeout(function () {
      if (done) return
      done = true
      reject(new Error('读取模型超时: ' + fileName))
    }, timeoutMs || 6000)

    function finish(err, value) {
      if (done) return
      done = true
      clearTimeout(timer)
      if (err) reject(err)
      else resolve(value)
    }

    function dataUrlToBlobUrl(dataUrl) {
      if (typeof dataUrl !== 'string' || dataUrl.indexOf('data:') !== 0) {
        throw new Error('模型读取结果不是 data URL: ' + fileName)
      }
      var comma = dataUrl.indexOf(',')
      if (comma < 0) throw new Error('模型 data URL 格式异常: ' + fileName)
      var meta = dataUrl.slice(0, comma)
      var mimeMatch = /^data:([^;,]+)/.exec(meta)
      var mime = (mimeMatch && mimeMatch[1]) || RESOURCE_TYPES[fileName] || 'application/octet-stream'
      var payload = dataUrl.slice(comma + 1)
      var binary = meta.indexOf(';base64') >= 0 ? atob(payload) : decodeURIComponent(payload)
      var bytes = new Uint8Array(binary.length)
      for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i) & 0xff
      return URL.createObjectURL(new Blob([bytes], { type: mime }))
    }

    var path = APP_MODEL_ROOT + '/' + fileName
    plus.io.resolveLocalFileSystemURL(
      path,
      function (entry) {
        entry.file(
          function (file) {
            var reader = new plus.io.FileReader()
            reader.onloadend = function (e) {
              var dataUrl = e && e.target ? e.target.result : null
              if (!dataUrl) {
                finish(new Error('模型文件为空: ' + fileName))
                return
              }
              try {
                finish(null, dataUrlToBlobUrl(dataUrl))
              } catch (convertErr) {
                finish(convertErr)
              }
            }
            reader.onerror = function () {
              finish(new Error('读取模型文件失败: ' + fileName))
            }
            reader.readAsDataURL(file)
          },
          function (err) {
            finish(new Error('获取模型文件失败: ' + fileName + ' code=' + (err && err.code)))
          },
        )
      },
      function (err) {
        finish(new Error('未找到模型文件: ' + fileName + ' code=' + (err && err.code)))
      },
    )
  })
}

export default {
  data() {
    return {
      owner: null,
      video: null,
      canvas: null,
      stream: null,
      assetUrls: null,
      assetBlobUrls: [],
      raf: 0,
      running: false,
      sending: false,
      sendTimer: 0,
      frameCount: 0,
      lastResultAt: 0,
      lastEmitAt: 0,
      sendFailCount: 0,
      lastSendError: '',
      lastOpen: 0,
      openHistory: [],
      lastPopAt: 0,
      pursedMax: 0.17,
      popFromMax: 0.2,
      popToMin: 0.2,
      popCooldownMs: 650,
    }
  },
  methods: {
    onCmd(newVal, oldVal, ownerInstance, instance) {
      if (ownerInstance) this.owner = ownerInstance
      if (instance && !this.owner) this.owner = instance
      if (!this.$ownerInstance && ownerInstance) {
        try { this.$ownerInstance = ownerInstance } catch (e) { /* ignore */ }
      }
      if (!newVal || !newVal.action) return
      if (newVal.action === 'start') {
        this.startCamera()
      } else if (newVal.action === 'stop') {
        this.stopCamera()
      }
    },

    emitToLogic(type, extra) {
      var payload = Object.assign({ type: type }, extra || {})
      try {
        payload = JSON.parse(JSON.stringify(payload))
      } catch (e) {
        payload = { type: type, message: extra && extra.message ? extra.message : '' }
      }

      try {
        if (this.$ownerInstance && typeof this.$ownerInstance.callMethod === 'function') {
          this.$ownerInstance.callMethod('onRenderEvent', payload)
          return
        }
      } catch (e1) { /* fallthrough */ }

      try {
        if (this.owner && typeof this.owner.callMethod === 'function') {
          this.owner.callMethod('onRenderEvent', payload)
          return
        }
      } catch (e2) { /* ignore */ }
    },

    emitSnapshot(snapshot, force) {
      var now = Date.now()
      if (!force && !snapshot.isPop && now - this.lastEmitAt < 90) return
      this.lastEmitAt = now
      this.emitToLogic('snapshot', { data: snapshot })
    },

    getHost() {
      if (typeof document === 'undefined') return null
      return document.getElementById('face-cam-host')
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
      try { video.webkitPlaysInline = true } catch (e) { /* ignore */ }
      video.style.cssText = [
        'position:absolute',
        'inset:0',
        'width:100%',
        'height:100%',
        'object-fit:cover',
        'transform:scaleX(-1)',
        'background:#12082e',
        'z-index:0',
      ].join(';')
      host.appendChild(video)

      var canvas = document.createElement('canvas')
      canvas.width = 360
      canvas.height = 480
      canvas.style.cssText = 'display:none;position:absolute;left:-9999px;top:-9999px;width:1px;height:1px;'
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
      var constraints = [
        {
          audio: false,
          video: {
            facingMode: { ideal: 'user' },
            width: { ideal: 480 },
            height: { ideal: 640 },
            frameRate: { ideal: 20, max: 24 },
          },
        },
        { audio: false, video: { facingMode: 'user' } },
        { audio: false, video: true },
      ]

      var lastErr = null
      for (var i = 0; i < constraints.length; i++) {
        try {
          return await getUserMedia(constraints[i])
        } catch (e) {
          lastErr = e
        }
      }

      var name = lastErr && lastErr.name ? String(lastErr.name) : ''
      if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
        throw new Error('请允许使用摄像头后重试')
      }
      if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
        throw new Error('未找到摄像头设备')
      }
      if (name === 'NotReadableError' || name === 'TrackStartError') {
        throw new Error('摄像头被占用，请关闭其他应用后重试')
      }
      throw new Error('无法打开摄像头，请检查权限')
    },

    async waitVideoReady(video) {
      await new Promise(function (resolve) {
        if (!video) {
          resolve()
          return
        }
        if (video.readyState >= 2 && video.videoWidth > 0) {
          resolve()
          return
        }
        var done = function () {
          video.removeEventListener('loadeddata', done)
          video.removeEventListener('loadedmetadata', done)
          resolve()
        }
        video.addEventListener('loadeddata', done)
        video.addEventListener('loadedmetadata', done)
        setTimeout(resolve, 2500)
      })
    },

    async ensureAssets() {
      if (this.assetUrls) return this.assetUrls

      var urls = {}
      var names = ['face_mesh.js'].concat(SCRIPT_RESOURCE_NAMES, BINARY_RESOURCE_NAMES)

      for (var i = 0; i < names.length; i++) {
        var name = names[i]
        this.emitToLogic('status', { message: '正在读取本地人脸模型 ' + (i + 1) + '/' + names.length })
        urls[name] = await readAppFileAsBlobUrl(name, i < 3 ? 8000 : 20000)
        if (isAppRuntime() && String(urls[name]).indexOf('blob:') === 0) {
          this.assetBlobUrls.push(urls[name])
        }
      }

      this.assetUrls = urls
      return urls
    },

    async loadFaceMeshLib() {
      if (typeof window !== 'undefined' && window.FaceMesh) return window.FaceMesh

      this.disableSimdForApp()
      var urls = await this.ensureAssets()
      this.emitToLogic('status', { message: '正在加载人脸识别模型…' })

      var restoreScriptMode = enterBrowserGlobalScriptMode()
      try {
        await loadScript(urls['face_mesh.js'], 12000)
      } catch (e) {
        restoreScriptMode()
        if (isAppRuntime()) throw e
        restoreScriptMode = enterBrowserGlobalScriptMode()
        try {
          await loadScript(CDN_ROOT + '/face_mesh.js', 20000)
        } finally {
          restoreScriptMode()
        }
      } finally {
        restoreScriptMode()
      }

      if (!window.FaceMesh) {
        throw new Error('人脸模型脚本已加载，但 FaceMesh 未就绪')
      }
      return window.FaceMesh
    },

    disableSimdForApp() {
      if (!isAppRuntime() || typeof window === 'undefined') return
      if (!window.WebAssembly || WebAssembly._faceCamNoSimd) return
      try {
        if (WebAssembly.validate) {
          var originalValidate = WebAssembly.validate
          WebAssembly.validate = function (bytes) {
            try {
              if (bytes && bytes.length && bytes.length < 80) return false
            } catch (e) { /* ignore */ }
            return originalValidate.call(WebAssembly, bytes)
          }
        }
        if (WebAssembly.instantiate) {
          var originalInstantiate = WebAssembly.instantiate
          WebAssembly.instantiate = function (bytes, imports) {
            try {
              if (bytes && bytes.length && bytes.length < 80) {
                return Promise.reject(new Error('SIMD probe disabled'))
              }
            } catch (e2) { /* ignore */ }
            return originalInstantiate.call(WebAssembly, bytes, imports)
          }
        }
        WebAssembly._faceCamNoSimd = true
      } catch (e3) {
        /* ignore */
      }
    },

    async ensureModel() {
      if (rawFaceMesh) return

      var FaceMeshCtor = await this.loadFaceMeshLib()
      var urls = await this.ensureAssets()
      var self = this

      var restoreScriptMode = enterBrowserGlobalScriptMode()
      var faceMesh = null
      try {
        faceMesh = new FaceMeshCtor({
          locateFile: function (file) {
            var clean = String(file || '').replace(/^\//, '')
            if (urls[clean]) {
              console.log('[FaceCam] locateFile blob/local: ' + clean)
              return urls[clean]
            }
            if (!isAppRuntime()) return MODEL_ROOT + '/' + clean
            return APP_MODEL_ROOT + '/' + clean
          },
        })

        faceMesh.setOptions({
          maxNumFaces: 1,
          refineLandmarks: false,
          minDetectionConfidence: 0.2,
          minTrackingConfidence: 0.2,
          selfieMode: true,
        })

        faceMesh.onResults(function (results) {
          self.onMeshResults(results)
        })

        if (typeof faceMesh.initialize === 'function') {
          await faceMesh.initialize()
        }
      } finally {
        restoreScriptMode()
      }

      rawFaceMesh = faceMesh
    },

    pickLandmarks(results) {
      if (!results) return null
      if (results.multiFaceLandmarks && results.multiFaceLandmarks.length > 0) {
        return results.multiFaceLandmarks[0]
      }
      if (results.faceLandmarks && results.faceLandmarks.length > 0) {
        var lm = results.faceLandmarks
        if (lm[0] && typeof lm[0].x === 'number') return lm
        if (lm[0] && lm[0].length) return lm[0]
      }
      return null
    },

    onMeshResults(results) {
      if (!this.running) return
      this.lastResultAt = Date.now()

      var lm = this.pickLandmarks(results)
      var faceDetected = !!(lm && lm.length >= 292)
      var mouthOpen = 0
      var isPursed = false
      var isPop = false
      var isTongueUp = false
      var tongueUpScore = 0
      var isNPoint = false
      var isBlowing = false
      var blowScore = 0
      var statusText = '未检测到整张脸，请后退一点，让眼睛、鼻子、嘴都入画'

      if (faceDetected) {
        var metrics = mouthMetrics(lm)
        mouthOpen = metrics.open
        var tongue = tongueUpMetrics(lm, this.canvas)
        isTongueUp = tongue.isTongueUp
        tongueUpScore = tongue.score
        isNPoint = mouthOpen >= 0.18 && isTongueUp

        this.openHistory.push(mouthOpen)
        if (this.openHistory.length > 16) this.openHistory.shift()

        isPursed = mouthOpen <= this.pursedMax
        blowScore = clamp01((this.pursedMax + 0.08 - mouthOpen) / (this.pursedMax + 0.08))
        if (metrics.width > 0) {
          blowScore = clamp01(blowScore + clamp01((0.45 - metrics.width) / 0.28) * 0.25)
        }
        isBlowing = blowScore >= 0.58 && mouthOpen <= this.pursedMax + 0.08
        var recentMin = Math.min.apply(null, this.openHistory)
        var now = Date.now()

        if (
          this.lastOpen <= this.popFromMax &&
          mouthOpen >= this.popToMin &&
          recentMin <= this.pursedMax + 0.06 &&
          now - this.lastPopAt >= this.popCooldownMs
        ) {
          isPop = true
          this.lastPopAt = now
        }

        if (isNPoint) statusText = '已识别舌尖上顶，请稳稳保持'
        else if (isBlowing) statusText = '已识别嘟嘴吹气，请持续长吹'
        else if (mouthOpen >= 0.18) statusText = '嘴巴已张开，请把舌尖顶到上方小台子'
        else if (isPursed) statusText = '已识别抿唇，请保持蓄力'
        else if (mouthOpen >= this.popToMin) statusText = '已检测到张嘴/弹唇'
        else statusText = '请把嘴唇再抿紧一点'

        this.lastOpen = mouthOpen
      } else {
        this.lastOpen = 0
        this.openHistory = []
      }

      this.emitSnapshot({
        ready: true,
        faceDetected: faceDetected,
        mouthOpen: mouthOpen,
        isPursed: isPursed,
        isPop: isPop,
        isNPoint: isNPoint,
        isTongueUp: isTongueUp,
        tongueUpScore: tongueUpScore,
        isBlowing: isBlowing,
        blowScore: blowScore,
        statusText: statusText,
      })
    },

    grabFrame() {
      var video = this.video
      var canvas = this.canvas
      if (!video || !video.videoWidth || !video.videoHeight) return null

      if (this.lastResultAt === 0 && this.frameCount > 8 && this.frameCount % 2 === 0) {
        return video
      }
      if (!canvas) return video
      try {
        var width = Math.min(360, video.videoWidth)
        var height = Math.max(1, Math.round((video.videoHeight / video.videoWidth) * width))
        if (canvas.width !== width) canvas.width = width
        if (canvas.height !== height) canvas.height = height

        var ctx = null
        try { ctx = canvas.getContext('2d', { willReadFrequently: true }) } catch (e1) { /* ignore */ }
        if (!ctx) ctx = canvas.getContext('2d')
        if (!ctx) return video
        ctx.drawImage(video, 0, 0, width, height)
        return canvas
      } catch (e) {
        return video
      }
    },

    loop() {
      if (!this.running) return
      this.raf = requestAnimationFrame(this.loop.bind(this))
      this.tick()
    },

    tick() {
      if (!this.running || this.sending) return
      var video = this.video
      var faceMesh = rawFaceMesh
      if (!video || !faceMesh || video.readyState < 2 || !video.videoWidth) return

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
        if (self.sendTimer) {
          clearTimeout(self.sendTimer)
          self.sendTimer = 0
        }
      }

      try {
        var result = faceMesh.send({ image: image })
        this.sendTimer = setTimeout(function () {
          self.sendFailCount += 1
          self.lastSendError = '识别单帧超时'
          console.warn('[FaceCam] 识别单帧超时，跳过本帧')
          release()
        }, 1800)
        if (result && typeof result.then === 'function') {
          result.then(function () {
            release()
          }).catch(function (err) {
            self.sendFailCount += 1
            self.lastSendError = err && err.message ? err.message : '模型处理视频帧失败'
            console.warn('[FaceCam] send failed: ' + self.lastSendError, err)
            release()
          })
        } else {
          setTimeout(release, 80)
        }
      } catch (e) {
        this.sendFailCount += 1
        this.lastSendError = e && e.message ? e.message : '模型处理视频帧失败'
        console.warn('[FaceCam] send exception: ' + this.lastSendError, e)
        release()
      }

      if (this.frameCount > 20 && this.lastResultAt === 0 && this.frameCount % 30 === 0) {
        var noResultText = this.sendFailCount > 0
          ? '人脸模型处理视频帧失败：' + this.lastSendError
          : '人脸模型未返回识别结果，正在切换视频帧模式'
        this.emitSnapshot({
          ready: true,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: noResultText,
        }, true)
      }
    },

    async startCamera() {
      this.stopCamera(false)
      this.running = true
      this.sending = false
      this.frameCount = 0
      this.lastResultAt = 0
      this.sendFailCount = 0
      this.lastSendError = ''
      this.lastOpen = 0
      this.openHistory = []
      this.lastPopAt = 0

      try {
        var host = this.getHost()
        if (!host) throw new Error('未找到摄像头区域，请返回重试')
        host.style.position = 'relative'
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
        }, true)

        this.video = this.mountVideo(host)
        this.stream = await this.openStream()
        if (!this.running) return

        this.video.srcObject = this.stream
        this.video.muted = true
        this.video.playsInline = true
        try {
          await this.video.play()
        } catch (e) {
          await new Promise(function (resolve) { setTimeout(resolve, 120) })
          try { await this.video.play() } catch (e2) { /* continue */ }
        }

        await this.waitVideoReady(this.video)
        if (!this.running) return

        this.emitToLogic('started')
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '摄像头已开启，正在加载人脸模型…',
        }, true)

        await this.ensureModel()
        if (!this.running) return

        this.emitSnapshot({
          ready: true,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '请后退一点，让眼睛、鼻子、嘴都在画面里',
        }, true)
        this.loop()
      } catch (e) {
        var message = e && e.message ? e.message : '摄像头/人脸识别启动失败'
        console.error('[FaceCam] start failed: ' + message, e)
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: message,
          error: message,
        }, true)
        this.emitToLogic('error', { message: message })
        this.stopCamera(false)
      }
    },

    stopCamera(emitStatus) {
      this.running = false
      this.sending = false
      if (this.sendTimer) {
        clearTimeout(this.sendTimer)
        this.sendTimer = 0
      }
      if (this.raf) {
        cancelAnimationFrame(this.raf)
        this.raf = 0
      }
      if (this.stream) {
        try {
          this.stream.getTracks().forEach(function (track) {
            try { track.stop() } catch (e) { /* ignore */ }
          })
        } catch (e) { /* ignore */ }
        this.stream = null
      }
      if (this.video) {
        try {
          this.video.srcObject = null
          if (this.video.parentNode) this.video.parentNode.removeChild(this.video)
        } catch (e) { /* ignore */ }
        this.video = null
      }
      if (this.canvas) {
        try {
          if (this.canvas.parentNode) this.canvas.parentNode.removeChild(this.canvas)
        } catch (e2) { /* ignore */ }
        this.canvas = null
      }
      this.lastOpen = 0
      this.openHistory = []
      if (emitStatus !== false) {
        this.emitSnapshot({
          ready: false,
          faceDetected: false,
          mouthOpen: 0,
          isPursed: false,
          isPop: false,
          statusText: '',
        }, true)
      }
    },

    destroyAll() {
      this.stopCamera(false)
      if (rawFaceMesh) {
        try {
          if (typeof rawFaceMesh.close === 'function') rawFaceMesh.close()
        } catch (e) { /* ignore */ }
        rawFaceMesh = null
      }
      if (this.assetBlobUrls && this.assetBlobUrls.length) {
        for (var i = 0; i < this.assetBlobUrls.length; i++) {
          try { URL.revokeObjectURL(this.assetBlobUrls[i]) } catch (e2) { /* ignore */ }
        }
      }
      this.assetBlobUrls = []
      this.assetUrls = null
    },
  },
  beforeDestroy() {
    this.destroyAll()
  },
  unmounted() {
    this.destroyAll()
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
