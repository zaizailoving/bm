<script setup lang="ts">
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useMemberStore } from '@/stores'
import { getUserProfileApi } from '@/services/user'
import type { UserProfile } from '@/types/api'

const memberStore = useMemberStore()
const loading = ref(false)
const profile = ref<UserProfile | null>(null)

const isLogin = computed(() => !!memberStore.profile?.access_token)

const displayName = computed(
  () => profile.value?.nickname || memberStore.profile?.user_name || '未登录',
)

const campStatusText = computed(() => {
  const s = profile.value?.train_camp_status
  if (s === 'ongoing') return '正在参加口呼吸训练营'
  if (s === 'finished' || s === 'completed') return '训练营已结营'
  if (s === 'not_started') return '尚未加入训练营'
  return s ? `训练营状态：${s}` : '欢迎加入训练营'
})

const archiveNo = computed(() => profile.value?.archive_no || '—')

const totalCoins = computed(() => profile.value?.total_coins ?? 0)
const availableCoins = computed(() => profile.value?.available_coins ?? 0)

/** 结营报告进度：后端暂无独立字段时用展示占位，后续可接 Daily 接口 */
const reportDay = ref(17)
const reportTotal = ref(35)
const reportUnlockDay = ref(20)
const reportOpen = computed(() => reportDay.value >= reportUnlockDay.value)

const loadProfile = async () => {
  if (!isLogin.value) {
    profile.value = null
    return
  }
  loading.value = true
  try {
    profile.value = await getUserProfileApi()
    // 同步昵称到本地会话，便于其它页展示
    if (memberStore.profile && profile.value) {
      memberStore.setProfile({
        ...memberStore.profile,
        nickname: profile.value.nickname,
        avatar: profile.value.avatar,
      })
    }
  } catch {
    // http 已 toast
  } finally {
    loading.value = false
  }
}

onShow(() => {
  loadProfile()
})

const goLogin = () => {
  uni.navigateTo({ url: '/pages/login/login' })
}

const onLogout = () => {
  uni.showModal({
    title: '提示',
    content: '确定退出登录吗？',
    success: (res) => {
      if (res.confirm) {
        memberStore.clearProfile()
        profile.value = null
        uni.showToast({ icon: 'none', title: '已退出' })
      }
    },
  })
}

const onReportTap = () => {
  if (!isLogin.value) {
    goLogin()
    return
  }
  if (!reportOpen.value) {
    uni.showToast({
      icon: 'none',
      title: `第 ${reportUnlockDay.value} 天解锁`,
    })
    return
  }
  uni.showToast({ icon: 'none', title: '报告功能开发中' })
}
</script>

<template>
  <view class="page">
    <!-- 顶部紫色背景 -->
    <view class="top-bg" />

    <view class="content">
      <!-- 未登录 -->
      <view v-if="!isLogin" class="card profile-card" @tap="goLogin">
        <view class="avatar-wrap">
          <view class="avatar avatar-empty">
            <text class="avatar-emoji">💗</text>
          </view>
        </view>
        <view class="nickname">点击登录</view>
        <view class="camp-desc">登录后查看训练进度与金币</view>
        <view class="login-btn">去登录</view>
      </view>

      <!-- 已登录资料卡 -->
      <view v-else class="card profile-card">
        <view class="avatar-wrap">
          <image
            v-if="profile?.avatar"
            class="avatar-img"
            :src="profile.avatar"
            mode="aspectFill"
          />
          <view v-else class="avatar">
            <!-- 红心卡通占位 -->
            <view class="heart-mascot">
              <view class="heart-body">❤</view>
              <view class="heart-face">
                <view class="eye left" />
                <view class="eye right" />
                <view class="smile" />
              </view>
              <view class="arm left-arm" />
              <view class="arm right-arm" />
              <view class="leg left-leg" />
              <view class="leg right-leg" />
            </view>
          </view>
        </view>
        <view class="nickname">{{ displayName }}</view>
        <view class="camp-desc">{{ campStatusText }}</view>
        <view class="archive-tag">
          <text>档案编号</text>
          <text class="archive-no">{{ archiveNo }}</text>
        </view>
      </view>

      <!-- 我的金币 -->
      <view class="card coins-card">
        <view class="card-title">
          <view class="icon-coin">¥</view>
          <text class="title-text">我的金币</text>
        </view>
        <view class="coin-row">
          <text class="coin-label">累计</text>
          <view class="coin-value">
            <view class="mini-coin">¥</view>
            <text class="num">{{ totalCoins }}</text>
          </view>
        </view>
        <view class="divider" />
        <view class="coin-row">
          <text class="coin-label">可用</text>
          <view class="coin-value">
            <view class="mini-coin">¥</view>
            <text class="num">{{ availableCoins }}</text>
          </view>
        </view>
      </view>

  
      <!-- 底部吉祥物 + 文案 -->
      <view class="footer-mascots">
        <view class="mascot yellow">◆</view>
        <view class="mascot orange">●</view>
        <view class="mascot blue">U</view>
      </view>
      <view class="slogan">每天坚持 · 陪伴孩子一起变好</view>

      <!-- 退出登录 -->
      <view v-if="isLogin" class="logout-btn" @tap="onLogout">
        <text class="logout-icon">⎋</text>
        <text>退出登录</text>
      </view>
    </view>
  </view>
