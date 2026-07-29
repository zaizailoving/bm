<script setup lang="ts">
import { ref, computed, onUnmounted } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { completeGameCheckinApi } from '@/services/checkin'


/** 关卡目标：点亮 5 个圆圈 */
const TARGET = 5

type Phase = 'intro' | 'playing' | 'success'

const phase = ref<Phase>('intro')
const taskName = ref('弹唇啵啵操')
const checkinId = ref(0)

/** 已轰飞数量 / 圆圈点亮数 */
const score = ref(0)
/** 能量 0–100 */
const energy = ref(0)
/** 是否正在抿唇蓄力 */
const charging = ref(false)
/** 能量是否已满，可发射 */
const readyToFire = ref(false)
/** 怪物是否在场 */
const monsterVisible = ref(true)
/** 怪物被轰飞中 */
const monsterFlying = ref(false)
/** 发射特效 */
const blasting = ref(false)
/** 提示文案 */
const tipText = ref('小炮手准备！抿住小嘴巴给能量炮充电，能量满后啵地弹一下！')
const paused = ref(false)

let chargeTimer: ReturnType<typeof setInterval> | null = null
let spawnTimer: ReturnType<typeof setTimeout> | null = null

const progressDots = computed(() =>
  Array.from({ length: TARGET }, (_, i) => ({
    lit: i < score.value,
  })),
)

const energyLabel = computed(() => {
  if (readyToFire.value) return 'MAX'
  return `${Math.round(energy.value)}%`
})

const energyFull = computed(() => energy.value >= 100)

onLoad((query) => {
  if (query?.name) {
    try {
      taskName.value = decodeURIComponent(String(query.name))
    } catch {
      taskName.value = String(query.name)
    }
  }
  if (query?.checkin_id) {
    checkinId.value = Number(query.checkin_id) || 0
  }
})

onUnmounted(() => {
  stopCharge()
  if (spawnTimer) clearTimeout(spawnTimer)
})

const goBack = () => {
  stopCharge()
  uni.navigateBack({ fail: () => uni.switchTab({ url: '/pages/index/index' }) })
}

const startGame = () => {
  score.value = 0
  energy.value = 0
  readyToFire.value = false
  charging.value = false
  monsterVisible.value = true
  monsterFlying.value = false
  blasting.value = false
  paused.value = false
  tipText.value = '小炮手准备！长按「抿唇蓄力」给能量炮充电～'
  phase.value = 'playing'
}

const togglePause = () => {
  paused.value = !paused.value
  if (paused.value) stopCharge()
}

const stopCharge = () => {
  charging.value = false
  if (chargeTimer) {
    clearInterval(chargeTimer)
    chargeTimer = null
  }
}

/** 长按开始蓄力（模拟抿唇） */
const onChargeStart = () => {
  if (phase.value !== 'playing' || paused.value || readyToFire.value || blasting.value) return
  if (!monsterVisible.value || monsterFlying.value) return
  charging.value = true
  tipText.value = '抿住小嘴巴，给能量炮充电中…'
  if (chargeTimer) clearInterval(chargeTimer)
  chargeTimer = setInterval(() => {
    if (paused.value) return
    // 约 8 秒蓄满（每 80ms +1）
    energy.value = Math.min(100, energy.value + 1.25)
    if (energy.value >= 100) {
      energy.value = 100
      readyToFire.value = true
      stopCharge()
      tipText.value = '能量满啦！啵地弹一下，发射！'
      uni.vibrateShort?.({ type: 'medium' })
    }
  }, 80)
}

const onChargeEnd = () => {
  stopCharge()
  if (!readyToFire.value && phase.value === 'playing' && !blasting.value) {
    tipText.value = energy.value > 0
      ? '继续长按「抿唇蓄力」，把能量蓄满～'
      : '小炮手准备！长按「抿唇蓄力」给能量炮充电～'
  }
}

