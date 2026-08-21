<script setup lang="ts">
/**
 * 弹唇啵啵操 · 人脸动作训练
 * App / 手机内直接开前置摄像头识别「抿唇蓄力 → 弹唇发射」，不跳转外部网页
 */
import { ref, computed, onUnmounted, nextTick } from 'vue'
import { onLoad, onHide, onShow, onUnload } from '@dcloudio/uni-app'
import { completeGameCheckinApi } from '@/services/checkin'
import FaceCam from '@/components/FaceCam.vue'
import type { FaceLipSnapshot } from '@/utils/faceLip'
import {
  ensureCameraPermission,
  promptOpenCameraSettings,
} from '@/utils/cameraPermission'
import { playGameSfx, setGameSoundMuted, startGameBgm, stopGameBgm } from '@/utils/gameSound'


const TARGET = 5
const CHARGE_MS = 15000
const CHARGE_TICK = 80
const CHARGE_STEP = (100 / CHARGE_MS) * CHARGE_TICK

type Phase = 'intro' | 'playing' | 'success'

const phase = ref<Phase>('intro')
const score = ref(0)
const energy = ref(0)
const charging = ref(false)
const readyToFire = ref(false)
const blasting = ref(false)
const monsterVisible = ref(true)
const monsterFlying = ref(false)
const paused = ref(false)
const finishing = ref(false)
const statusBarHeight = ref(20)
const taskName = ref('弹唇啵啵操')
const checkinId = ref(0)

const faceEnabled = ref(false)
const faceStarting = ref(false)
const faceReady = ref(false)
const faceDetected = ref(false)
const faceStatus = ref('')
const faceError = ref('')
/** >0 启动摄像头；0 停止。递增可强制重启 */
const camRunToken = ref(0)

let chargeTimer: ReturnType<typeof setInterval> | null = null
let startWatchTimer: ReturnType<typeof setTimeout> | null = null
let lastAutoFireAt = 0
const AUTO_FIRE_COOLDOWN_MS = 900


const progressDots = computed(() =>
  Array.from({ length: TARGET }, (_, i) => ({ lit: i < score.value })),
)

const energyFull = computed(() => energy.value >= 100)

const tipText = computed(() => {
  // 有识别状态文案时优先展示（避免一直卡在「启动中」）
  if (faceError.value) return faceError.value
  if (blasting.value) return '💥 啵！轰飞张嘴怪！'
  if (readyToFire.value) return '能量已满！做一次「弹唇啵」即可发射'
  if (charging.value) return '检测到抿唇，正在蓄力… 保持嘴唇抿紧'
  if (faceReady.value && faceDetected.value && faceStatus.value) return faceStatus.value
  if (faceReady.value && !faceDetected.value) {
    return faceStatus.value || '未检测到人脸，请正对镜头、光线充足'
  }
  if (faceEnabled.value && faceStatus.value) return faceStatus.value
  if (faceStarting.value) return faceStatus.value || '正在启动摄像头与人脸识别…'
  if (!faceEnabled.value) return '请允许摄像头权限，把脸对准画面中央'
  if (faceStatus.value) return faceStatus.value
  return '请抿紧嘴唇蓄力，充满后再弹唇啵一下'
})


const energyLabel = computed(() => {
  if (readyToFire.value) return '满'
  if (charging.value) return `${Math.floor(energy.value)}%`
  return '蓄力'
})

onLoad((q) => {
  try {
    const sys = uni.getSystemInfoSync()
    statusBarHeight.value = sys.statusBarHeight || 20
  } catch {
    /* ignore */
  }
  if (q?.name) {
    try {
      taskName.value = decodeURIComponent(String(q.name))
    } catch {
      taskName.value = String(q.name)
    }
  }
  if (q?.checkin_id) {
    const n = Number(q.checkin_id)
    if (!Number.isNaN(n) && n > 0) checkinId.value = n
  }
})

onHide(() => {
  stopCharge()
  stopFace()
  stopGameBgm()
  if (phase.value === 'playing') paused.value = true
})

onShow(() => {
  // 从后台回来：若仍在对战且未暂停意图为继续，可手动点继续；摄像头在 resume 时再开
})

