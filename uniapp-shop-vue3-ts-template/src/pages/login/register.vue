<script setup lang="ts">
import { ref } from 'vue'
import { registerApi } from '@/services/auth'

const form = ref({ username: '', password: '', confirmPassword: '' })
const loading = ref(false)

const submit = async () => {
  const data = form.value
  if (data.username.trim().length < 3) return uni.showToast({ icon: 'none', title: '用户名至少3位' })
  if (data.password.length < 6) return uni.showToast({ icon: 'none', title: '密码至少6位' })
  if (data.password !== data.confirmPassword) return uni.showToast({ icon: 'none', title: '两次密码不一致' })
  if (loading.value) return
  loading.value = true
  try {
    await registerApi({
      username: data.username.trim(), password: data.password, role: 'student',
    })
    uni.showToast({ icon: 'success', title: '注册成功' })
    setTimeout(() => uni.navigateBack(), 500)
  } finally { loading.value = false }
}
</script>

<template>
  <view class="page"><view class="card">
    <view class="title">创建账号</view>
    <input v-model="form.username" class="input" placeholder="用户名（至少3位）" />
    <input v-model="form.password" class="input" password placeholder="密码（至少6位）" />
    <input v-model="form.confirmPassword" class="input" password placeholder="再次输入密码" />
    <button class="button" :loading="loading" :disabled="loading" @tap="submit">注册</button>
  </view></view>
</template>

<style lang="scss" scoped>
.page { min-height: 100vh; padding: 48rpx 32rpx; box-sizing: border-box; background: #f5f6f8; }
.card { padding: 44rpx 36rpx; border-radius: 24rpx; background: #fff; }
.title { margin-bottom: 36rpx; font-size: 40rpx; font-weight: 700; color: #333; }
.input { height: 88rpx; margin-bottom: 24rpx; padding: 0 26rpx; border-radius: 14rpx; background: #f5f6f8; font-size: 28rpx; }
.button { margin-top: 20rpx; border-radius: 46rpx; background: #27ba9b; color: #fff; }
</style>