/** 弹唇发射 */
const onFire = () => {
  if (phase.value !== 'playing' || paused.value) return
  if (!readyToFire.value || blasting.value) {
    if (!readyToFire.value) {
      uni.showToast({ icon: 'none', title: '先抿唇把能量蓄满哦' })
    }
    return
  }
  if (!monsterVisible.value || monsterFlying.value) return

  blasting.value = true
  monsterFlying.value = true
  tipText.value = '啵！张嘴怪被轰飞啦！'
  uni.vibrateShort?.({ type: 'heavy' })

  setTimeout(() => {
    monsterVisible.value = false
    monsterFlying.value = false
    blasting.value = false
    energy.value = 0
    readyToFire.value = false
    score.value = Math.min(TARGET, score.value + 1)

    if (score.value >= TARGET) {
      tipText.value = '太棒了！圆圈全部点亮，训练成功！'
      phase.value = 'success'
      return
    }

    tipText.value = `已轰飞 ${score.value}/${TARGET} 只，下一只马上出现…`
    spawnTimer = setTimeout(() => {
      monsterVisible.value = true
      tipText.value = '新的张嘴怪出现了！继续抿唇蓄力～'
    }, 900)
  }, 650)
}

const playAgain = () => {
  startGame()
}

const finishing = ref(false)

/** 完成打卡：任务勾选 + 首次奖励 5 金币 */
const finishAndBack = async () => {
  if (finishing.value) return
  finishing.value = true
  try {
    if (!checkinId.value) {
      uni.showToast({ icon: 'none', title: '缺少任务信息，请从首页进入' })
      finishing.value = false
      return
    }
    const result = await completeGameCheckinApi({
      checkin_id: checkinId.value,
      description: '游戏打卡完成（弹唇啵啵操）',
    })
    const coins = result?.coins_awarded ?? 0
    if (coins > 0) {
      uni.showToast({ icon: 'success', title: `打卡成功 +${coins}金币` })
    } else {
      uni.showToast({ icon: 'success', title: '已完成打卡' })
    }
    setTimeout(() => goBack(), 700)
  } catch {
    // toast 已在 service / http 中处理
    finishing.value = false
  }
}

</script>

