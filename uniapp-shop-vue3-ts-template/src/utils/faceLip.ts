/**
 * 人脸唇部动作识别类型与 H5 辅助工具
 * - 抿唇：嘴巴闭合/抿紧
 * - 弹唇：由闭合突然张开（啵）
 *
 * App 实战路径：pages/game/bobo 使用 components/FaceCam.vue（renderjs）
 * 在 App 内直接开前置摄像头，不跳转外部网页。
 * 本文件保留类型定义与 H5 可直接调用的 FaceLipDetector。
 */


export type FaceLipSnapshot = {
    ready: boolean
    faceDetected: boolean
    /** 张口程度 0~1（越大越张） */
    mouthOpen: number
    /** 当前判定为抿唇 */
    isPursed: boolean
    /** 本帧触发弹唇（瞬时） */
    isPop: boolean
    /** N点训练近似：张大嘴并检测到舌尖上顶 */
    isNPoint?: boolean
    /** 舌尖上顶近似识别结果 */
    isTongueUp?: boolean
    /** 舌色/上顶置信度 0~1 */
    tongueUpScore?: number
    /** 吹气球近似：闭嘴/嘟嘴吹气姿态 */
    isBlowing?: boolean
    /** 吹气姿态置信度 0~1 */
    blowScore?: number
    /** 状态文案 */
    statusText: string
    error?: string
}

type Landmark = { x: number; y: number; z?: number }

type FaceLandmarkerLike = {
    detectForVideo: (
        video: HTMLVideoElement,
        timestamp: number,
    ) => { faceLandmarks?: Landmark[][] }
    close?: () => void
}

const MODEL_URL =
    'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task'
const WASM_CDN = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm'

/** 上唇 / 下唇关键点（MediaPipe Face Mesh） */
const UPPER_LIP = 13
const LOWER_LIP = 14
const MOUTH_LEFT = 61
const MOUTH_RIGHT = 291

function dist(a: Landmark, b: Landmark) {
    const dx = a.x - b.x
    const dy = a.y - b.y
    return Math.sqrt(dx * dx + dy * dy)
}

function mouthOpenRatio(lm: Landmark[]): number {
    if (!lm || lm.length < 292) return 0
    const vertical = dist(lm[UPPER_LIP], lm[LOWER_LIP])
    const horizontal = dist(lm[MOUTH_LEFT], lm[MOUTH_RIGHT]) || 1e-6
    // 归一化：闭合约 0.02~0.06，张口可到 0.3+
    return Math.min(1, vertical / horizontal)
}

type VisionModule = {
    FaceLandmarker: {
        createFromOptions: (
            fileset: unknown,
            opts: Record<string, unknown>,
        ) => Promise<FaceLandmarkerLike>
    }
    FilesetResolver: {
        forVisionTasks: (path: string) => Promise<unknown>
    }
}

async function loadVisionModule(): Promise<VisionModule> {
    // 运行时 CDN 动态加载；用 Function 绕过打包器静态解析
    const importer = new Function('u', 'return import(u)') as (
        u: string,
    ) => Promise<VisionModule>
    return importer('https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/+esm')
}

/** 是否可能支持摄像头识别（手机浏览器 / App WebView） */
export function canUseFaceCamera(): boolean {
    try {
        return (
            typeof window !== 'undefined' &&
            typeof document !== 'undefined' &&
            typeof navigator !== 'undefined' &&
            !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia)
        )
    } catch {
        return false
    }
}

/**
 * 在容器内创建原生 HTMLVideoElement（手机端必须用这个，不能用 uni video 组件）
 */
export function mountFaceVideo(host: HTMLElement): HTMLVideoElement {
    // 清掉旧节点
    while (host.firstChild) host.removeChild(host.firstChild)

    const video = document.createElement('video')
    video.className = 'face-cam-native'
    video.setAttribute('autoplay', 'true')
    video.setAttribute('muted', 'true')
    video.setAttribute('playsinline', 'true')
    video.setAttribute('webkit-playsinline', 'true')
    // 禁止系统控件
    video.controls = false
    video.muted = true
    video.playsInline = true
    // @ts-expect-error 旧 WebKit
    video.webkitPlaysInline = true
    video.disablePictureInPicture = true
    video.style.cssText = [
        'width:100%',
        'height:100%',
        'object-fit:cover',
        'transform:scaleX(-1)',
        'background:#12082e',
        'display:block',
        'position:absolute',
        'inset:0',
        'z-index:0',
    ].join(';')

    host.appendChild(video)
    return video
}

