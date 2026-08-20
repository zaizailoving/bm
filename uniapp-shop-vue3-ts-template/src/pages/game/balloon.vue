<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { onHide, onLoad, onUnload } from '@dcloudio/uni-app'
import FaceCam from '@/components/FaceCam.vue'
import { completeGameCheckinApi } from '@/services/checkin'
import type { FaceLipSnapshot } from '@/utils/faceLip'
import { playGameSfx, setGameSoundMuted, startGameBgm, stopGameBgm } from '@/utils/gameSound'

type Phase = 'intro' | 'playing' | 'success'

const TARGET = 5
const FULL = 100
const TICK_MS = 100
const BLOW_STEP = 2.6
const LEAK_STEP = 0.9

const phase = ref<Phase>('intro')
const statusBarHeight = ref(20)
const taskName = ref('吹气球')
const checkinId = ref(0)
const current = ref(0)
const balloonPower = ref(0)
const blowing = ref(false)
const paused = ref(false)
const muted = ref(false)
const finishing = ref(false)
const faceReady = ref(false)
const faceDetected = ref(false)
const faceStatus = ref('')
const faceError = ref('')
const blowScore = ref(0)
const camRunToken = ref(0)
const balloonDone = ref(false)
const mascotMood = ref<'waiting' | 'happy' | 'rest'>('waiting')

let timer: ReturnType<typeof setInterval> | null = null

const colors = ['#ff6b8a', '#ffb23f', '#6ec7ff', '#b883ff', '#66d889']

const progressItems = computed(() =>
  Array.from({ length: TARGET }, (_, i) => ({
    done: i < current.value,
    active: i === current.value && phase.value === 'playing',
    color: colors[i % colors.length],
  })),
)

const currentLabel = computed(() => Math.min(current.value + 1, TARGET))
const balloonScale = computed(() => 0.55 + balloonPower.value / 145)
const balloonColor = computed(() => colors[current.value % colors.length])

