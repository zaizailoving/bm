<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { onHide, onLoad, onUnload } from '@dcloudio/uni-app'
import FaceCam from '@/components/FaceCam.vue'
import { completeGameCheckinApi } from '@/services/checkin'
import type { FaceLipSnapshot } from '@/utils/faceLip'
import { playGameSfx, setGameSoundMuted, startGameBgm, stopGameBgm } from '@/utils/gameSound'

type Phase = 'intro' | 'playing' | 'success'

const TARGET = 5
const HOLD_MS = 10000
const TICK_MS = 100
const DECAY_STEP = 220

const phase = ref<Phase>('intro')
const statusBarHeight = ref(20)
const taskName = ref('N点训练')
const checkinId = ref(0)
const current = ref(0)
const holdMs = ref(0)
const nPointActive = ref(false)
const paused = ref(false)
const muted = ref(false)
const finishing = ref(false)
const faceReady = ref(false)
const faceDetected = ref(false)
const faceStatus = ref('')
const faceError = ref('')
const tongueScore = ref(0)
const camRunToken = ref(0)
const flyingFruit = ref(false)
const babyMood = ref<'waiting' | 'happy' | 'rest'>('waiting')

let timer: ReturnType<typeof setInterval> | null = null

const fruitNames = ['草莓', '苹果', '橘子', '蓝莓', '苹果']
const fruitEmoji = ['🍓', '🍎', '🍊', '🫐', '🍎']

const progressItems = computed(() =>
  Array.from({ length: TARGET }, (_, i) => ({
    done: i < current.value,
    active: i === current.value && phase.value === 'playing',
  })),
)

const holdPercent = computed(() => Math.min(100, Math.round((holdMs.value / HOLD_MS) * 100)))
const secondsLeft = computed(() => Math.max(0, Math.ceil((HOLD_MS - holdMs.value) / 1000)))
const currentLabel = computed(() => Math.min(current.value + 1, TARGET))
const currentFruit = computed(() => fruitEmoji[current.value] || '🍎')

const coachText = computed(() => {
  if (faceError.value) return faceError.value
  if (paused.value) return '已暂停，点右上角继续'
  if (!faceReady.value) return faceStatus.value || '正在启动摄像头与识别模型'
  if (!faceDetected.value) return faceStatus.value || '请把整张脸放进圆圈里'
  if (nPointActive.value) return `识别到舌尖上顶，稳住 ${secondsLeft.value}s`
  if (holdMs.value > 0) return '舌尖掉下来了，继续顶回上方小台子'
  return faceStatus.value || '张大嘴，把舌尖顶住上颚 N 点'
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
  if (phase.value === 'playing') pauseGame()
})

onUnload(() => cleanup())
onUnmounted(() => cleanup())

function cleanup() {
  stopTimer()
  stopGameBgm()
  camRunToken.value = 0
  nPointActive.value = false
}

function goBack() {
  cleanup()
  uni.navigateBack({
    fail: () => uni.switchTab({ url: '/pages/index/index' }),
  })
}

function startGame() {
  current.value = 0
  holdMs.value = 0
  nPointActive.value = false
  paused.value = false
  faceReady.value = false
  faceDetected.value = false
  faceStatus.value = '正在请求摄像头权限…'
  faceError.value = ''
  tongueScore.value = 0
  babyMood.value = 'waiting'
  phase.value = 'playing'
  setGameSoundMuted(muted.value)
  playGameSfx('start')
  startGameBgm('sky')
  camRunToken.value = typeof performance !== 'undefined' ? performance.now() : Date.now()
  startTimer()
}

function startTimer() {
  stopTimer()
  timer = setInterval(() => {
    if (phase.value !== 'playing' || paused.value || flyingFruit.value) return
    if (nPointActive.value) {
      holdMs.value = Math.min(HOLD_MS, holdMs.value + TICK_MS)
      babyMood.value = 'happy'
      if (holdMs.value >= HOLD_MS) completeFruit()
    } else if (holdMs.value > 0) {
      holdMs.value = Math.max(0, holdMs.value - DECAY_STEP)
      babyMood.value = 'rest'
    } else {
      babyMood.value = 'waiting'
    }
  }, TICK_MS)
}

function stopTimer() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

