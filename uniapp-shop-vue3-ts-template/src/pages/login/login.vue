<script setup lang="ts">
import { ref } from 'vue'
import { loginApi } from '@/services/auth'
import { useMemberStore } from '@/stores'

const memberStore = useMemberStore()

const form = ref({
  user_name: '',
  password: '',
})
const loading = ref(false)
const showPassword = ref(false)

const onLogin = async () => {
  const user_name = form.value.user_name.trim()
  const password = form.value.password
  if (!user_name) {
    uni.showToast({ icon: 'none', title: '请输入用户名' })
    return
  }
  // 用户名 mock：免密进入演示模式；其它账号仍需密码
  const isMock = user_name.toLowerCase() === 'mock'
  if (!isMock && !password) {
    uni.showToast({ icon: 'none', title: '请输入密码' })
    return
  }


  if (loading.value) return
  loading.value = true
  try {
    const data = await loginApi({ user_name, password: password || 'mock' })
    memberStore.setProfile({
      ...data,
      nickname: isMock ? 'student' : data.user_name,
    })
    uni.showToast({
      icon: 'success',
      title:  '登录成功',
    })

    setTimeout(() => {
      // 有上一页则返回，否则进「我的」
      const pages = getCurrentPages()
      if (pages.length > 1) {
        uni.navigateBack()
      } else {
        uni.switchTab({ url: '/pages/my/my' })
      }
    }, 400)
  } catch {
    // 错误提示已在 http 中处理
  } finally {
    loading.value = false
  }
}

const goBack = () => {
  const pages = getCurrentPages()
  if (pages.length > 1) {
    uni.navigateBack()
  } else {
    uni.switchTab({ url: '/pages/index/index' })
  }
}

const goRegister = () => uni.navigateTo({ url: '/pages/login/register' })
const goForgotPassword = () => uni.navigateTo({ url: '/pages/login/forgot-password' })
</script>

<template>
  <view class="login-page">
    <view class="hero">
      <view class="logo">BM</view>
      <view class="title">欢迎登录</view>
      <view class="subtitle">训练营 · 打卡 · 成长</view>
    </view>

    <view class="card">
      <view class="field">
        <text class="label">用户名</text>
        <input
          v-model="form.user_name"
          class="input"
          type="text"
          placeholder="请输入用户名"
          placeholder-class="placeholder"
          confirm-type="next"
        />
      </view>

      <view class="field">
        <text class="label">密码</text>
        <view class="pwd-row">
          <input
            v-model="form.password"
            class="input flex1"
            :password="!showPassword"
            placeholder="请输入密码"
            placeholder-class="placeholder"
            confirm-type="done"
            @confirm="onLogin"
          />
          <text class="eye" @tap="showPassword = !showPassword">
            {{ showPassword ? '隐藏' : '显示' }}
          </text>
        </view>
      </view>

      <button class="btn-login" :loading="loading" :disabled="loading" @tap="onLogin">
        登 录
      </button>

      <view class="auth-links">
        <text class="link" @tap="goRegister">注册账号</text>
        <text class="divider">|</text>
        <text class="link" @tap="goForgotPassword">忘记密码</text>
      </view>

      <view class="tips">
        <text>演示账号：mock（任意密码或不填）· 管理员：admin / 1</text>
      </view>


      <view class="link-row" @tap="goBack">
        <text class="link">暂不登录，返回</text>
      </view>
    </view>
  </view>
</template>

<style lang="scss">
.login-page {
  min-height: 100vh;
  background: linear-gradient(165deg, #1a9b7e 0%, #27ba9b 45%, #f5f6f8 45%);
  padding: 80rpx 40rpx 40rpx;
  box-sizing: border-box;
}

.hero {
  color: #fff;
  padding: 40rpx 20rpx 60rpx;

  .logo {
    width: 120rpx;
    height: 120rpx;
    border-radius: 28rpx;
    background: rgba(255, 255, 255, 0.2);
    border: 2rpx solid rgba(255, 255, 255, 0.35);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 48rpx;
    font-weight: 700;
    margin-bottom: 32rpx;
  }

  .title {
    font-size: 48rpx;
    font-weight: 700;
    letter-spacing: 2rpx;
  }

  .subtitle {
    margin-top: 12rpx;
    font-size: 26rpx;
    opacity: 0.9;
  }
}

.card {
  background: #fff;
  border-radius: 24rpx;
  padding: 48rpx 40rpx;
  box-shadow: 0 12rpx 40rpx rgba(0, 0, 0, 0.06);
}

.field {
  margin-bottom: 36rpx;

  .label {
    display: block;
    font-size: 26rpx;
    color: #666;
    margin-bottom: 16rpx;
  }

  .input {
    height: 88rpx;
    background: #f5f6f8;
    border-radius: 16rpx;
    padding: 0 28rpx;
    font-size: 30rpx;
    color: #333;
  }

  .pwd-row {
    display: flex;
    align-items: center;
    background: #f5f6f8;
    border-radius: 16rpx;
    padding-right: 24rpx;

    .input {
      background: transparent;
    }

    .flex1 {
      flex: 1;
    }

    .eye {
      font-size: 26rpx;
      color: #27ba9b;
      padding: 12rpx 8rpx;
    }
  }
}

.placeholder {
  color: #bbb;
}

.btn-login {
  margin-top: 16rpx;
  height: 92rpx;
  line-height: 92rpx;
  border-radius: 46rpx;
  background: linear-gradient(90deg, #1a9b7e, #27ba9b);
  color: #fff;
  font-size: 32rpx;
  font-weight: 600;
  border: none;

  &[disabled] {
    opacity: 0.7;
  }
}

.tips {
  margin-top: 28rpx;
  font-size: 22rpx;
  color: #999;
  line-height: 1.5;
  text-align: center;
}

.auth-links {
  margin-top: 28rpx;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 24rpx;
  font-size: 26rpx;

  .link { color: #27ba9b; }
  .divider { color: #ddd; }
}

.link-row {
  margin-top: 36rpx;
  text-align: center;

  .link {
    font-size: 26rpx;
    color: #27ba9b;
  }
}
</style>