onUnload(() => {
  cleanupAll()
})

onUnmounted(() => {
  cleanupAll()
})

function cleanupAll() {
  stopCharge()
  stopFace()
  stopGameBgm()
}

function goBack() {
  cleanupAll()
  uni.navigateBack({
    fail: () => uni.switchTab({ url: '/pages/index/index' }),
  })
}

function togglePause() {
  if (phase.value !== 'playing') return
  paused.value = !paused.value
  if (paused.value) {
    playGameSfx('pause')
    stopGameBgm()
    stopCharge()
    // 暂停时关掉摄像头省电/释放
    stopFace()
  } else {
    playGameSfx('resume')
    startGameBgm('cute')
    void startFaceDetect()
  }
}

/** App 端先申请原生摄像头权限；被拒时弹窗引导去系统设置 */
async function requestAppCameraPermission(): Promise<boolean> {
  const result = await ensureCameraPermission(true)
  return result.granted
}


async function startGame() {
  score.value = 0
  energy.value = 0
  readyToFire.value = false
  blasting.value = false
  monsterVisible.value = true
  monsterFlying.value = false
  paused.value = false
  faceError.value = ''
  phase.value = 'playing'
  setGameSoundMuted(false)
  playGameSfx('start')
  startGameBgm('cute')
  // 等对战区 / FaceCam 挂载后再开摄像头
  await nextTick()
  await startFaceDetect()
}

function resetRoundVisual() {
  energy.value = 0
  readyToFire.value = false
  blasting.value = false
  monsterFlying.value = false
  monsterVisible.value = true
}

function startCharge() {
  if (paused.value || readyToFire.value || blasting.value || !monsterVisible.value) return
  if (charging.value) return
  charging.value = true
  playGameSfx('charge')
  chargeTimer = setInterval(() => {
    if (paused.value) return
    energy.value = Math.min(100, energy.value + CHARGE_STEP)
    if (energy.value >= 100) {
      energy.value = 100
      stopCharge()
      readyToFire.value = true
      playGameSfx('resume')
      uni.vibrateShort?.({})
    }
  }, CHARGE_TICK)
}

function stopCharge() {
  charging.value = false
  if (chargeTimer) {
    clearInterval(chargeTimer)
    chargeTimer = null
  }
}

function onFire() {
  if (paused.value || !readyToFire.value || blasting.value || !monsterVisible.value) return
  blasting.value = true
  readyToFire.value = false
  stopCharge()
  monsterFlying.value = true
  playGameSfx('pop')
  uni.vibrateShort?.({})

  setTimeout(() => {
    monsterVisible.value = false
    monsterFlying.value = false
    blasting.value = false
    score.value += 1

    if (score.value >= TARGET) {
      stopFace()
      stopGameBgm()
      phase.value = 'success'
      playGameSfx('success')
      uni.showToast({ icon: 'success', title: '训练成功！' })
      return
    }

    setTimeout(() => {
      resetRoundVisual()
    }, 500)
  }, 700)
}

/** 上次收到 snapshot 的时间戳，用于诊断 */
let lastSnapshotAt = 0