<template>
  <view class="game-page">
    <!-- 自定义顶栏 -->
    <view class="nav" :style="{ paddingTop: 'calc(12rpx + env(safe-area-inset-top))' }">
      <view class="nav-btn" @tap="goBack">×</view>
      <view class="nav-title-wrap">
        <text class="nav-title">爱脸动动 · 今日训练</text>
        <text class="nav-sub">弹唇啵啵操 · 游戏打卡</text>
      </view>
      <view class="nav-right">
        <view v-if="phase === 'playing'" class="nav-btn sm" @tap="togglePause">
          {{ paused ? '▶' : 'Ⅱ' }}
        </view>
      </view>
    </view>

    <!-- ===== 介绍页 ===== -->
    <view v-if="phase === 'intro'" class="intro">
      <view class="intro-logo">💗</view>
      <text class="intro-brand">爱脸动动</text>
      <text class="intro-sub">口呼吸面容恢复 · 趣味训练</text>

      <view class="intro-art">
        <text class="art-gun">🔫</text>
        <text class="art-boom">💥</text>
        <text class="art-monster">👾</text>
      </view>

      <text class="intro-tag">能量炮 · 轰飞张嘴怪</text>
      <text class="intro-name">{{ taskName || '弹唇啵啵操' }}</text>
      <text class="intro-desc">抿唇蓄力，啵地一声把张嘴怪轰上天！</text>

      <view class="steps">
        <view class="step">
          <text class="step-ico">🤐</text>
          <text class="step-txt">抿住小嘴巴，给能量炮充电（约 8 秒）</text>
        </view>
        <view class="step">
          <text class="step-ico">⚡</text>
          <text class="step-txt">能量满格后，准备「弹唇」发射</text>
        </view>
        <view class="step">
          <text class="step-ico">💥</text>
          <text class="step-txt">「啵」地弹一下嘴唇，把张嘴怪轰飞！</text>
        </view>
      </view>

      <view class="trophy-line">🏆 点亮全部 {{ TARGET }} 个圆圈即训练成功</view>

      <view class="hint-card">
        <text class="hint-ico">💡</text>
        <text class="hint-txt">
          本游戏用「长按抿唇 + 点击弹唇」模拟动作，帮孩子坚持练习；真实动作是否标准，请以点评老师指导为准。
        </text>
      </view>

      <view class="start-btn" @tap="startGame">
        <text class="start-ico">▶</text>
        <text>开始训练</text>
      </view>
    </view>

    <!-- ===== 对战中 ===== -->
    <view v-else-if="phase === 'playing'" class="play">
      <!-- 顶部状态 -->
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
          <text>训练中</text>
        </view>
      </view>

      <!-- 怪物舞台 -->
      <view class="stage">
        <view
          v-if="monsterVisible"
          class="monster"
          :class="{ flying: monsterFlying, shake: charging }"
        >
          <text class="monster-face">👾</text>
          <view class="monster-tag">{{ monsterFlying ? '快轰我！' : '张嘴怪' }}</view>
        </view>
        <view v-else class="monster-empty">
          <text>下一只马上出现…</text>
        </view>

        <!-- 发射大按钮（能量满时） -->
        <view
          v-if="readyToFire && !blasting"
          class="fire-btn"
          @tap="onFire"
        >
          <text class="fire-bolt">⚡</text>
          <text>啵地发射！</text>
        </view>
        <view v-if="blasting" class="blast-fx">💥 啵！</view>
      </view>

      <!-- 左侧能量条 -->
      <view class="energy-rail">
        <view class="energy-bolt" :class="{ on: energyFull }">⚡</view>
        <view class="energy-track">
          <view
            class="energy-fill"
            :class="{ full: energyFull }"
            :style="{ height: energy + '%' }"
          />
        </view>
        <text class="energy-label">{{ energyLabel }}</text>
      </view>

      <!-- 底部操作 + 提示 -->
      <view class="bottom">
        <view class="tip-bubble">
          <text class="tip-main">{{ tipText }}</text>
          <view class="tip-brand">💗 爱脸动动</view>
        </view>

        <view class="controls">
          <view
            class="ctrl-btn charge"
            :class="{ active: charging, disabled: readyToFire || blasting || !monsterVisible }"
            @touchstart.prevent="onChargeStart"
            @touchend.prevent="onChargeEnd"
            @touchcancel.prevent="onChargeEnd"
            @mousedown.prevent="onChargeStart"
            @mouseup.prevent="onChargeEnd"
            @mouseleave.prevent="onChargeEnd"
          >
            <text class="ctrl-ico">🤐</text>
            <text>{{ charging ? '蓄力中…' : '长按 · 抿唇蓄力' }}</text>
          </view>
          <view
            class="ctrl-btn fire"
            :class="{ ready: readyToFire && !blasting, disabled: !readyToFire || blasting }"
            @tap="onFire"
          >
            <text class="ctrl-ico">💋</text>
            <text>弹唇发射</text>
          </view>
        </view>
      </view>

      <!-- 暂停遮罩 -->
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
  padding-left: 24rpx;
  padding-right: 24rpx;
  padding-bottom: 12rpx;
  z-index: 10;
  position: relative;
}

.nav-btn {
  width: 72rpx;
  height: 72rpx;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 40rpx;
  line-height: 1;
  flex-shrink: 0;

  &.sm {
    font-size: 28rpx;
    width: 64rpx;
    height: 64rpx;
  }
}

.nav-title-wrap {
  flex: 1;
  text-align: center;
  min-width: 0;
}

.nav-title {
  display: block;
  font-size: 30rpx;
  font-weight: 600;
}

.nav-sub {
  display: block;
  font-size: 22rpx;
  opacity: 0.75;
  margin-top: 4rpx;
}

.nav-right {
  width: 72rpx;
  display: flex;
  justify-content: flex-end;
}