const coachText = computed(() => {
  if (faceError.value) return faceError.value
  if (paused.value) return '已暂停，点右上角继续'
  if (!faceReady.value) return faceStatus.value || '正在启动摄像头与识别模型'
  if (!faceDetected.value) return faceStatus.value || '请把整张脸放进圆圈里'
  if (blowing.value) return '识别到嘟嘴吹气，保持长吹'
  if (balloonPower.value > 0) return '气球在漏气，继续闭嘴嘟嘴吹'
  return '闭嘴嘟起，把气球压在上下嘴唇中间'
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
  blowing.value = false
}

function goBack() {
  cleanup()
  uni.navigateBack({
    fail: () => uni.switchTab({ url: '/pages/index/index' }),
  })
}

function startGame() {
  current.value = 0
  balloonPower.value = 0
  blowing.value = false
  paused.value = false
  faceReady.value = false
  faceDetected.value = false
  faceStatus.value = '正在请求摄像头权限…'
  faceError.value = ''
  blowScore.value = 0
  balloonDone.value = false
  mascotMood.value = 'waiting'
  phase.value = 'playing'
  setGameSoundMuted(muted.value)
  playGameSfx('start')
  startGameBgm('green')
  camRunToken.value = typeof performance !== 'undefined' ? performance.now() : Date.now()
  startTimer()
}

function startTimer() {
  stopTimer()
  timer = setInterval(() => {
    if (phase.value !== 'playing' || paused.value || balloonDone.value) return
    if (blowing.value) {
      balloonPower.value = Math.min(FULL, balloonPower.value + BLOW_STEP)
      mascotMood.value = 'happy'
      if (balloonPower.value >= FULL) completeBalloon()
    } else if (balloonPower.value > 0) {
      balloonPower.value = Math.max(0, balloonPower.value - LEAK_STEP)
      mascotMood.value = 'rest'
    } else {
      mascotMood.value = 'waiting'
    }
  }, TICK_MS)
}

function stopTimer() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

function completeBalloon() {
  if (balloonDone.value) return
  blowing.value = false
  balloonDone.value = true
  mascotMood.value = 'happy'
  if (!muted.value) {
    uni.vibrateShort?.({})
    playGameSfx('balloon')
  }

  setTimeout(() => {
    current.value += 1
    balloonPower.value = 0
    balloonDone.value = false
    mascotMood.value = 'waiting'

    if (current.value >= TARGET) {
      stopTimer()
      stopGameBgm()
      camRunToken.value = 0
      phase.value = 'success'
      playGameSfx('success')
      uni.showToast({ icon: 'success', title: '训练成功！' })
    }
  }, 900)
}

function pauseGame() {
  paused.value = true
  blowing.value = false
  playGameSfx('pause')
}

function togglePause() {
  if (phase.value !== 'playing') return
  paused.value = !paused.value
  blowing.value = false
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
    startGameBgm('green')
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
  blowing.value = false
  playGameSfx('error')
}

function onFaceSnapshot(s: FaceLipSnapshot) {
  faceReady.value = s.ready
  faceDetected.value = s.faceDetected
  faceStatus.value = s.statusText || faceStatus.value
  faceError.value = s.error || ''
  blowScore.value = Math.round((s.blowScore || 0) * 100)
  const nextBlowing = phase.value === 'playing' && !paused.value && !!s.isBlowing
  if (nextBlowing && !blowing.value) playGameSfx('charge')
  blowing.value = nextBlowing
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
        description: '游戏训练完成：吹气球',
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
  <view class="balloon-page">
    <view class="bubble b1" />
    <view class="bubble b2" />
    <view class="bubble b3" />

    <view class="nav" :style="{ paddingTop: statusBarHeight + 'px' }">
      <view class="round-btn dark" @tap="goBack">×</view>
      <view class="nav-title">
        <text>{{ phase === 'intro' ? taskName : '爱脸动动 · 今日训练' }}</text>
        <text v-if="phase === 'playing'" class="nav-sub">第 {{ currentLabel }} 个气球</text>
      </view>
      <view class="nav-actions">
        <view class="round-btn light" @tap="toggleMuted">{{ muted ? '静' : '声' }}</view>
        <view v-if="phase === 'playing'" class="round-btn light pause" @tap="togglePause">
          {{ paused ? '▶' : 'Ⅱ' }}
        </view>
      </view>
    </view>

    <view v-if="phase === 'intro'" class="intro">
      <view class="hero">
        <view class="lung">🫁</view>
        <view class="hero-balloon">🎈</view>
      </view>
      <view class="brand">爱脸动动</view>
      <view class="age">训练部位 · 呼吸</view>
      <view class="title">吹气球</view>
      <view class="subtitle">每天一组，连续吹五个气球</view>

      <view class="rule-cards">
        <view class="rule-card">
          <text class="rule-ico">👄</text>
          <text class="rule-title">闭嘴嘟嘴</text>
          <text class="rule-desc">嘴角向两侧咧开一点</text>
        </view>
        <view class="rule-card">
          <text class="rule-ico">🎈</text>
          <text class="rule-title">长吹气</text>
          <text class="rule-desc">识别到吹气球会变大</text>
        </view>
        <view class="rule-card">
          <text class="rule-ico">🌬️</text>
          <text class="rule-title">鼻呼吸</text>
          <text class="rule-desc">换气时闭嘴，用鼻子吸气</text>
        </view>
      </view>

      <view class="tips-panel">
        <view class="tips-title">动作要点</view>
        <view class="tips-grid">
          <view class="tip ok">气球压在上下嘴唇中间</view>
          <view class="tip ok">尽量长吹气，肩膀胸部别大幅起伏</view>
          <view class="tip ok">换气时闭嘴，用鼻子呼吸</view>
          <view class="tip no">不要张大嘴含住吹嘴</view>
        </view>
      </view>

      <view class="start-btn" @tap="startGame">开始吹气 🎈</view>
    </view>

    <view v-else-if="phase === 'playing'" class="play">
      <view class="progress-row">
        <view class="balloon-track">
          <view
            v-for="(item, i) in progressItems"
            :key="i"
            class="mini-balloon"
            :class="{ done: item.done, active: item.active }"
            :style="{ background: item.done ? item.color : '#d7e7dd' }"
          />
        </view>
      </view>

      <view class="lens-wrap" :class="{ blowing, done: balloonDone }">
        <FaceCam
          class="face-cam"
          :run-token="camRunToken"
          @snapshot="onFaceSnapshot"
          @error="onCamError"
          @started="onCamStarted"
          @status="onCamStatus"
        />
        <view class="lens-ring" />
        <view class="mouth-badge" :class="{ ok: blowing }">
          {{ blowing ? '识别到吹气' : '闭嘴嘟嘴吹' }}
        </view>
        <view class="score-badge">吹气 {{ blowScore }}%</view>
      </view>

      <view class="balloon-stage">
        <view
          class="big-balloon"
          :class="{ full: balloonDone }"
          :style="{ transform: `scale(${balloonScale})`, background: balloonColor }"
        >
          <view class="shine" />
        </view>
        <view class="balloon-string" />
        <view class="mascot" :class="mascotMood">🫁</view>
      </view>

      <view class="coach-card">
        <view class="avatar">🎈</view>
        <view class="coach-text">{{ coachText }}</view>
        <view class="rec" :class="{ on: blowing }">REC</view>
      </view>

      <view class="inflate-pad" :class="{ active: blowing }">
        <view class="inflate-fill" :style="{ width: balloonPower + '%' }" />
        <text>{{ blowing ? '气球正在变大' : '等待识别闭嘴嘟嘴吹气' }}</text>
      </view>

      <view v-if="paused" class="pause-mask" @tap="togglePause">
        <text class="pause-title">已暂停</text>
        <text class="pause-sub">点一下继续训练</text>
      </view>
    </view>

    <view v-else class="success">
      <view class="success-balloon">🎈</view>
      <view class="success-title">五个气球吹完啦！</view>
      <view class="success-desc">完成 {{ TARGET }} 个气球，今天的呼吸训练很稳。</view>
      <view class="success-balloons">
        <text v-for="i in TARGET" :key="i">🎈</text>
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
.balloon-page {
  min-height: 100vh;
  position: relative;
  overflow: hidden;
  color: #24513d;
  background: linear-gradient(180deg, #96dd48 0%, #cdf1a2 58%, #fff4c8 100%);
  box-sizing: border-box;
}

.bubble {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.35);
}

.b1 { width: 120rpx; height: 120rpx; top: 160rpx; left: 42rpx; }
.b2 { width: 80rpx; height: 80rpx; top: 282rpx; right: 80rpx; }
.b3 { width: 180rpx; height: 180rpx; bottom: 220rpx; left: -60rpx; }

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
  font-weight: 900;
  color: #1f3e31;
}

.nav-sub {
  margin-top: 4rpx;
  font-size: 24rpx;
  color: #426957;
  font-weight: 800;
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
  font-weight: 900;
}

.round-btn.dark { color: #fff; background: rgba(31, 59, 48, 0.72); }
.round-btn.light { color: #36a342; background: rgba(255, 255, 255, 0.8); }
.round-btn.pause { color: #fff; background: #ffa423; }

.intro {
  position: relative;
  z-index: 2;
  padding: 26rpx 28rpx calc(42rpx + env(safe-area-inset-bottom));
  text-align: center;
}

.hero {
  height: 230rpx;
  position: relative;
}

.lung {
  position: absolute;
  left: 190rpx;
  top: 30rpx;
  font-size: 122rpx;
}

.hero-balloon {
  position: absolute;
  right: 170rpx;
  top: 12rpx;
  font-size: 126rpx;
}

.brand {
  color: #fff;
  font-size: 34rpx;
  font-weight: 900;
}

.age {
  margin-top: 16rpx;
  color: rgba(255, 255, 255, 0.9);
  font-size: 28rpx;
  font-weight: 800;
}

.title {
  margin-top: 18rpx;
  color: #fff;
  font-size: 76rpx;
  font-weight: 900;
  text-shadow: 0 6rpx 0 rgba(54, 163, 66, 0.45);
}

.subtitle {
  margin-top: 16rpx;
  color: #255a3f;
  font-size: 28rpx;
  font-weight: 800;
}

.rule-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18rpx;
  margin-top: 46rpx;
}

.rule-card {
  min-height: 176rpx;
  padding: 24rpx 10rpx;
  border-radius: 28rpx;
  background: rgba(255, 255, 255, 0.86);
  box-shadow: 0 10rpx 24rpx rgba(62, 156, 92, 0.16);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.rule-ico { height: 50rpx; font-size: 44rpx; }
.rule-title { margin-top: 16rpx; font-size: 25rpx; line-height: 1.15; font-weight: 900; color: #24513d; }
.rule-desc { margin-top: 10rpx; font-size: 22rpx; line-height: 1.25; color: #66816f; }

.tips-panel {
  margin-top: 28rpx;
  padding: 28rpx;
  border-radius: 30rpx;
  background: rgba(255, 255, 255, 0.9);
  text-align: left;
  box-shadow: 0 10rpx 30rpx rgba(85, 155, 92, 0.16);
}

.tips-title { font-size: 30rpx; font-weight: 900; color: #24513d; }

.tips-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 14rpx;
  margin-top: 20rpx;
}

.tip {
  min-height: 52rpx;
  border-radius: 14rpx;
  padding: 12rpx 16rpx 12rpx 52rpx;
  font-size: 24rpx;
  line-height: 1.35;
  position: relative;
}

.tip::before { position: absolute; left: 16rpx; top: 13rpx; font-weight: 900; }
.tip.ok { color: #329669; background: #e1f5e8; }
.tip.ok::before { content: '✓'; }
.tip.no { color: #a8847b; background: #fdece6; }
.tip.no::before { content: '×'; }

.start-btn {
  margin-top: 30rpx;
  height: 126rpx;
  border-radius: 64rpx;
  background: #28bf49;
  box-shadow: inset 0 -12rpx 0 rgba(18, 107, 45, 0.32), 0 14rpx 24rpx rgba(57, 180, 65, 0.28);
  color: #fff;
  font-size: 44rpx;
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

.balloon-track {
  height: 70rpx;
  min-width: 360rpx;
  padding: 0 28rpx;
  border-radius: 35rpx;
  background: rgba(255, 255, 255, 0.78);
  display: flex;
  align-items: center;
  gap: 22rpx;
}

.mini-balloon {
  width: 34rpx;
  height: 42rpx;
  border-radius: 50% 50% 46% 46%;
  position: relative;
}

.mini-balloon::after {
  content: '';
  position: absolute;
  left: 12rpx;
  bottom: -8rpx;
  border-left: 5rpx solid transparent;
  border-right: 5rpx solid transparent;
  border-top: 9rpx solid currentColor;
}

.mini-balloon.done { color: inherit; }
.mini-balloon.active { box-shadow: 0 0 0 8rpx rgba(255, 255, 255, 0.62); }

.lens-wrap {
  position: relative;
  width: 650rpx;
  height: 650rpx;
  margin: 26rpx auto 0;
  border-radius: 50%;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.4);
  box-shadow: 0 0 0 8rpx rgba(255, 255, 255, 0.78), 0 0 38rpx rgba(50, 178, 73, 0.52);
}

.lens-wrap.blowing {
  box-shadow: 0 0 0 8rpx rgba(255, 255, 255, 0.9), 0 0 56rpx rgba(40, 191, 73, 0.88);
}

.lens-wrap.done {
  box-shadow: 0 0 0 8rpx rgba(255, 226, 90, 0.9), 0 0 62rpx rgba(255, 170, 46, 0.92);
}

.face-cam {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.lens-ring {
  position: absolute;
  inset: 22rpx;
  border-radius: 50%;
  border: 4rpx solid rgba(255, 255, 255, 0.66);
  pointer-events: none;
}

.mouth-badge,
.score-badge {
  position: absolute;
  z-index: 6;
  padding: 12rpx 24rpx;
  border-radius: 30rpx;
  color: #fff;
  font-size: 24rpx;
  font-weight: 900;
}

.mouth-badge {
  left: 50%;
  top: 42rpx;
  transform: translateX(-50%);
  background: rgba(48, 116, 75, 0.72);
}

.mouth-badge.ok { background: rgba(40, 191, 73, 0.9); }
.score-badge { right: 50rpx; bottom: 54rpx; background: rgba(63, 76, 73, 0.58); }

.balloon-stage {
  height: 260rpx;
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-end;
  margin-top: -6rpx;
}

.big-balloon {
  width: 128rpx;
  height: 158rpx;
  border-radius: 50% 50% 46% 46%;
  position: relative;
  transition: transform 0.12s linear, opacity 0.2s ease;
  box-shadow: inset -14rpx -18rpx 0 rgba(0, 0, 0, 0.08);
}

.big-balloon.full {
  animation: balloonFloat 0.85s ease forwards;
}

.shine {
  position: absolute;
  top: 26rpx;
  left: 30rpx;
  width: 30rpx;
  height: 46rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.48);
}

.balloon-string {
  width: 4rpx;
  height: 54rpx;
  background: rgba(55, 95, 72, 0.5);
}

.mascot {
  position: absolute;
  right: 92rpx;
  bottom: 8rpx;
  font-size: 84rpx;
  transition: transform 0.2s ease;
}

.mascot.happy { transform: translateY(-8rpx) scale(1.08); }

@keyframes balloonFloat {
  to {
    transform: translateY(-180rpx) scale(1.25);
    opacity: 0;
  }
}

.coach-card {
  height: 84rpx;
  margin-top: 18rpx;
  padding: 0 22rpx;
  border-radius: 42rpx;
  background: rgba(255, 255, 255, 0.64);
  display: flex;
  align-items: center;
  gap: 18rpx;
}

.avatar {
  width: 58rpx;
  height: 58rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.75);
  display: flex;
  align-items: center;
  justify-content: center;
}

.coach-text {
  flex: 1;
  font-size: 28rpx;
  color: #4f775e;
  font-weight: 900;
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

.rec.on { background: #28bf49; }

.inflate-pad {
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

.inflate-pad.active {
  background: #28bf49;
  box-shadow: inset 0 -10rpx 0 rgba(18, 107, 45, 0.28), 0 12rpx 24rpx rgba(38, 145, 92, 0.22);
}

.inflate-pad text {
  position: relative;
  z-index: 2;
}

.inflate-fill {
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

.success-balloon { font-size: 160rpx; }

.success-title {
  margin-top: 20rpx;
  font-size: 52rpx;
  font-weight: 900;
  color: #24513d;
}

.success-desc {
  margin-top: 18rpx;
  font-size: 28rpx;
  line-height: 1.5;
  color: #4f775e;
}

.success-balloons {
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

.s-btn.ghost { background: rgba(255, 255, 255, 0.78); color: #5a8b78; }
.s-btn.primary { color: #fff; background: #28bf49; }
.s-btn.disabled { opacity: 0.65; }
</style>
