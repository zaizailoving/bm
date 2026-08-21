<script setup lang="ts">
import { ref } from 'vue'
import { resetPasswordApi } from '@/services/auth'

const form = ref({ username: '', phone: '', password: '', confirmPassword: '' })
const loading = ref(false)

const submit = async () => {
  const data = form.value
  if (!data.username.trim() || !data.phone.trim()) return uni.showToast({ icon: 'none', title: '请填写用户名和手机号' })
  if (data.password.length < 6) return uni.showToast({ icon: 'none', title: '新密码至少6位' })
  if (data.password !== data.confirmPassword) return uni.showToast({ icon: 'none', title: '两次密码不一致' })
  if (loading.value) return
  loading.value = true
  try {
    await resetPasswordApi({ username: data.username.trim(), phone: data.phone.trim(), new_password: data.password })
    uni.showToast({ icon: 'success', title: '密码已重置' })
    setTimeout(() => uni.navigateBack(), 500)
  } finally { loading.value = false }
}
</script>

<template>
  <view class="page"><view class="card">
    <view class="title">重置密码</view>
    <view class="hint">输入注册时使用的用户名和手机号</view>
    <input v-model="form.username" class="input" placeholder="用户名" />
    <input v-model="form.phone" class="input" type="number" placeholder="注册手机号" />
    <input v-model="form.password" class="input" password placeholder="新密码（至少6位）" />
    <input v-model="form.confirmPassword" class="input" password placeholder="再次输入新密码" />
    <button class="button" :loading="loading" :disabled="loading" @tap="submit">重置密码</button>
  </view></view>
</template>

<style lang="scss" scoped>
.page { min-height: 100vh; padding: 48rpx 32rpx; box-sizing: border-box; background: #f5f6f8; }
.card { padding: 44rpx 36rpx; border-radius: 24rpx; background: #fff; }
.title { font-size: 40rpx; font-weight: 700; color: #333; }
.hint { margin: 14rpx 0 36rpx; color: #999; font-size: 25rpx; }
.input { height: 88rpx; margin-bottom: 24rpx; padding: 0 26rpx; border-radius: 14rpx; background: #f5f6f8; font-size: 28rpx; }
.button { margin-top: 20rpx; border-radius: 46rpx; background: #27ba9b; color: #fff; }
</style>