async function startFaceDetect() {
  // 如果正在启动中，不重复触发
  if (faceStarting.value) return
  if (phase.value !== 'playing') return
  // 如果摄像头已启用且近期有数据到达，说明工作正常，无需重启
  if (faceEnabled.value && lastSnapshotAt > 0 && Date.now() - lastSnapshotAt < 5000) return

  // 强制重启：先停掉旧会话
  if (faceEnabled.value) {
    console.log('[bobo] 摄像头已启用但无数据，强制重启')
    stopFace()
    await new Promise((r) => setTimeout(r, 300))
  }

  faceStarting.value = true
  faceError.value = ''
  faceStatus.value = '正在请求摄像头权限…'
  faceReady.value = false
  faceDetected.value = false
  lastSnapshotAt = 0

  const ok = await requestAppCameraPermission()
  if (!ok) {
    faceStarting.value = false
    faceError.value = '未获得摄像头权限，请在系统设置中允许本应用使用摄像头'
    faceStatus.value = faceError.value
    // ensureCameraPermission(true) 已自动弹「去设置」窗口
    return
  }

  if (phase.value !== 'playing' || paused.value) {
    faceStarting.value = false
    return
  }

  faceStatus.value = '正在打开前置摄像头…'
  // 递增 token，触发 FaceCam(renderjs) 启动
  // 用 performance.now() 保证每次值不同（比 Date.now() 精度更高）
  camRunToken.value = (typeof performance !== 'undefined' ? performance.now() : Date.now())

  // 兜底：部分机型 renderjs→逻辑层回调延迟/丢失时，不要永远停在「启动中」
  if (startWatchTimer) clearTimeout(startWatchTimer)
  startWatchTimer = setTimeout(() => {
    if (phase.value !== 'playing' || paused.value) return
    if (faceStarting.value && !faceEnabled.value) {
      faceEnabled.value = true
      faceStarting.value = false
      if (!faceStatus.value || faceStatus.value.includes('打开') || faceStatus.value.includes('请求')) {
        faceStatus.value = '摄像头已开启，正在加载/等待人脸识别…'
      }
    }
  }, 2500)
}

function stopFace() {
  stopCharge()
  lastSnapshotAt = 0
  if (startWatchTimer) {
    clearTimeout(startWatchTimer)
    startWatchTimer = null
  }
  camRunToken.value = 0
  faceEnabled.value = false
  faceStarting.value = false
  faceReady.value = false
  faceDetected.value = false
}

function onCamStarted() {
  faceEnabled.value = true
  faceStarting.value = false
  faceError.value = ''
  if (!faceStatus.value || faceStatus.value.includes('打开') || faceStatus.value.includes('请求')) {
    faceStatus.value = '摄像头已开启，正在加载人脸模型…'
  }
}

function onCamError(msg: string) {
  playGameSfx('error')
  if (startWatchTimer) {
    clearTimeout(startWatchTimer)
    startWatchTimer = null
  }
  faceStarting.value = false
  faceEnabled.value = false
  faceReady.value = false
  faceError.value = msg || '摄像头启动失败'
  faceStatus.value = faceError.value
  camRunToken.value = 0

  const lower = (msg || '').toLowerCase()
  const isPermission =
    lower.includes('权限') ||
    lower.includes('允许') ||
    lower.includes('notallowed') ||
    lower.includes('permission') ||
    lower.includes('denied')

  if (isPermission) {
    void promptOpenCameraSettings(
      msg ||
        '训练需要使用前置摄像头。请在系统设置中允许本应用使用「摄像头」后返回，再点继续重试。',
    )
  } else {
    uni.showModal({
      title: '人脸识别启动失败',
      content: faceError.value,
      confirmText: '重试',
      cancelText: '取消',
      success: (res) => {
        if (res.confirm && phase.value === 'playing' && !paused.value) {
          void startFaceDetect()
        }
      },
    })
  }
}


function onCamStatus(msg: string) {
  if (msg) faceStatus.value = msg
  // 有状态回传说明 renderjs→逻辑层通道通了，解除纯启动态
  if (faceStarting.value) {
    faceEnabled.value = true
    faceStarting.value = false
    lastSnapshotAt = Date.now() // 记录通信成功时间
  }
}

function onFaceSnapshot(s: FaceLipSnapshot) {
  // 任意 snapshot 都说明 renderjs 通信成功，记录时间戳
  lastSnapshotAt = Date.now()

  if (faceStarting.value) {
    faceStarting.value = false
    faceEnabled.value = true
  }
  if (startWatchTimer) {
    clearTimeout(startWatchTimer)
    startWatchTimer = null
  }

  faceReady.value = s.ready
  faceDetected.value = s.faceDetected
  faceStatus.value = s.statusText || faceStatus.value
  if (s.error) {
    faceError.value = s.error
  } else if (s.ready) {
    // 识别就绪后清掉启动期错误
    faceError.value = ''
  }
  if (s.ready || s.faceDetected) {
    faceEnabled.value = true
    faceStarting.value = false
  }

  if (phase.value !== 'playing' || paused.value) {
    stopCharge()
    return
  }
  if (!s.ready || !s.faceDetected) {
    stopCharge()
    return
  }

  // 抿唇 → 自动蓄力
  if (s.isPursed && !readyToFire.value && !blasting.value && monsterVisible.value) {
    startCharge()
  } else if (!s.isPursed && charging.value) {
    stopCharge()
  }

  // 弹唇 → 能量满时自动发射
  if (s.isPop && readyToFire.value && !blasting.value) {
    const now = Date.now()
    if (now - lastAutoFireAt >= AUTO_FIRE_COOLDOWN_MS) {
      lastAutoFireAt = now
      onFire()
    }
  }
}