export class FaceLipDetector {
    private video: HTMLVideoElement | null = null
    private stream: MediaStream | null = null
    private landmarker: FaceLandmarkerLike | null = null
    private raf = 0
    private running = false
    private lastOpen = 0
    private openHistory: number[] = []
    private onUpdate: ((s: FaceLipSnapshot) => void) | null = null
    private initPromise: Promise<void> | null = null
    private lastError = ''
    private lastPopAt = 0

    /** 抿唇阈值：张口低于此值（手机端略放宽，更好识别） */
    pursedMax = 0.1
    /** 弹唇：从低到高的跃变 */
    popFromMax = 0.12
    popToMin = 0.15
    /** 弹唇冷却，避免连续误触发 */
    popCooldownMs = 700

    isSupported(): boolean {
        return canUseFaceCamera()
    }

    async start(
        videoEl: HTMLVideoElement,
        onUpdate: (s: FaceLipSnapshot) => void,
    ): Promise<void> {
        this.onUpdate = onUpdate
        this.video = videoEl
        this.running = true
        this.lastError = ''
        this.openHistory = []
        this.lastOpen = 0
        this.lastPopAt = 0

        try {
            await this.ensureCamera()
            this.emit({
                ready: false,
                faceDetected: false,
                mouthOpen: 0,
                isPursed: false,
                isPop: false,
                statusText: '正在加载人脸模型…',
            })
            await this.ensureModel()
            if (!this.running) return
            this.loop()
            this.emit({
                ready: true,
                faceDetected: false,
                mouthOpen: 0,
                isPursed: false,
                isPop: false,
                statusText: '请把脸对准画面中央',
            })
        } catch (e) {
            const msg = e instanceof Error ? e.message : '摄像头/人脸模型启动失败'
            this.lastError = msg
            this.emit({
                ready: false,
                faceDetected: false,
                mouthOpen: 0,
                isPursed: false,
                isPop: false,
                statusText: msg,
                error: msg,
            })
            throw e
        }
    }

    stop() {
        this.running = false
        if (this.raf) {
            cancelAnimationFrame(this.raf)
            this.raf = 0
        }
        if (this.stream) {
            this.stream.getTracks().forEach((t) => {
                try {
                    t.stop()
                } catch {
                    /* ignore */
                }
            })
            this.stream = null
        }
        if (this.video) {
            try {
                this.video.srcObject = null
                this.video.removeAttribute('src')
                this.video.load?.()
            } catch {
                /* ignore */
            }
        }
        try {
            this.landmarker?.close?.()
        } catch {
            /* ignore */
        }
        this.landmarker = null
        this.initPromise = null
        this.openHistory = []
        this.lastOpen = 0
        this.onUpdate = null
    }

    private emit(s: FaceLipSnapshot) {
        this.onUpdate?.(s)
    }

