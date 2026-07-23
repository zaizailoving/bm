<script setup lang="ts">
import { ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { buildLocalVideoUrl } from '@/utils/teachVideo'

const src = ref('')
const title = ref('教学视频')
const errorMsg = ref('')
const loading = ref(true)

function safeDecode(v: unknown): string {
  const s = String(v ?? '')
  if (!s) return ''
  try {
    return decodeURIComponent(s)
  } catch {
    return s
  }
}

onLoad((query) => {
  title.value = safeDecode(query?.title) || '教学视频'
  if (title.value) {
    uni.setNavigationBarTitle({ title: title.value })
  }

  // 优先 file（短参数），兼容旧的 src 完整地址
  const file = safeDecode(query?.file)
  const rawSrc = safeDecode(query?.src)

  if (file) {
    src.value = /^https?:\/\//i.test(file) ? file : buildLocalVideoUrl(file)
  } else if (rawSrc) {
    if (/^https?:\/\//i.test(rawSrc)) {
      src.value = rawSrc
    } else if (rawSrc.startsWith('/static/')) {
      src.value = buildLocalVideoUrl(rawSrc)
    } else {
      const last = rawSrc.includes('/') ? rawSrc.split('/').pop() || rawSrc : rawSrc
      src.value = buildLocalVideoUrl(last)
    }
  }

  if (!src.value) {
    loading.value = false
    errorMsg.value = '暂无视频'
  } else {
    console.log('[video/play] src =', src.value)
  }
})

const onPlay = () => {
  loading.value = false
  errorMsg.value = ''
}

const onError = (e: unknown) => {
  loading.value = false
  const detail = (e as { detail?: { errMsg?: string } })?.detail
  const msg = detail?.errMsg || '视频加载失败'
  errorMsg.value = msg
  console.error('[video/play] error', src.value, e)
  uni.showToast({ icon: 'none', title: '视频无法播放', duration: 2500 })
}

const onLoadedMeta = () => {
  loading.value = false
}
</script>

<template>
  <view class="page">
    <video
      v-if="src"
      id="teachVideo"
      class="player"
      :src="src"
      controls
      autoplay
      :show-center-play-btn="true"
      :enable-play-gesture="true"
      :show-fullscreen-btn="true"
      :show-play-btn="true"
      :show-progress="true"
      object-fit="contain"
      playsinline
      webkit-playsinline
      x5-playsinline
      x5-video-player-type="h5"
      x5-video-player-fullscreen="true"
      @play="onPlay"
      @error="onError"
      @loadedmetadata="onLoadedMeta"
    />
    <view v-if="loading && src && !errorMsg" class="tip">加载中...</view>
    <view v-if="errorMsg" class="tip">
      <text>{{ errorMsg }}</text>
      <text v-if="src" class="src">{{ src }}</text>
    </view>
    <view v-if="!src" class="tip">暂无视频</view>
  </view>
</template>

<style lang="scss">
.page {
  min-height: 100vh;
  width: 100%;
  background: #000;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
  position: relative;
}

.player {
  width: 100%;
  height: 100vh;
  max-height: 100vh;
  background: #000;
}

.tip {
  position: absolute;
  left: 32rpx;
  right: 32rpx;
  bottom: 80rpx;
  color: #ccc;
  font-size: 26rpx;
  text-align: center;
  line-height: 1.5;
  word-break: break-all;

  .src {
    display: block;
    margin-top: 12rpx;
    font-size: 20rpx;
    color: #666;
  }
}
</style>