function playAgain() {
  startGame()
}

async function finishAndBack() {
  if (finishing.value) return
  finishing.value = true
  try {
    if (checkinId.value > 0) {
      const result = await completeGameCheckinApi({
        checkin_id: checkinId.value,
        description: '游戏训练完成：弹唇啵啵操',
      })
      const coins = result.coins_awarded || 0
      uni.showToast({
        icon: 'success',
        title: coins > 0 ? `打卡成功 +${coins}金币` : '已记录训练',
        duration: 1800,
      })
      await new Promise((r) => setTimeout(r, 600))
    } else {
      uni.showToast({ icon: 'none', title: '未关联任务，仅本地完成' })
      await new Promise((r) => setTimeout(r, 500))
    }
  } catch (e) {
    const msg = e instanceof Error ? e.message : '提交失败'
    uni.showToast({ icon: 'none', title: msg })
    finishing.value = false
    return
  }
  finishing.value = false
  cleanupAll()
  uni.navigateBack({
    fail: () => uni.switchTab({ url: '/pages/index/index' }),
  })
}
</script>

<template>
  <view class="game-page">
    <view class="nav" :style="{ paddingTop: statusBarHeight + 'px' }">
      <view class="nav-btn" @tap="goBack">‹</view>
      <view class="nav-title-wrap">
        <text class="nav-title">{{ taskName || '弹唇啵啵操' }}</text>
        <text class="nav-sub">人脸动作 · 自动识别</text>
      </view>
      <view class="nav-right">
        <view
          v-if="phase === 'playing'"
          class="nav-btn sm"
          @tap="togglePause"
        >{{ paused ? '▶' : 'Ⅱ' }}</view>
      </view>
    </view>

    <!-- ===== 引导 ===== -->
    <view v-if="phase === 'intro'" class="intro">
      <view class="intro-art">
        <text class="art-face">😗</text>
        <text class="art-bolt">⚡</text>
        <text class="art-monster">👾</text>
      </view>

      <text class="intro-tag">能量炮 · 轰飞张嘴怪</text>
      <text class="intro-name">{{ taskName || '弹唇啵啵操' }}</text>
      <text class="intro-desc">全程人脸识别，无需按键：抿唇蓄力，张嘴啵一下发射！</text>

      <view class="steps">
        <view class="step">
          <text class="step-ico">📷</text>
          <text class="step-txt">开启摄像头，脸置于画面中央</text>
        </view>
        <view class="step">
          <text class="step-ico">🤐</text>
          <text class="step-txt">识别「抿唇」自动蓄力（约 4 秒）</text>
        </view>
        <view class="step">
          <text class="step-ico">💥</text>
          <text class="step-txt">识别「弹唇啵」自动轰飞张嘴怪</text>
        </view>
      </view>

      <view class="trophy-line">🏆 点亮全部 {{ TARGET }} 个圆圈即训练成功</view>

      <view class="hint-card">
        <text class="hint-ico">💡</text>
        <text class="hint-txt">
          开始后将直接打开前置摄像头（请允许权限）。训练中无按键，仅靠抿唇蓄力、弹唇发射。动作是否标准以老师点评为准。
        </text>
      </view>

      <view class="start-btn" @tap="startGame">
        <text class="start-ico">▶</text>
        <text>开始训练</text>
      </view>
    </view>

    <!-- ===== 对战中：人脸居中大窗 ===== -->
    <view v-else-if="phase === 'playing'" class="play">
      <view class="hud">
        <view class="progress-pill">
          <view
            v-for="(d, i) in progressDots"
            :key="i"
            class="dot"
            :class="{ lit: d.lit }"
          />
          <text class="prog-num">{{ score }}/{{ TARGET }}</text>
        </view>
        <view class="rec-pill">
          <text class="rec-dot">●</text>
          <text>识别中</text>
        </view>
      </view>

      <view class="cam-center">
        <view class="cam-frame" :class="{ on: faceEnabled || faceStarting, full: energyFull }">
          <!-- App/H5：renderjs 在组件内直接开摄像头，不跳网页 -->
          <view class="cam-host">
            <FaceCam
              :run-token="camRunToken"
              @snapshot="onFaceSnapshot"
              @error="onCamError"
              @started="onCamStarted"
              @status="onCamStatus"
            />

          </view>

          <view v-if="!faceEnabled && !faceStarting" class="cam-placeholder">
            <text class="ph-ico">📷</text>
            <text class="ph-txt">正在准备前置摄像头…</text>
          </view>
          <view v-else-if="faceStarting && !faceEnabled" class="cam-placeholder dim">
            <text class="ph-ico">⏳</text>
            <text class="ph-txt">{{ faceStatus || '正在启动摄像头…' }}</text>
          </view>

          <view class="face-ring" :class="{ ok: faceDetected, charge: charging, full: energyFull }" />

          <view class="cam-badge" :class="{ ok: faceDetected, warn: !faceDetected && faceReady }">
            <text v-if="faceStarting">启动中…</text>
            <text v-else-if="faceEnabled && faceDetected">人脸已锁定</text>
            <text v-else-if="faceEnabled">寻找人脸…</text>
            <text v-else>等待摄像头</text>
          </view>

          <view class="cam-energy">
            <view class="cam-energy-track">
              <view
                class="cam-energy-fill"
                :class="{ full: energyFull }"
                :style="{ width: energy + '%' }"
              />
            </view>
            <text class="cam-energy-label">{{ energyLabel }} · {{ Math.floor(energy) }}%</text>
          </view>
        </view>
      </view>

      <view class="stage-mini">
        <view
          v-if="monsterVisible"
          class="monster"
          :class="{ flying: monsterFlying, shake: charging }"
        >
          <text class="monster-face">👾</text>
          <view class="monster-tag">{{ monsterFlying ? '被轰飞！' : (readyToFire ? '等你弹唇啵' : '张嘴怪') }}</view>
        </view>
        <view v-else class="monster-empty">
          <text>下一只马上出现…</text>
        </view>
        <view v-if="blasting" class="blast-fx">💥 啵！</view>
      </view>

      <view class="bottom-tip">
        <view class="tip-bubble">
          <text class="tip-main">{{ tipText }}</text>
          <view class="tip-brand">💗 爱脸动动 · 纯人脸操控</view>
        </view>
      </view>

      <view v-if="paused" class="pause-mask" @tap="togglePause">
        <text class="pause-title">已暂停</text>
        <text class="pause-sub">点击继续</text>
      </view>
    </view>

    <!-- ===== 成功 ===== -->
    <view v-else class="success">
      <text class="success-emoji">🎉</text>
      <text class="success-title">训练成功！</text>
      <text class="success-desc">
        你点亮了全部 {{ TARGET }} 个圆圈，轰飞了 {{ score }} 只张嘴怪！
      </text>

      <view class="success-dots">
        <view v-for="(d, i) in progressDots" :key="i" class="dot big lit" />
      </view>

      <view class="success-actions">
        <view class="s-btn ghost" :class="{ disabled: finishing }" @tap="playAgain">再玩一次</view>
        <view class="s-btn primary" :class="{ disabled: finishing }" @tap="finishAndBack">
          {{ finishing ? '提交中…' : '完成打卡 · +5金币' }}
        </view>
      </view>
      <text class="success-note">完成后将自动勾选任务并获得金币；也可回首页再上传视频给老师点评</text>
    </view>
  </view>