/* ---- intro ---- */
.intro {
  padding: 24rpx 40rpx calc(40rpx + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
  align-items: center;
}

.intro-logo {
  font-size: 64rpx;
  margin-top: 12rpx;
}

.intro-brand {
  font-size: 36rpx;
  font-weight: 700;
  margin-top: 8rpx;
}

.intro-sub {
  font-size: 24rpx;
  opacity: 0.7;
  margin-top: 6rpx;
}

.intro-art {
  margin: 36rpx 0 20rpx;
  display: flex;
  align-items: center;
  gap: 12rpx;
  font-size: 72rpx;
}

.intro-tag {
  font-size: 26rpx;
  color: #ffd666;
  margin-bottom: 8rpx;
}

.intro-name {
  font-size: 52rpx;
  font-weight: 800;
  letter-spacing: 2rpx;
}

.intro-desc {
  margin-top: 12rpx;
  font-size: 26rpx;
  opacity: 0.85;
  text-align: center;
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
  gap: 16rpx;
  background: rgba(255, 255, 255, 0.12);
  border-radius: 20rpx;
  padding: 22rpx 24rpx;
}

.step-ico {
  font-size: 36rpx;
  flex-shrink: 0;
}

.step-txt {
  font-size: 26rpx;
  line-height: 1.4;
  flex: 1;
}

.trophy-line {
  margin-top: 28rpx;
  font-size: 24rpx;
  color: #ffd666;
}

.hint-card {
  margin-top: 24rpx;
  width: 100%;
  box-sizing: border-box;
  background: rgba(0, 0, 0, 0.22);
  border-radius: 20rpx;
  padding: 20rpx 24rpx;
  display: flex;
  gap: 12rpx;
  align-items: flex-start;
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
  height: 100rpx;
  border-radius: 999rpx;
  background: linear-gradient(90deg, #ffb347, #ff7a59);
  color: #3b1a00;
  font-size: 34rpx;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12rpx;
  box-shadow: 0 12rpx 32rpx rgba(255, 122, 89, 0.45);
}

.start-ico {
  font-size: 28rpx;
}

/* ---- play ---- */
.play {
  position: relative;
  min-height: calc(100vh - 120rpx);
  padding-bottom: calc(24rpx + env(safe-area-inset-bottom));
}

.hud {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8rpx 28rpx 0;
}

.progress-pill {
  display: flex;
  align-items: center;
  gap: 10rpx;
  background: rgba(0, 0, 0, 0.35);
  padding: 12rpx 20rpx;
  border-radius: 999rpx;
}

.dot {
  width: 18rpx;
  height: 18rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.25);
  border: 2rpx solid rgba(255, 255, 255, 0.35);

  &.lit {
    background: #ffd666;
    border-color: #fff;
    box-shadow: 0 0 12rpx rgba(255, 214, 102, 0.8);
  }

  &.big {
    width: 36rpx;
    height: 36rpx;
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
  background: rgba(0, 0, 0, 0.4);
  padding: 10rpx 20rpx;
  border-radius: 999rpx;
  font-size: 22rpx;
}

.rec-dot {
  color: #ff4d4f;
  font-size: 18rpx;
}

.stage {
  margin-top: 40rpx;
  height: 420rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
}

.monster {
  display: flex;
  flex-direction: column;
  align-items: center;
  transition: transform 0.55s ease, opacity 0.55s ease;

  &.shake {
    animation: wobble 0.4s ease-in-out infinite;
  }

  &.flying {
    transform: translateY(-280rpx) scale(0.3) rotate(25deg);
    opacity: 0;
  }
}

.monster-face {
  font-size: 160rpx;
  line-height: 1;
  filter: drop-shadow(0 12rpx 24rpx rgba(0, 0, 0, 0.35));
}

.monster-tag {
  margin-top: 8rpx;
  background: #ff9a6b;
  color: #fff;
  font-size: 24rpx;
  padding: 6rpx 20rpx;
  border-radius: 999rpx;
  font-weight: 600;
}

.monster-empty {
  opacity: 0.6;
  font-size: 26rpx;
}

.fire-btn {
  margin-top: 28rpx;
  background: linear-gradient(90deg, #ffe566, #ffc107);
  color: #2a1a6e;
  font-size: 32rpx;
  font-weight: 800;
  padding: 22rpx 48rpx;
  border-radius: 999rpx;
  display: flex;
  align-items: center;
  gap: 12rpx;
  box-shadow: 0 10rpx 28rpx rgba(255, 193, 7, 0.5);
  animation: pulse 1s ease-in-out infinite;
}

.fire-bolt {
  font-size: 32rpx;
}

.blast-fx {
  margin-top: 24rpx;
  font-size: 48rpx;
  font-weight: 800;
  color: #ffd666;
  animation: pop 0.4s ease-out;
}

.energy-rail {
  position: absolute;
  left: 28rpx;
  top: 160rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12rpx;
}

.energy-bolt {
  width: 48rpx;
  height: 48rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28rpx;

  &.on {
    background: #ffd666;
    box-shadow: 0 0 16rpx rgba(255, 214, 102, 0.9);
  }
}

.energy-track {
  width: 28rpx;
  height: 320rpx;
  border-radius: 999rpx;
  background: rgba(0, 0, 0, 0.35);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  border: 2rpx solid rgba(255, 255, 255, 0.2);
}

.energy-fill {
  width: 100%;
  background: linear-gradient(180deg, #ffe566 0%, #ff9800 55%, #ff5722 100%);
  border-radius: 999rpx;
  transition: height 0.08s linear;
  box-shadow: 0 0 16rpx rgba(255, 193, 7, 0.6);

  &.full {
    background: linear-gradient(180deg, #fff59d 0%, #ffd666 40%, #ff9800 100%);
  }
}

.energy-label {
  font-size: 22rpx;
  font-weight: 700;
  color: #ffd666;
}

.bottom {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  padding: 0 28rpx calc(28rpx + env(safe-area-inset-bottom));
}

.tip-bubble {
  background: rgba(255, 255, 255, 0.95);
  color: #333;
  border-radius: 24rpx;
  padding: 20rpx 24rpx 16rpx;
  margin-bottom: 20rpx;
  position: relative;
}

.tip-main {
  font-size: 26rpx;
  line-height: 1.5;
  display: block;
  padding-right: 120rpx;
}

.tip-brand {
  position: absolute;
  right: 16rpx;
  bottom: 12rpx;
  font-size: 20rpx;
  color: #e85a7a;
  background: #fff0f3;
  padding: 4rpx 12rpx;
  border-radius: 999rpx;
}

.controls {
  display: flex;
  gap: 16rpx;
}

.ctrl-btn {
  flex: 1;
  height: 96rpx;
  border-radius: 999rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10rpx;
  font-size: 26rpx;
  font-weight: 600;

  .ctrl-ico {
    font-size: 30rpx;
  }

  &.charge {
    background: rgba(255, 255, 255, 0.18);
    border: 2rpx solid rgba(255, 255, 255, 0.35);

    &.active {
      background: linear-gradient(90deg, #7b5cff, #9b7cff);
      border-color: transparent;
    }

    &.disabled {
      opacity: 0.4;
    }
  }

  &.fire {
    background: rgba(255, 255, 255, 0.12);
    opacity: 0.55;

    &.ready {
      opacity: 1;
      background: linear-gradient(90deg, #ffb347, #ff7eb3);
      box-shadow: 0 8rpx 24rpx rgba(255, 126, 179, 0.45);
    }

    &.disabled:not(.ready) {
      opacity: 0.45;
    }
  }
}

.pause-mask {
  position: absolute;
  inset: 0;
  background: rgba(20, 10, 50, 0.72);
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
  margin-top: 12rpx;
  font-size: 26rpx;
  opacity: 0.8;
}

/* ---- success ---- */
.success {
  padding: 80rpx 40rpx calc(40rpx + env(safe-area-inset-bottom));
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
  font-size: 48rpx;
  font-weight: 800;
}

.success-desc {
  margin-top: 16rpx;
  font-size: 28rpx;
  opacity: 0.9;
  line-height: 1.5;
}

.success-dots {
  margin-top: 40rpx;
  display: flex;
  gap: 16rpx;
}

.success-actions {
  margin-top: 60rpx;
  width: 100%;
  display: flex;
  gap: 20rpx;
}

.s-btn {
  flex: 1;
  height: 92rpx;
  border-radius: 999rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30rpx;
  font-weight: 600;

  &.ghost {
    background: rgba(255, 255, 255, 0.15);
  }

  &.primary {
    background: linear-gradient(90deg, #ffb347, #ff7a59);
    color: #3b1a00;
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
}

@keyframes wobble {
  0%,
  100% {
    transform: rotate(-3deg) scale(1);
  }
  50% {
    transform: rotate(3deg) scale(1.04);
  }
}

@keyframes pulse {
  0%,
  100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.05);
  }
}

@keyframes pop {
  0% {
    transform: scale(0.5);
    opacity: 0;
  }
  100% {
    transform: scale(1);
    opacity: 1;
  }
}
</style>
