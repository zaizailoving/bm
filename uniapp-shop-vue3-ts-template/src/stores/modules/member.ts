import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MemberProfile } from '@/types/api'

// 定义 Store
export const useMemberStore = defineStore(
  'member',
  () => {
    // 会员 / 登录会话信息
    const profile = ref<MemberProfile>()

    // 保存会员信息，登录时使用
    const setProfile = (val: MemberProfile) => {
      profile.value = val
    }

    // 清理会员信息，退出时使用
    const clearProfile = () => {
      profile.value = undefined
    }

    const isLoggedIn = () => !!profile.value?.access_token

    return {
      profile,
      setProfile,
      clearProfile,
      isLoggedIn,
    }
  },
  // 持久化（H5 / App 使用 localStorage；小程序使用 storage）
  {
    persist: {
      storage: {
        getItem(key) {
          return uni.getStorageSync(key)
        },
        setItem(key, value) {
          uni.setStorageSync(key, value)
        },
      },
    },
  },
)