function completeFruit() {
  if (flyingFruit.value) return
  nPointActive.value = false
  flyingFruit.value = true
  babyMood.value = 'happy'
  if (!muted.value) {
    uni.vibrateShort?.({})
    playGameSfx('fruit')
  }

  setTimeout(() => {
    flyingFruit.value = false
    current.value += 1
    holdMs.value = 0
    babyMood.value = 'waiting'

    if (current.value >= TARGET) {
      stopTimer()
      stopGameBgm()
      camRunToken.value = 0
      phase.value = 'success'
      playGameSfx('success')
      uni.showToast({ icon: 'success', title: '训练成功！' })
    }
  }, 850)
}

function pauseGame() {
  paused.value = true
  nPointActive.value = false
  playGameSfx('pause')
}

function togglePause() {
  if (phase.value !== 'playing') return
  paused.value = !paused.value
  nPointActive.value = false
  playGameSfx(paused.value ? 'pause' : 'resume')
  if (!paused.value && camRunToken.value <= 0) {
    camRunToken.value = typeof performance !== 'undefined' ? performance.now() : Date.now()
  }
}

function toggleMuted() {
  muted.value = !muted.value
  setGameSoundMuted(muted.value)
  if (!muted.value && phase.value === 'playing') {
    playGameSfx('resume')
    startGameBgm('sky')
  }
}

function onCamStarted() {
  faceStatus.value = '摄像头已开启，正在加载识别模型…'
}

function onCamStatus(msg: string) {
  if (msg) faceStatus.value = msg
}

function onCamError(msg: string) {
  faceError.value = msg || '摄像头启动失败'
  faceReady.value = false
  faceDetected.value = false
  nPointActive.value = false
  playGameSfx('error')
}

function onFaceSnapshot(s: FaceLipSnapshot) {
  faceReady.value = s.ready
  faceDetected.value = s.faceDetected
  faceStatus.value = s.statusText || faceStatus.value
  tongueScore.value = Math.round((s.tongueUpScore || 0) * 100)
  faceError.value = s.error || ''
  const nextActive = phase.value === 'playing' && !paused.value && !!s.isNPoint
  if (nextActive && !nPointActive.value) playGameSfx('charge')
  nPointActive.value = nextActive
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
        description: '游戏训练完成：N点训练',
      })
      const coins = result.coins_awarded || 0
      uni.showToast({
        icon: 'success',
        title: coins > 0 ? `打卡成功 +${coins}金币` : '已记录训练',
        duration: 1600,
      })
      await new Promise((resolve) => setTimeout(resolve, 550))
    } else {
      uni.showToast({ icon: 'none', title: '未关联任务，仅本地完成' })
      await new Promise((resolve) => setTimeout(resolve, 450))
    }
  } catch (e) {
    const msg = e instanceof Error ? e.message : '提交失败'
    uni.showToast({ icon: 'none', title: msg })
    finishing.value = false
    return
  }
  finishing.value = false
  goBack()
}
</script>