    private async ensureCamera() {
        if (!this.video) throw new Error('video 未绑定')
        if (!navigator.mediaDevices?.getUserMedia) {
            throw new Error('当前环境不支持摄像头，请用手机浏览器打开')
        }

        // 手机端优先前置摄像头；分辨率偏低以保证流畅
        const tryConstraints: MediaStreamConstraints[] = [
            {
                audio: false,
                video: {
                    facingMode: { ideal: 'user' },
                    width: { ideal: 480 },
                    height: { ideal: 640 },
                    frameRate: { ideal: 24, max: 30 },
                },
            },
            {
                audio: false,
                video: {
                    facingMode: 'user',
                    width: { ideal: 640 },
                    height: { ideal: 480 },
                },
            },
            { audio: false, video: true },
        ]

        let lastErr: unknown = null
        for (const c of tryConstraints) {
            try {
                this.stream = await navigator.mediaDevices.getUserMedia(c)
                break
            } catch (e) {
                lastErr = e
            }
        }
        if (!this.stream) {
            const name =
                lastErr && typeof lastErr === 'object' && 'name' in lastErr
                    ? String((lastErr as { name: string }).name)
                    : ''
            if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
                throw new Error('请允许使用摄像头后重试')
            }
            if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
                throw new Error('未找到摄像头设备')
            }
            if (name === 'NotReadableError' || name === 'TrackStartError') {
                throw new Error('摄像头被占用，请关闭其他应用后重试')
            }
            throw new Error('无法打开摄像头，请检查权限与浏览器设置')
        }

        const video = this.video
        video.srcObject = this.stream
        video.muted = true
        video.playsInline = true
        // @ts-expect-error 旧 WebKit
        video.webkitPlaysInline = true

        try {
            await video.play()
        } catch {
            // 部分手机需要再次 play
            await new Promise((r) => setTimeout(r, 120))
            await video.play().catch(() => {
                /* 继续等 loadeddata */
            })
        }

        await new Promise<void>((resolve) => {
            if (video.readyState >= 2 && video.videoWidth > 0) {
                resolve()
                return
            }
            const onMeta = () => {
                video.removeEventListener('loadeddata', onMeta)
                video.removeEventListener('loadedmetadata', onMeta)
                resolve()
            }
            video.addEventListener('loadeddata', onMeta)
            video.addEventListener('loadedmetadata', onMeta)
            setTimeout(resolve, 2500)
        })
    }

    private async ensureModel() {
        if (this.landmarker) return
        if (this.initPromise) return this.initPromise

        this.initPromise = (async () => {
            const vision = await loadVisionModule()
            const fileset = await vision.FilesetResolver.forVisionTasks(WASM_CDN)
            // 手机优先 CPU，兼容性更好；失败再试 GPU
            try {
                this.landmarker = await vision.FaceLandmarker.createFromOptions(fileset, {
                    baseOptions: {
                        modelAssetPath: MODEL_URL,
                        delegate: 'CPU',
                    },
                    runningMode: 'VIDEO',
                    numFaces: 1,
                })
            } catch {
                this.landmarker = await vision.FaceLandmarker.createFromOptions(fileset, {
                    baseOptions: {
                        modelAssetPath: MODEL_URL,
                        delegate: 'GPU',
                    },
                    runningMode: 'VIDEO',
                    numFaces: 1,
                })
            }
        })()

        try {
            await this.initPromise
        } catch (e) {
            this.initPromise = null
            const msg = e instanceof Error ? e.message : '人脸模型加载失败'
            throw new Error(`人脸识别模型加载失败：${msg}`)
        }
    }

    private loop = () => {
        if (!this.running) return
        this.raf = requestAnimationFrame(this.loop)
        this.tick()
    }

    private tick() {
        const video = this.video
        const landmarker = this.landmarker
        if (!video || !landmarker || video.readyState < 2) return

        let faceDetected = false
        let mouthOpen = 0
        let isPursed = false
        let isPop = false
        let statusText = '请把脸对准画面'

        try {
            const result = landmarker.detectForVideo(video, performance.now())
            const faces = result?.faceLandmarks || []
            if (faces.length > 0) {
                faceDetected = true
                mouthOpen = mouthOpenRatio(faces[0])
                this.openHistory.push(mouthOpen)
                if (this.openHistory.length > 14) this.openHistory.shift()

                isPursed = mouthOpen <= this.pursedMax

                // 弹唇：近期有闭合，当前明显张开
                const recentMin = Math.min(...this.openHistory)
                const now = Date.now()
                if (
                    this.lastOpen <= this.popFromMax &&
                    mouthOpen >= this.popToMin &&
                    recentMin <= this.pursedMax + 0.03 &&
                    now - this.lastPopAt >= this.popCooldownMs
                ) {
                    isPop = true
                    this.lastPopAt = now
                }

                if (isPursed) statusText = '已识别抿唇 ✓ 请保持'
                else if (mouthOpen > 0.2) statusText = '嘴巴张开中…（弹唇要「啵」一下）'
                else statusText = '请抿住小嘴巴'
            } else {
                this.openHistory = []
                statusText = '未检测到人脸，请正对摄像头'
            }
            this.lastOpen = mouthOpen
        } catch {
            statusText = '识别中…'
        }

        this.emit({
            ready: true,
            faceDetected,
            mouthOpen,
            isPursed,
            isPop,
            statusText,
            error: this.lastError || undefined,
        })
    }
}

/** 单例便于页面复用 */
let shared: FaceLipDetector | null = null
export function getFaceLipDetector() {
    if (!shared) shared = new FaceLipDetector()
    return shared
}