</template>

<style lang="scss">
.game-page {
  min-height: 100vh;
  background: linear-gradient(180deg, #2a1478 0%, #4b2bb5 40%, #6a3fd4 100%);
  color: #fff;
  box-sizing: border-box;
  position: relative;
  overflow: hidden;
}

.nav {
  display: flex;
  align-items: center;
  padding-left: 16rpx;
  padding-right: 16rpx;
  padding-bottom: 12rpx;
  position: relative;
  z-index: 5;
}

.nav-btn {
  width: 72rpx;
  height: 72rpx;
  border-radius: 36rpx;
  background: rgba(0, 0, 0, 0.25);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 44rpx;
  line-height: 1;

  &.sm {
    font-size: 28rpx;
    width: 64rpx;
    height: 64rpx;
  }
}

.nav-title-wrap {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0 12rpx;
}

.nav-title {
  font-size: 32rpx;
  font-weight: 700;
}

.nav-sub {
  margin-top: 4rpx;
  font-size: 20rpx;
  opacity: 0.7;
}

.nav-right {
  width: 72rpx;
  display: flex;
  justify-content: flex-end;
}

/* ---- intro ---- */
.intro {
  padding: 24rpx 40rpx calc(48rpx + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.intro-art {
  position: relative;
  width: 280rpx;
  height: 200rpx;
  margin: 24rpx 0 16rpx;
}

.art-face {
  font-size: 120rpx;
  position: absolute;
  left: 20rpx;
  top: 20rpx;
}

.art-bolt {
  font-size: 56rpx;
  position: absolute;
  left: 130rpx;
  top: 70rpx;
}

.art-monster {
  font-size: 88rpx;
  position: absolute;
  right: 10rpx;
  top: 40rpx;
}

.intro-tag {
  font-size: 24rpx;
  opacity: 0.85;
  background: rgba(255, 255, 255, 0.12);
  padding: 8rpx 24rpx;
  border-radius: 24rpx;
}

.intro-name {
  margin-top: 20rpx;
  font-size: 48rpx;
  font-weight: 800;
}

.intro-desc {
  margin-top: 12rpx;
  font-size: 26rpx;
  opacity: 0.9;
  line-height: 1.5;
  padding: 0 12rpx;
}

.steps {
  width: 100%;
  margin-top: 36rpx;
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.step {
  display: flex;
  align-items: center;
  background: rgba(0, 0, 0, 0.22);
  border-radius: 20rpx;
  padding: 20rpx 24rpx;
  text-align: left;
}

.step-ico {
  font-size: 36rpx;
  margin-right: 16rpx;
}

.step-txt {
  font-size: 26rpx;
  line-height: 1.4;
  flex: 1;
}

.trophy-line {
  margin-top: 28rpx;
  font-size: 26rpx;
  font-weight: 600;
}

.hint-card {
  margin-top: 28rpx;
  width: 100%;
  box-sizing: border-box;
  background: rgba(255, 255, 255, 0.1);
  border: 2rpx solid rgba(255, 255, 255, 0.15);
  border-radius: 20rpx;
  padding: 20rpx 24rpx;
  display: flex;
  gap: 12rpx;
  text-align: left;
}

.hint-ico {
  font-size: 28rpx;
}

.hint-txt {
  flex: 1;
  font-size: 22rpx;
  line-height: 1.5;
  opacity: 0.9;
}

.start-btn {
  margin-top: 40rpx;
  width: 100%;
  height: 96rpx;
  border-radius: 48rpx;
  background: linear-gradient(90deg, #ff6b9d, #ff8e53);
  box-shadow: 0 12rpx 28rpx rgba(255, 107, 157, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32rpx;
  font-weight: 700;
  gap: 12rpx;
}

.start-ico {
  font-size: 28rpx;
}

/* ---- play ---- */
.play {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0 24rpx calc(24rpx + env(safe-area-inset-bottom));
  min-height: calc(100vh - 120rpx);
  box-sizing: border-box;
  position: relative;
}

.hud {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16rpx;
}

.progress-pill {
  display: flex;
  align-items: center;
  gap: 10rpx;
  background: rgba(0, 0, 0, 0.3);
  padding: 10rpx 20rpx;
  border-radius: 28rpx;
}

.dot {
  width: 18rpx;
  height: 18rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.25);
  border: 2rpx solid rgba(255, 255, 255, 0.35);

  &.lit {
    background: #ffd76a;
    border-color: #ffe9a8;
    box-shadow: 0 0 10rpx rgba(255, 215, 106, 0.8);
  }

  &.big {
    width: 28rpx;
    height: 28rpx;
  }
}

.prog-num {
  margin-left: 8rpx;
  font-size: 24rpx;
  font-weight: 600;
}

.rec-pill {
  display: flex;
  align-items: center;
  gap: 8rpx;
  background: rgba(255, 60, 80, 0.25);
  padding: 10rpx 18rpx;
  border-radius: 28rpx;
  font-size: 22rpx;
}

.rec-dot {
  color: #ff4d6a;
  font-size: 18rpx;
  animation: blink 1s infinite;
}

@keyframes blink {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}

.cam-center {
  width: 100%;
  display: flex;
  justify-content: center;
  flex: 1;
  min-height: 520rpx;
  align-items: center;
}

.cam-frame {
  position: relative;
  width: min(92vw, 640rpx);
  aspect-ratio: 3 / 4;
  border-radius: 36rpx;
  overflow: hidden;
  background: #12082e;
  border: 4rpx solid rgba(255, 255, 255, 0.2);
  box-shadow: 0 16rpx 40rpx rgba(0, 0, 0, 0.35);

  &.on {
    border-color: rgba(120, 200, 255, 0.55);
  }

  &.full {
    border-color: rgba(255, 215, 106, 0.9);
    box-shadow: 0 0 28rpx rgba(255, 215, 106, 0.45);
  }
}

.cam-host {
  position: absolute;
  inset: 0;
  z-index: 0;
  overflow: hidden;
}

.cam-placeholder {
  position: absolute;
  inset: 0;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: rgba(18, 8, 46, 0.72);
  padding: 32rpx;
  box-sizing: border-box;
  pointer-events: none;

  &.dim {
    background: rgba(18, 8, 46, 0.45);
  }
}

.ph-ico {
  font-size: 64rpx;
}

.ph-txt {
  margin-top: 16rpx;
  font-size: 26rpx;
  text-align: center;
  line-height: 1.45;
  opacity: 0.9;
}

.face-ring {
  position: absolute;
  left: 16%;
  right: 16%;
  top: 8%;
  bottom: 30%;
  border: 3rpx dashed rgba(255, 255, 255, 0.35);
  border-radius: 50%;
  z-index: 3;
  pointer-events: none;
  transition: border-color 0.2s, box-shadow 0.2s;

  &.ok {
    border-style: solid;
    border-color: rgba(100, 220, 160, 0.85);
  }

  &.charge {
    border-color: rgba(255, 180, 80, 0.95);
    box-shadow: 0 0 24rpx rgba(255, 160, 60, 0.4);
  }

  &.full {
    border-color: rgba(255, 215, 106, 1);
    box-shadow: 0 0 28rpx rgba(255, 215, 106, 0.55);
  }
}

.cam-badge {
  position: absolute;
  top: 16rpx;
  left: 50%;
  transform: translateX(-50%);
  z-index: 4;
  background: rgba(0, 0, 0, 0.45);
  padding: 8rpx 20rpx;
  border-radius: 20rpx;
  font-size: 22rpx;
  white-space: nowrap;

  &.ok {
    background: rgba(30, 140, 90, 0.75);
  }

  &.warn {
    background: rgba(160, 100, 20, 0.75);
  }
}

.cam-energy {
  position: absolute;
  left: 24rpx;
  right: 24rpx;
  bottom: 20rpx;
  z-index: 4;
}

.cam-energy-track {
  height: 16rpx;
  border-radius: 8rpx;
  background: rgba(0, 0, 0, 0.4);
  overflow: hidden;
}

.cam-energy-fill {
  height: 100%;
  border-radius: 8rpx;
  background: linear-gradient(90deg, #5ad1ff, #7d8cff);
  transition: width 0.08s linear;

  &.full {
    background: linear-gradient(90deg, #ffd76a, #ff8e53);
  }
}

.cam-energy-label {
  display: block;
  margin-top: 8rpx;
  font-size: 22rpx;
  text-align: center;
  text-shadow: 0 2rpx 6rpx rgba(0, 0, 0, 0.6);
}

.stage-mini {
  position: relative;
  width: 100%;
  min-height: 140rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: 8rpx;
}

.monster {
  display: flex;
  flex-direction: column;
  align-items: center;
  transition: transform 0.7s ease-in, opacity 0.7s ease-in;

  &.shake {
    animation: shake 0.4s ease-in-out infinite;
  }

  &.flying {
    transform: translateY(-280rpx) scale(0.3) rotate(25deg);
    opacity: 0;
  }
}

.monster-face {
  font-size: 100rpx;
  line-height: 1;
  filter: drop-shadow(0 8rpx 16rpx rgba(0, 0, 0, 0.35));
}

.monster-tag {
  margin-top: 4rpx;
  font-size: 22rpx;
  background: rgba(0, 0, 0, 0.35);
  padding: 6rpx 16rpx;
  border-radius: 20rpx;
}

.monster-empty {
  font-size: 24rpx;
  opacity: 0.7;
}

@keyframes shake {
  0%,
  100% {
    transform: translateX(0);
  }
  25% {
    transform: translateX(-6rpx);
  }
  75% {
    transform: translateX(6rpx);
  }
}

.blast-fx {
  position: absolute;
  font-size: 48rpx;
  font-weight: 800;
  animation: blast-pop 0.6s ease-out;
  text-shadow: 0 4rpx 12rpx rgba(0, 0, 0, 0.4);
}

@keyframes blast-pop {
  0% {
    transform: scale(0.4);
    opacity: 0;
  }
  40% {
    transform: scale(1.2);
    opacity: 1;
  }
  100% {
    transform: scale(1);
    opacity: 0.9;
  }
}

.bottom-tip {
  width: 100%;
  padding: 16rpx 32rpx 8rpx;
  box-sizing: border-box;
}

.tip-bubble {
  background: rgba(0, 0, 0, 0.35);
  border-radius: 24rpx;
  padding: 24rpx 28rpx;
  text-align: center;
}

.tip-main {
  display: block;
  font-size: 28rpx;
  line-height: 1.45;
  font-weight: 500;
}

.tip-brand {
  margin-top: 12rpx;
  font-size: 22rpx;
  opacity: 0.7;
}

.pause-mask {
  position: absolute;
  inset: 0;
  background: rgba(20, 8, 50, 0.82);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  z-index: 20;
}

.pause-title {
  font-size: 48rpx;
  font-weight: 700;
}

.pause-sub {
  margin-top: 16rpx;
  font-size: 28rpx;
  opacity: 0.8;
}

.success {
  padding: 48rpx 40rpx calc(48rpx + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.success-emoji {
  font-size: 100rpx;
}

.success-title {
  margin-top: 16rpx;
  font-size: 44rpx;
  font-weight: 700;
}

.success-desc {
  margin-top: 16rpx;
  font-size: 26rpx;
  opacity: 0.9;
  line-height: 1.5;
}

.success-dots {
  display: flex;
  gap: 16rpx;
  margin: 40rpx 0;
}

.success-actions {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  margin-top: 12rpx;
}

.s-btn {
  height: 92rpx;
  border-radius: 46rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30rpx;
  font-weight: 600;

  &.primary {
    background: linear-gradient(90deg, #ff6b9d, #ff8e53);
    box-shadow: 0 12rpx 28rpx rgba(255, 107, 157, 0.4);
  }

  &.ghost {
    background: rgba(255, 255, 255, 0.15);
    border: 2rpx solid rgba(255, 255, 255, 0.35);
  }

  &.disabled {
    opacity: 0.55;
    pointer-events: none;
  }
}

.success-note {
  margin-top: 28rpx;
  font-size: 22rpx;
  opacity: 0.65;
  line-height: 1.5;
  padding: 0 12rpx;
}
</style>