<template>
  <view class="npoint-page">
    <view class="sky-cloud cloud-a" />
    <view class="sky-cloud cloud-b" />
    <view class="sun" />

    <view class="nav" :style="{ paddingTop: statusBarHeight + 'px' }">
      <view class="round-btn dark" @tap="goBack">×</view>
      <view class="nav-title">
        <text>{{ phase === 'intro' ? taskName : '爱脸动动 · 今日训练' }}</text>
        <text v-if="phase === 'playing'" class="nav-sub">
          第 {{ currentLabel }} 颗 · {{ fruitNames[current] || '苹果' }}
        </text>
      </view>
      <view class="nav-actions">
        <view class="round-btn light" @tap="toggleMuted">{{ muted ? '静' : '声' }}</view>
        <view v-if="phase === 'playing'" class="round-btn light pause" @tap="togglePause">
          {{ paused ? '▶' : 'Ⅱ' }}
        </view>
      </view>
    </view>

    <view v-if="phase === 'intro'" class="intro">
      <view class="tree">🌳</view>
      <view class="mom-bird">🐦</view>
      <view class="hero-bug">🫶</view>

      <view class="brand">爱脸动动</view>
      <view class="age">4~12岁口呼吸面容恢复</view>
      <view class="game-name">小鸟吃饱饱</view>
      <view class="title">N点训练</view>

      <view class="baby-wrap">
        <view class="baby">🐥</view>
        <view class="nest">🪹</view>
      </view>

      <view class="rule-cards">
        <view class="rule-card">
          <text class="rule-ico">👄</text>
          <text class="rule-title">张大嘴</text>
          <text class="rule-desc">脸和嘴都在圆圈里</text>
        </view>
        <view class="rule-card">
          <text class="rule-ico">👅</text>
          <text class="rule-title">舌尖上顶</text>
          <text class="rule-desc">顶住上颚 N 点 10 秒</text>
        </view>
        <view class="rule-card">
          <text class="rule-ico">🪹</text>
          <text class="rule-title">送果回窝</text>
          <text class="rule-desc">完成 5 颗果子</text>
        </view>
      </view>

      <view class="tips-panel">
        <view class="tips-title">这样做才对</view>
        <view class="tips-grid">
          <view class="tip ok">嘴张得大大的，保持稳稳的</view>
          <view class="tip no">张一下就掉，越张越小</view>
          <view class="tip ok">舌尖顶“小台子”，不碰牙</view>
          <view class="tip no">顶到牙齿上或舌尖掉下</view>
        </view>
      </view>

      <view class="start-btn" @tap="startGame">开饭啦 🍎</view>
    </view>

    <view v-else-if="phase === 'playing'" class="play">
      <view class="progress-row">
        <view class="fruit-track">
          <view
            v-for="(item, i) in progressItems"
            :key="i"
            class="fruit-dot"
            :class="{ done: item.done, active: item.active }"
          >
            {{ item.done ? fruitEmoji[i] : '●' }}
          </view>
        </view>
      </view>

      <view class="orchard">
        <view class="tree-side">🌳</view>
        <view class="mom" :class="{ fly: flyingFruit }">🐦</view>
        <view class="dash-path" />
        <view class="nest-side">🪹</view>
      </view>

      <view class="lens-wrap" :class="{ holding: nPointActive, paused }">
        <view class="timer-badge">{{ secondsLeft }}s</view>
        <FaceCam
          class="face-cam"
          :run-token="camRunToken"
          @snapshot="onFaceSnapshot"
          @error="onCamError"
          @started="onCamStarted"
          @status="onCamStatus"
        />
        <view class="lens-ring" />
        <view class="lens-glow" :style="{ opacity: 0.16 + holdPercent / 140 }" />
        <view class="face-target" :class="{ ok: nPointActive }">
          {{ nPointActive ? 'N点顶住中' : '张嘴顶 N 点' }}
        </view>
        <view class="fruit-fly" :class="{ show: flyingFruit }">{{ currentFruit }}</view>
        <view class="score-badge">舌尖 {{ tongueScore }}%</view>
      </view>

      <view class="baby-stage">
        <view class="baby" :class="babyMood">🐥</view>
        <view class="nest">🪹</view>
        <view class="baby-label">鸟宝宝 · 啾啾</view>
      </view>

      <view class="coach-card">
        <view class="avatar">🐥</view>
        <view class="coach-text">{{ coachText }}</view>
        <view class="rec" :class="{ on: nPointActive }">REC</view>
      </view>

      <view class="detect-pad" :class="{ active: nPointActive }">
        <view class="detect-fill" :style="{ width: holdPercent + '%' }" />
        <text>{{ nPointActive ? '正在累计有效 N 点' : '等待识别舌尖上顶' }}</text>
      </view>

      <view v-if="paused" class="pause-mask" @tap="togglePause">
        <text class="pause-title">已暂停</text>
        <text class="pause-sub">点一下继续训练</text>
      </view>
    </view>

    <view v-else class="success">
      <view class="success-bird">🐥</view>
      <view class="success-title">小鸟吃饱啦！</view>
      <view class="success-desc">完成 {{ TARGET }} 颗果子的 N 点训练，舌尖今天很努力。</view>
      <view class="success-fruits">
        <text v-for="i in TARGET" :key="i">🍎</text>
      </view>
      <view class="success-actions">
        <view class="s-btn ghost" :class="{ disabled: finishing }" @tap="playAgain">再练一次</view>
        <view class="s-btn primary" :class="{ disabled: finishing }" @tap="finishAndBack">
          {{ finishing ? '提交中…' : '完成打卡' }}
        </view>
      </view>
    </view>
  </view>