</template>

<style lang="scss">
$page-bg: #f3f0fa;
$purple: #7b5cff;
$purple-deep: #6a4dff;
$gold: #e8a317;

.page {
  min-height: 100vh;
  background: $page-bg;
  position: relative;
  padding-bottom: calc(40rpx + env(safe-area-inset-bottom));
}

.top-bg {
  height: 280rpx;
  background: linear-gradient(180deg, $purple-deep 0%, $purple 55%, $page-bg 100%);
  border-radius: 0 0 48rpx 48rpx;
}

.content {
  margin-top: -160rpx;
  padding: 0 32rpx;
  position: relative;
  z-index: 1;
}

.card {
  background: #fff;
  border-radius: 28rpx;
  box-shadow: 0 8rpx 32rpx rgba(90, 60, 180, 0.06);
  margin-bottom: 24rpx;
}

.profile-card {
  padding: 48rpx 32rpx 40rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.avatar-wrap {
  margin-bottom: 24rpx;
}

.avatar,
.avatar-img {
  width: 160rpx;
  height: 160rpx;
  border-radius: 50%;
  background: #f0ebff;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.avatar-empty .avatar-emoji {
  font-size: 72rpx;
}

/* 红心吉祥物（纯 CSS 简化） */
.heart-mascot {
  position: relative;
  width: 120rpx;
  height: 120rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.heart-body {
  font-size: 96rpx;
  color: #ff4d5a;
  line-height: 1;
  position: relative;
  z-index: 1;
}

.heart-face {
  position: absolute;
  top: 48rpx;
  left: 50%;
  transform: translateX(-50%);
  width: 56rpx;
  height: 28rpx;
  z-index: 2;
}

.eye {
  position: absolute;
  width: 10rpx;
  height: 10rpx;
  background: #2b2b2b;
  border-radius: 50%;
  top: 0;

  &.left {
    left: 8rpx;
  }
  &.right {
    right: 8rpx;
  }
}

.smile {
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 20rpx;
  height: 10rpx;
  border-bottom: 3rpx solid #2b2b2b;
  border-radius: 0 0 12rpx 12rpx;
}

.arm,
.leg {
  position: absolute;
  background: #ff8a94;
  border-radius: 8rpx;
  z-index: 0;
}

.arm {
  width: 8rpx;
  height: 28rpx;
  top: 58rpx;

  &.left-arm {
    left: 18rpx;
    transform: rotate(-35deg);
  }
  &.right-arm {
    right: 18rpx;
    transform: rotate(35deg);
  }
}

.leg {
  width: 8rpx;
  height: 22rpx;
  bottom: 8rpx;

  &.left-leg {
    left: 44rpx;
  }
  &.right-leg {
    right: 44rpx;
  }
}

.nickname {
  font-size: 40rpx;
  font-weight: 700;
  color: #1a1a1a;
  margin-bottom: 12rpx;
}

.camp-desc {
  font-size: 26rpx;
  color: #999;
  margin-bottom: 24rpx;
}

.archive-tag {
  display: inline-flex;
  align-items: center;
  gap: 12rpx;
  padding: 12rpx 28rpx;
  background: #f5f3fb;
  border-radius: 999rpx;
  font-size: 24rpx;
  color: #888;

  .archive-no {
    color: #555;
    font-weight: 600;
  }
}

.login-btn {
  margin-top: 8rpx;
  padding: 16rpx 48rpx;
  background: linear-gradient(90deg, $purple-deep, $purple);
  color: #fff;
  border-radius: 999rpx;
  font-size: 28rpx;
}

/* 金币卡 */
.coins-card {
  padding: 32rpx 36rpx;
}

.card-title {
  display: flex;
  align-items: center;
  margin-bottom: 28rpx;

  .title-text {
    font-size: 30rpx;
    font-weight: 600;
    color: #222;
  }
}

.icon-coin {
  width: 44rpx;
  height: 44rpx;
  border-radius: 50%;
  background: linear-gradient(145deg, #ffd76a, $gold);
  color: #fff;
  font-size: 22rpx;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 14rpx;
  box-shadow: 0 4rpx 8rpx rgba(232, 163, 23, 0.35);
}

.icon-clip {
  margin-right: 12rpx;
  font-size: 32rpx;
}

.coin-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12rpx 0;
}

.coin-label {
  font-size: 28rpx;
  color: #666;
}

.coin-value {
  display: flex;
  align-items: center;
  gap: 10rpx;

  .num {
    font-size: 40rpx;
    font-weight: 700;
    color: $gold;
  }
}

.mini-coin {
  width: 36rpx;
  height: 36rpx;
  border-radius: 50%;
  background: linear-gradient(145deg, #ffd76a, $gold);
  color: #fff;
  font-size: 18rpx;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
}

.divider {
  height: 1rpx;
  background: #f0f0f0;
  margin: 8rpx 0;
}

/* 报告卡 */
.report-card {
  padding: 32rpx 36rpx;
}

.report-head {
  display: flex;
  align-items: center;
  justify-content: space-between;

  .card-title {
    margin-bottom: 0;
  }
}

.status-tag {
  font-size: 22rpx;
  color: #999;
  background: #f2f2f2;
  padding: 8rpx 20rpx;
  border-radius: 999rpx;

  &.open {
    color: $purple;
    background: #f0ebff;
  }
}

.report-desc {
  margin-top: 20rpx;
  font-size: 26rpx;
  color: #aaa;
}

/* 底部装饰 */
.footer-mascots {
  display: flex;
  justify-content: center;
  align-items: flex-end;
  gap: 48rpx;
  margin: 48rpx 0 16rpx;
}

.mascot {
  width: 72rpx;
  height: 88rpx;
  border-radius: 36rpx 36rpx 20rpx 20rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.85);
  font-size: 36rpx;
  font-weight: 700;
  box-shadow: 0 8rpx 16rpx rgba(0, 0, 0, 0.08);

  &.yellow {
    background: linear-gradient(180deg, #ffd666, #f5b942);
  }
  &.orange {
    background: linear-gradient(180deg, #ff9a5a, #f2783a);
  }
  &.blue {
    background: linear-gradient(180deg, #6b8cff, #4a6ef0);
  }
}

.slogan {
  text-align: center;
  font-size: 24rpx;
  color: #b0a8c4;
  margin-bottom: 36rpx;
}

.logout-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12rpx;
  height: 96rpx;
  background: #fff;
  border-radius: 999rpx;
  font-size: 30rpx;
  color: #555;
  box-shadow: 0 8rpx 24rpx rgba(90, 60, 180, 0.05);

  .logout-icon {
    font-size: 32rpx;
    color: #888;
  }
}
</style>