</template>

<style lang="scss">
.npoint-page {
  min-height: 100vh;
  position: relative;
  overflow: hidden;
  color: #5b3a2c;
  background: linear-gradient(180deg, #67cdeb 0%, #b7edf1 66%, #fff1c4 100%);
  box-sizing: border-box;
}

.sky-cloud {
  position: absolute;
  width: 150rpx;
  height: 48rpx;
  border-radius: 40rpx;
  background: rgba(255, 255, 255, 0.86);
}

.cloud-a { top: 72rpx; left: 314rpx; }
.cloud-b { top: 156rpx; right: 84rpx; transform: scale(0.72); }

.sun {
  position: absolute;
  top: 172rpx;
  left: -20rpx;
  width: 104rpx;
  height: 104rpx;
  border-radius: 50%;
  background: #f7ee8a;
  box-shadow: 0 0 0 22rpx rgba(247, 238, 138, 0.26);
}

.nav {
  position: relative;
  z-index: 5;
  display: flex;
  align-items: center;
  padding-left: 24rpx;
  padding-right: 24rpx;
  padding-bottom: 16rpx;
}

.nav-title {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  font-size: 34rpx;
  font-weight: 800;
  color: #3f342f;
}

.nav-sub {
  margin-top: 4rpx;
  font-size: 24rpx;
  color: #6f625d;
  font-weight: 700;
}

.nav-actions {
  width: 156rpx;
  display: flex;
  justify-content: flex-end;
  gap: 12rpx;
}

.round-btn {
  width: 72rpx;
  height: 72rpx;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 34rpx;
  font-weight: 800;
}

.round-btn.dark { color: #fff; background: rgba(49, 70, 67, 0.72); }
.round-btn.light { color: #2a797e; background: rgba(255, 255, 255, 0.78); }
.round-btn.pause { color: #fff; background: #ffa423; }

.intro {
  position: relative;
  z-index: 2;
  padding: 28rpx 28rpx calc(42rpx + env(safe-area-inset-bottom));
  text-align: center;
}

.tree,
.mom-bird,
.hero-bug {
  position: absolute;
  z-index: 1;
}

.tree { top: 42rpx; left: 28rpx; font-size: 102rpx; }
.mom-bird { top: 88rpx; right: 128rpx; font-size: 76rpx; }
.hero-bug { top: 210rpx; left: 50%; transform: translateX(-50%); font-size: 78rpx; }

.brand {
  margin-top: 250rpx;
  color: #fff;
  font-size: 34rpx;
  font-weight: 800;
}

.age {
  margin-top: 16rpx;
  color: rgba(255, 255, 255, 0.82);
  font-size: 26rpx;
}

.game-name {
  margin-top: 20rpx;
  color: #3da366;
  font-size: 34rpx;
  font-weight: 800;
}

.title {
  margin-top: 16rpx;
  color: #5a3728;
  font-size: 64rpx;
  font-weight: 900;
}

.baby-wrap {
  margin-top: 44rpx;
  height: 250rpx;
  position: relative;
}

.baby-wrap .baby { font-size: 142rpx; line-height: 1; }
.baby-wrap .nest { margin-top: -20rpx; font-size: 100rpx; }

.rule-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18rpx;
  margin-top: 34rpx;
}

.rule-card {
  min-height: 176rpx;
  padding: 24rpx 10rpx;
  border-radius: 28rpx;
  background: rgba(255, 255, 255, 0.84);
  box-shadow: 0 10rpx 24rpx rgba(62, 156, 164, 0.14);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.rule-ico { height: 50rpx; font-size: 44rpx; }

.rule-title {
  margin-top: 16rpx;
  font-size: 25rpx;
  line-height: 1.15;
  font-weight: 800;
  color: #5a3728;
}

.rule-desc {
  margin-top: 10rpx;
  font-size: 22rpx;
  line-height: 1.25;
  color: #8b817b;
}

.tips-panel {
  margin-top: 28rpx;
  padding: 28rpx;
  border-radius: 30rpx;
  background: rgba(255, 255, 255, 0.88);
  text-align: left;
  box-shadow: 0 10rpx 30rpx rgba(115, 159, 126, 0.14);
}

.tips-title {
  font-size: 30rpx;
  font-weight: 900;
  color: #5a3728;
}

.tips-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14rpx 16rpx;
  margin-top: 20rpx;
}

.tip {
  min-height: 52rpx;
  border-radius: 14rpx;
  padding: 12rpx 16rpx 12rpx 52rpx;
  font-size: 22rpx;
  line-height: 1.35;
  position: relative;
}

.tip::before {
  position: absolute;
  left: 16rpx;
  top: 13rpx;
  font-weight: 900;
}

.tip.ok { color: #329669; background: #e1f5e8; }
.tip.ok::before { content: '✓'; }
.tip.no { color: #a8847b; background: #fdece6; }
.tip.no::before { content: '×'; }

.start-btn {
  margin-top: 30rpx;
  height: 126rpx;
  border-radius: 64rpx;
  background: #ffab3c;
  box-shadow: inset 0 -12rpx 0 rgba(208, 96, 23, 0.32), 0 14rpx 24rpx rgba(255, 153, 52, 0.28);
  color: #fff;
  font-size: 46rpx;
  font-weight: 900;
  display: flex;
  align-items: center;
  justify-content: center;
}

.play {
  position: relative;
  z-index: 2;
  padding: 0 24rpx calc(28rpx + env(safe-area-inset-bottom));
}

.progress-row {
  display: flex;
  align-items: center;
  margin-top: 4rpx;
}

.fruit-track {
  height: 70rpx;
  min-width: 360rpx;
  padding: 0 28rpx;
  border-radius: 35rpx;
  background: rgba(255, 255, 255, 0.76);
  display: flex;
  align-items: center;
  gap: 20rpx;
}

.fruit-dot {
  width: 34rpx;
  height: 34rpx;
  border-radius: 50%;
  color: #bed4d2;
  font-size: 28rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.fruit-dot.done { color: #e34e44; font-size: 32rpx; }
.fruit-dot.active { box-shadow: 0 0 0 8rpx rgba(255, 171, 60, 0.32); }

.orchard {
  height: 168rpx;
  position: relative;
}

.tree-side,
.mom,
.nest-side {
  position: absolute;
}

.tree-side { left: 16rpx; bottom: 4rpx; font-size: 90rpx; }
.mom { left: 122rpx; bottom: 40rpx; font-size: 72rpx; transition: transform 0.8s ease; }
.mom.fly { transform: translateX(372rpx) translateY(-20rpx); }
.nest-side { right: 28rpx; bottom: 18rpx; font-size: 84rpx; }

.dash-path {
  position: absolute;
  left: 178rpx;
  right: 132rpx;
  bottom: 86rpx;
  height: 76rpx;
  border-top: 8rpx dashed rgba(255, 255, 255, 0.85);
  border-radius: 50% 50% 0 0;
}

.lens-wrap {
  position: relative;
  width: 682rpx;
  height: 682rpx;
  margin: -4rpx auto 0;
  border-radius: 50%;
  overflow: hidden;
  background: rgba(255, 244, 219, 0.72);
  box-shadow: 0 0 0 8rpx rgba(99, 209, 233, 0.9), 0 0 34rpx rgba(255, 158, 61, 0.74);
}

.lens-wrap.holding {
  box-shadow: 0 0 0 8rpx rgba(83, 213, 133, 0.95), 0 0 54rpx rgba(255, 150, 47, 0.96);
}

.face-cam {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.lens-ring,
.lens-glow {
  position: absolute;
  inset: 24rpx;
  border-radius: 50%;
  pointer-events: none;
}

.lens-ring { border: 4rpx solid rgba(255, 255, 255, 0.62); }
.lens-glow { border: 30rpx solid rgba(255, 161, 61, 0.82); }

.timer-badge {
  position: absolute;
  top: 24rpx;
  left: 50%;
  z-index: 6;
  transform: translateX(-50%);
  height: 62rpx;
  min-width: 118rpx;
  padding: 0 22rpx;
  border-radius: 32rpx;
  background: #ffad43;
  color: #fff;
  font-size: 36rpx;
  font-weight: 900;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: inset 0 -8rpx 0 rgba(211, 95, 19, 0.24);
}

.face-target,
.score-badge {
  position: absolute;
  z-index: 6;
  padding: 12rpx 24rpx;
  border-radius: 30rpx;
  color: #fff;
  font-size: 24rpx;
  font-weight: 900;
}

.face-target {
  left: 50%;
  top: 104rpx;
  transform: translateX(-50%);
  background: rgba(158, 112, 45, 0.78);
}

.face-target.ok { background: rgba(45, 169, 96, 0.86); }

.score-badge {
  right: 50rpx;
  bottom: 54rpx;
  background: rgba(63, 76, 73, 0.58);
}

.fruit-fly {
  position: absolute;
  left: 72rpx;
  top: 184rpx;
  z-index: 7;
  font-size: 42rpx;
  opacity: 0;
}

.fruit-fly.show {
  opacity: 1;
  animation: fruitToNest 0.82s ease forwards;
}

@keyframes fruitToNest {
  to {
    transform: translate(480rpx, -88rpx) scale(0.9);
    opacity: 0.1;
  }
}

.baby-stage {
  margin-top: 28rpx;
  height: 178rpx;
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.baby-stage .baby {
  font-size: 90rpx;
  line-height: 1;
  transition: transform 0.2s ease;
}

.baby-stage .baby.happy { transform: translateY(-8rpx) scale(1.08); }
.baby-stage .nest { margin-top: -16rpx; font-size: 66rpx; }

.baby-label {
  margin-top: -8rpx;
  padding: 8rpx 22rpx;
  border-radius: 22rpx;
  background: rgba(255, 255, 255, 0.82);
  font-size: 24rpx;
  color: #7a5b4d;
  font-weight: 800;
}

.coach-card {
  height: 84rpx;
  margin-top: 20rpx;
  padding: 0 22rpx;
  border-radius: 42rpx;
  background: rgba(255, 255, 255, 0.62);
  display: flex;
  align-items: center;
  gap: 18rpx;
}

.avatar {
  width: 58rpx;
  height: 58rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
}

.coach-text {
  flex: 1;
  font-size: 28rpx;
  color: #75856c;
  font-weight: 800;
}

.rec {
  min-width: 86rpx;
  height: 52rpx;
  border-radius: 28rpx;
  color: #fff;
  background: #5d6256;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22rpx;
  font-weight: 900;
}

.rec.on { background: #2fae66; }

.detect-pad {
  position: relative;
  height: 98rpx;
  margin-top: 22rpx;
  border-radius: 50rpx;
  overflow: hidden;
  background: #5d6256;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32rpx;
  font-weight: 900;
  box-shadow: inset 0 -10rpx 0 rgba(40, 48, 45, 0.28);
}

.detect-pad.active {
  background: #3fb878;
  box-shadow: inset 0 -10rpx 0 rgba(20, 116, 66, 0.28), 0 12rpx 24rpx rgba(38, 145, 92, 0.22);
}

.detect-pad text {
  position: relative;
  z-index: 2;
}

.detect-fill {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  background: rgba(255, 255, 255, 0.24);
}

.pause-mask {
  position: fixed;
  inset: 0;
  z-index: 20;
  background: rgba(61, 78, 74, 0.36);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #fff;
}

.pause-title { font-size: 54rpx; font-weight: 900; }
.pause-sub { margin-top: 14rpx; font-size: 28rpx; }

.success {
  position: relative;
  z-index: 2;
  min-height: 70vh;
  padding: 120rpx 42rpx 48rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.success-bird { font-size: 150rpx; }

.success-title {
  margin-top: 20rpx;
  font-size: 52rpx;
  font-weight: 900;
  color: #5a3728;
}

.success-desc {
  margin-top: 18rpx;
  font-size: 28rpx;
  line-height: 1.5;
  color: #786359;
}

.success-fruits {
  margin-top: 32rpx;
  display: flex;
  gap: 16rpx;
  font-size: 42rpx;
}

.success-actions {
  width: 100%;
  margin-top: 54rpx;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18rpx;
}

.s-btn {
  height: 92rpx;
  border-radius: 46rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30rpx;
  font-weight: 900;
}

.s-btn.ghost {
  background: rgba(255, 255, 255, 0.78);
  color: #5a8b78;
}

.s-btn.primary {
  color: #fff;
  background: #ff9f35;
}

.s-btn.disabled { opacity: 0.65; }
</style>
