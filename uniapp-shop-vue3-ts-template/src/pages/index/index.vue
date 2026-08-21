<script setup lang="ts">
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { useMemberStore } from '@/stores'
import { getDailyTodayApi, submitDailyApi } from '@/services/daily'
import { uploadCheckinApi } from '@/services/checkin'
import { getUserProfileApi } from '@/services/user'
import { resolveTeachVideoFile, hasTeachVideo } from '@/utils/teachVideo'

import type { DailyToday, DailyTaskItem } from '@/types/api'

const memberStore = useMemberStore()
const loading = ref(false)
const plan = ref<DailyToday | null>(null)
const coins = ref(0)

/** 上传弹窗 */
const showUpload = ref(false)
const uploadTask = ref<DailyTaskItem | null>(null)
const uploadDesc = ref('')
const videoPath = ref('')
/** 本次新选的本地图片临时路径 */
const imagePaths = ref<string[]>([])
/** 服务端已有图片（只读展示） */
const existingImages = ref<string[]>([])
const existingVideo = ref('')
const saving = ref(false)
const MAX_IMAGES = 9
const MAX_DESC = 200


const isLogin = computed(() => !!memberStore.profile?.access_token)

const nickname = computed(
  () => memberStore.profile?.nickname || memberStore.profile?.user_name || '小朋友',
)

const weekNo = computed(() => plan.value?.week_no ?? 1)
const dayNo = computed(() => plan.value?.day_no ?? 1)

/** 营期进度：用相对天数估算（第 week 周 day 天 → 总第几天），默认 20 天结营 */
const campTotalDays = 20
const currentCampDay = computed(() => {
  const w = weekNo.value || 1
  const d = dayNo.value || 1
  return Math.min(Math.max((w - 1) * 7 + d, 1), campTotalDays)
})
const progressPercent = computed(() =>
  Math.min(100, Math.round((currentCampDay.value / campTotalDays) * 100)),
)

const tasks = computed(() => plan.value?.tasks || [])

const uploadedCount = computed(
  () => tasks.value.filter((t) => t.status === 'uploaded' || t.status === 'submitted').length,
)

const allDone = computed(
  () => tasks.value.length > 0 && tasks.value.every((t) => t.status !== 'unfinished'),
)

const canSubmit = computed(
  () =>
    isLogin.value &&
    plan.value?.status === 'draft' &&
    allDone.value,
)

const submitHint = computed(() => {
  if (!isLogin.value) return '请先登录'
  // if (!plan.value) return '暂无训练计划'
  // if (plan.value.status === 'submitted' || plan.value.status === 'commented') {
  //   return '今日已提交'
  // }
  // if (tasks.value.length === 0) return '今日暂无任务'
  // if (!allDone.value) {
  //   return `未上传任何动作（${uploadedCount.value}/${tasks.value.length}）`
  // }
  // return '一键提交今日打卡'
})

/** 任务展示用图标底色轮换 */
const iconColors = ['#FFE8E8', '#FFF0E0', '#F0EBFF', '#E8F4FF', '#E8FFF0', '#FFF8E0']
const iconFor = (task: DailyTaskItem, index: number) => {
  if (task.icon_url) return { type: 'img' as const, src: task.icon_url }
  // 按名称给简单 emoji 占位
  const n = task.task_name || ''
  let emoji = '💗'
  if (n.includes('捏鼻')) emoji = '👃'
  else if (n.includes('N点') || n.includes('n点')) emoji = '🥕'
  else if (n.includes('腹式') || n.includes('呼吸')) emoji = '🌬️'
  else if (n.includes('气球')) emoji = '🎈'
  else if (n.includes('舌板')) emoji = '👅'
  else if (n.includes('弹唇') || n.includes('抿唇') || n.includes('口贴') || n.includes('纽扣'))
    emoji = '❤️'
  else if (n.includes('吹水') || n.includes('啊咿')) emoji = '💧'
  return { type: 'emoji' as const, emoji, bg: iconColors[index % iconColors.length] }

}

const loadData = async () => {
  if (!isLogin.value) {
    plan.value = null
    coins.value = 0
    return
  }
  loading.value = true
  try {
    const [today, profile] = await Promise.all([
      getDailyTodayApi(),
      getUserProfileApi().catch(() => null),
    ])
    plan.value = today
    if (profile) {
      coins.value = profile.available_coins ?? profile.total_coins ?? 0
      if (memberStore.profile) {
        memberStore.setProfile({
          ...memberStore.profile,
          nickname: profile.nickname,
          avatar: profile.avatar,
        })
      }
    }
  } catch {
    // http 已 toast
  } finally {
    loading.value = false
  }
}

onShow(() => {
  loadData()
})

const goLogin = () => {
  uni.navigateTo({ url: '/pages/login/login' })
}

const onWatchVideo = (task: DailyTaskItem) => {
  // 只传文件名，播放页再拼绝对 URL，避免中文路径双重编码 / query 过长导致黑屏
  const file = resolveTeachVideoFile(task.task_name, task.teach_video_url)
  if (!file) {
    uni.showToast({ icon: 'none', title: '暂无教学视频' })
    return
  }
  uni.navigateTo({
    url: `/pages/video/play?file=${encodeURIComponent(file)}&title=${encodeURIComponent(
      task.task_name || '教学视频',
    )}`,
  })
}


/** 是否「弹唇啵啵操」类任务（可进游戏打卡） */
const isBoboTask = (task: DailyTaskItem) => {
  const n = (task.task_name || '').replace(/\s/g, '')
  return n.includes('弹唇') || n.includes('啵啵') || n.includes('波波')
}

/** 是否「N点训练」任务（可进游戏打卡） */
const isNPointTask = (task: DailyTaskItem) => {
  const n = (task.task_name || '').replace(/\s/g, '')
  return n.includes('N点') || n.includes('n点')
}

/** 是否「吹气球」任务（可进游戏打卡） */
const isBalloonTask = (task: DailyTaskItem) => {
  const n = (task.task_name || '').replace(/\s/g, '')
  return n.includes('吹气球') || n.includes('气球')
}

const isGameTask = (task: DailyTaskItem) =>
  isBoboTask(task) || isNPointTask(task) || isBalloonTask(task)

const onGameCheckin = (task: DailyTaskItem) => {
  if (!isLogin.value) {
    goLogin()
    return
  }
  const page = isNPointTask(task) ? 'npoint' : isBalloonTask(task) ? 'balloon' : 'bobo'
  const fallbackName = isNPointTask(task)
    ? 'N点训练'
    : isBalloonTask(task)
      ? '吹气球'
      : '弹唇啵啵操'
  uni.navigateTo({
    url: `/pages/game/${page}?checkin_id=${encodeURIComponent(String(task.checkin_id))}&name=${encodeURIComponent(
      task.task_name || fallbackName,
    )}`,
  })
}

const onUpload = (task: DailyTaskItem) => {
  if (!isLogin.value) {
    goLogin()
    return
  }
  if (plan.value?.status === 'submitted' || plan.value?.status === 'commented') {
    uni.showToast({ icon: 'none', title: '今日已提交，不可再改' })
    return
  }
  if (task.status === 'submitted') {
    uni.showToast({ icon: 'none', title: '该动作已提交，不可再改' })
    return
  }
  openUploadModal(task)
}


/** 过滤游戏占位媒体，避免首页展示无效图片 */
const realMediaUrls = (urls?: string[] | null) =>
  (urls || []).filter((u) => u && !String(u).startsWith('game://'))

const openUploadModal = (task: DailyTaskItem) => {
  uploadTask.value = task
  uploadDesc.value = task.description || ''
  existingVideo.value = task.video_url || ''
  existingImages.value = realMediaUrls(task.image_urls)
  videoPath.value = ''
  imagePaths.value = []
  showUpload.value = true
}


const closeUploadModal = () => {
  if (saving.value) return
  showUpload.value = false
  uploadTask.value = null
}

const uploadSubtitle = computed(() => {
  const t = uploadTask.value
  if (!t) return ''
  const req = (t.requirement || '').trim()
  return req ? `${t.task_name} · ${req}` : t.task_name
})

const descCount = computed(() => uploadDesc.value.length)

const chooseVideo = () => {
  uni.chooseVideo({
    sourceType: ['album', 'camera'],
    compressed: true,
    maxDuration: 60,
    success: (res) => {
      videoPath.value = res.tempFilePath
      existingVideo.value = ''
    },
  })
}

const clearVideo = () => {
  videoPath.value = ''
  existingVideo.value = ''
}

const chooseImages = () => {
  const remain = MAX_IMAGES - existingImages.value.length - imagePaths.value.length
  if (remain <= 0) {
    uni.showToast({ icon: 'none', title: `最多上传 ${MAX_IMAGES} 张图片` })
    return
  }
  uni.chooseImage({
    count: remain,
    sizeType: ['compressed'],
    sourceType: ['album', 'camera'],
    success: (res) => {
      const list = res.tempFilePaths || []
      imagePaths.value = [...imagePaths.value, ...list].slice(
        0,
        MAX_IMAGES - existingImages.value.length,
      )
    },
  })
}

const removeLocalImage = (idx: number) => {
  imagePaths.value = imagePaths.value.filter((_, i) => i !== idx)
}

const hasUploadMedia = computed(() => {
  const hasNewVideo = !!videoPath.value
  const hasNewImages = imagePaths.value.length > 0
  const hasExistingMedia =
    !!existingVideo.value ||
    existingImages.value.length > 0 ||
    !!(
      uploadTask.value?.video_url ||
      (uploadTask.value?.image_urls && uploadTask.value.image_urls.length)
    )
  return hasNewVideo || hasNewImages || hasExistingMedia
})

const canSaveUpload = computed(() => hasUploadMedia.value && !saving.value)

const onSaveUpload = async () => {
  const task = uploadTask.value
  if (!task || saving.value) return

  const desc = uploadDesc.value.trim()
  const hasNewVideo = !!videoPath.value
  const hasNewImages = imagePaths.value.length > 0
  const hasExistingMedia =
    !!existingVideo.value ||
    existingImages.value.length > 0 ||
    !!(task.video_url || (task.image_urls && task.image_urls.length))

  // 至少要有一个图片或视频（新选或已有）
  if (!hasNewVideo && !hasNewImages && !hasExistingMedia) {
    uni.showToast({ icon: 'none', title: '请至少上传一张图片或一个视频' })
    return
  }

  const prevDesc = (task.description || '').trim()
  const descChanged = desc !== prevDesc
  // 无新文件、描述也没改：直接关闭
  if (!hasNewVideo && !hasNewImages && !descChanged) {
    showUpload.value = false
    uploadTask.value = null
    return
  }

  if (desc.length > MAX_DESC) {
    uni.showToast({ icon: 'none', title: `描述最多 ${MAX_DESC} 字` })
    return
  }

  saving.value = true
  uni.showLoading({ title: '保存中...', mask: true })
  try {
    const result = await uploadCheckinApi({
      checkin_id: task.checkin_id,
      description: desc,
      videoPath: videoPath.value || undefined,
      imagePaths: imagePaths.value,
      hasExistingMedia,
    })
    if (typeof result.available_coins === 'number' && result.available_coins >= 0) {
      coins.value = result.available_coins
    }
    const awarded = result.coins_awarded || 0
    if (awarded > 0) {
      uni.showToast({ icon: 'none', title: `保存成功，+${awarded} 金币` })
    } else {
      uni.showToast({ icon: 'success', title: '保存成功' })
    }
    showUpload.value = false
    uploadTask.value = null
    await loadData()
  } catch {
    // toast 已在 service / http 中处理
  } finally {
    saving.value = false
    uni.hideLoading()
  }
}




const onSubmit = async () => {
  if (!canSubmit.value || !plan.value) {
    uni.showToast({ icon: 'none', title: submitHint.value })
    return
  }
  try {
    await submitDailyApi(plan.value.plan_date)
    uni.showToast({ icon: 'success', title: '提交成功' })
    await loadData()
  } catch {
    // toast 已处理
  }
}

const onSwitchPlan = () => {
  uni.showToast({ icon: 'none', title: '感冒方案切换开发中' })
}
</script>

<template>
  <view class="page">
    <!-- 顶部紫色区域 -->
    <view class="hero">
      <view class="hero-row">
        <view class="week-label">WEEK {{ weekNo }} · DAY {{ dayNo }}</view>
        <view class="hero-badge">
          <text class="badge-dots">●●●</text>
        </view>
      </view>
      <view class="greet">{{ nickname }}, 今天加油!</view>

      <!-- 进度条 D1 → D20 -->
      <view class="track-card">
        <view class="track-line">
          <view class="track-fill" :style="{ width: progressPercent + '%' }" />
          <view class="node start" :class="{ active: currentCampDay >= 1 }">
            <view class="node-icon rocket">🚀</view>
            <text class="node-tag">D1</text>
            <text class="node-label">开营</text>
          </view>
          <view
            class="node end"
            :class="{ active: currentCampDay >= campTotalDays }"
            :style="{ left: '100%' }"
          >
            <view class="node-icon trophy">🏆</view>
            <text class="node-tag">D{{ campTotalDays }}</text>
            <text class="node-label">结营</text>
          </view>
        </view>
      </view>

      <view class="hero-actions">
        <view class="coin-pill">
          <text class="coin-yen">¥</text>
          <text class="coin-num">{{ coins }}</text>
        </view>
        <view class="switch-btn" @tap="onSwitchPlan">
          <text>🌤 切换感冒方案</text>
        </view>
      </view>
    </view>

    <!-- 未登录 -->
    <view v-if="!isLogin" class="login-tip card" @tap="goLogin">
      <text>登录后查看今日训练任务</text>
      <view class="login-link">去登录</view>
    </view>

    <!-- 任务列表 -->
    <view v-else class="task-list">
      <view v-if="loading && !plan" class="empty-tip">加载中...</view>
      <view v-else-if="!tasks.length" class="empty-tip">今日暂无训练任务</view>

      <view v-for="(task, index) in tasks" :key="task.checkin_id" class="task-card card">
        <view class="task-main">
          <view
            class="task-icon"
            :style="
              iconFor(task, index).type === 'emoji'
                ? { background: iconFor(task, index).bg }
                : {}
            "
          >
            <image
              v-if="iconFor(task, index).type === 'img'"
              class="task-icon-img"
              :src="iconFor(task, index).src"
              mode="aspectFill"
            />
            <text v-else class="task-emoji">{{ iconFor(task, index).emoji }}</text>
          </view>
          <view class="task-info">
            <view class="task-name-row">
              <text class="task-name">{{ task.task_name }}</text>
              <view
                v-if="task.status === 'uploaded' || task.status === 'submitted'"
                class="done-badge"
              >
                <text class="done-check">✓</text>
              </view>
            </view>
            <view class="task-req">{{ task.requirement || '按要求完成训练' }}</view>
          </view>

        </view>
        <view class="task-actions" :class="{ 'three-cols': isGameTask(task) }">
          <view
            class="btn ghost"
            :class="{ disabled: !hasTeachVideo(task.task_name, task.teach_video_url) }"
            @tap="onWatchVideo(task)"
          >
            <text class="play-ico">▶</text>
            看教学视频
          </view>
          <view
            v-if="isGameTask(task)"
            class="btn game"
            @tap="onGameCheckin(task)"
          >
            🎮 游戏打卡
          </view>
          <view class="btn primary" @tap="onUpload(task)">
            上传内容 →
          </view>
        </view>
      </view>


      <view v-if="tasks.length" class="list-end">— 动作就这些啦 —</view>
    </view>

    <!-- 底部提交条 -->
    <view class="submit-bar">
      <view
        class="submit-btn"
        :class="{ ready: canSubmit, disabled: !canSubmit }"
        @tap="onSubmit"
      >
        <text class="submit-ico">✈</text>
        <text>{{ submitHint }}</text>
      </view>
    </view>

    <!-- 上传打卡内容弹窗 -->
    <view v-if="showUpload" class="upload-mask" @tap="closeUploadModal">
      <view class="upload-sheet" @tap.stop>
        <view class="upload-head">
          <view class="upload-title-wrap">
            <text class="upload-title">上传打卡内容</text>
            <text class="upload-sub">{{ uploadSubtitle }}</text>
          </view>
          <view class="upload-close" @tap="closeUploadModal">×</view>
        </view>

        <!-- 内容区可滚动；保存按钮紧跟文字描述下方 -->
        <scroll-view class="upload-body" scroll-y :show-scrollbar="false">
          <!-- 视频 -->
          <view class="upload-section">
            <text class="sec-label">视频</text>
            <text class="sec-hint">每个动作只用传一个视频,传练习的最后 30 秒视频</text>
            <view class="media-row">
              <view
                v-if="!videoPath && !existingVideo"
                class="media-add"
                @tap="chooseVideo"
              >
                <text class="media-ico">📹</text>
                <text class="media-add-text">点击上传</text>
              </view>
              <view v-else class="media-preview video-preview">
                <video
                  v-if="videoPath"
                  class="preview-video"
                  :src="videoPath"
                  object-fit="cover"
                  :controls="false"
                  :show-center-play-btn="false"
                />
                <view v-else class="video-placeholder">
                  <text class="media-ico">▶</text>
                  <text class="media-add-text">已上传视频</text>
                </view>
                <view class="media-del" @tap="clearVideo">×</view>
                <view class="media-rechoose" @tap="chooseVideo">重选</view>
              </view>
            </view>
          </view>

          <!-- 图片 -->
          <view class="upload-section">
            <text class="sec-label">图片</text>
            <view class="media-row images-row">
              <view
                v-for="(url, idx) in existingImages"
                :key="'e' + idx"
                class="media-preview img-preview"
              >
                <image class="preview-img" :src="url" mode="aspectFill" />
              </view>
              <view
                v-for="(path, idx) in imagePaths"
                :key="'l' + idx"
                class="media-preview img-preview"
              >
                <image class="preview-img" :src="path" mode="aspectFill" />
                <view class="media-del" @tap="removeLocalImage(idx)">×</view>
              </view>
              <view
                v-if="existingImages.length + imagePaths.length < MAX_IMAGES"
                class="media-add"
                @tap="chooseImages"
              >
                <text class="media-ico upload-arrow">↑</text>
                <text class="media-add-text">点击上传</text>
              </view>
            </view>
          </view>

          <!-- 文字描述 -->
          <view class="upload-section desc-section">
            <view class="sec-label-row">
              <text class="sec-label">文字描述</text>
              <text class="sec-count">{{ descCount }} / {{ MAX_DESC }}</text>
            </view>
            <textarea
              class="desc-input"
              v-model="uploadDesc"
              :maxlength="MAX_DESC"
              placeholder="今天的训练感受、遇到的问题…"
              placeholder-class="desc-ph"
              :auto-height="false"
            />
          </view>

          <view class="upload-tip">
            <text class="tip-ico">💡</text>
            <text class="tip-text">
              请至少上传一张图片或一个视频后点「保存」；首次保存可获得 5 金币。
            </text>
          </view>

          <!-- 保存按钮：紧挨文字描述/提示下方，避免被 tabBar 遮挡 -->
          <view class="upload-footer">
            <view class="footer-btn cancel" @tap="closeUploadModal">取消</view>
            <view
              class="footer-btn save"
              :class="{ disabled: !canSaveUpload, ready: canSaveUpload }"
              @tap="onSaveUpload"
            >
              {{ saving ? '保存中…' : '保存' }}
            </view>
          </view>
        </scroll-view>
      </view>
    </view>



  </view>
</template>


<style lang="scss">
$purple: #7b5cff;
$purple-deep: #6a4dff;
$purple-soft: #9b7cff;
$page-bg: #f0eef6;
$gold: #e8a317;

.page {
  min-height: 100vh;
  background: $page-bg;
  padding-bottom: calc(160rpx + env(safe-area-inset-bottom));
}

.hero {
  background: linear-gradient(165deg, $purple-deep 0%, $purple 48%, #8f7cff 100%);
  padding: 32rpx 32rpx 40rpx;
  border-radius: 0 0 40rpx 40rpx;
  color: #fff;
}

.hero-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.week-label {
  font-size: 22rpx;
  letter-spacing: 2rpx;
  opacity: 0.85;
  font-weight: 500;
}

.hero-badge {
  width: 72rpx;
  height: 72rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.25);
  display: flex;
  align-items: center;
  justify-content: center;

  .badge-dots {
    font-size: 18rpx;
    letter-spacing: 2rpx;
    color: #fff;
  }
}

.greet {
  margin-top: 16rpx;
  font-size: 44rpx;
  font-weight: 700;
  line-height: 1.3;
}

.track-card {
  margin-top: 32rpx;
  background: rgba(255, 255, 255, 0.18);
  border-radius: 24rpx;
  padding: 36rpx 48rpx 28rpx;
}

.track-line {
  position: relative;
  height: 8rpx;
  background: rgba(255, 255, 255, 0.25);
  border-radius: 8rpx;
  margin: 0 24rpx 48rpx;
}

.track-fill {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  background: #fff;
  border-radius: 8rpx;
  transition: width 0.3s ease;
}

.node {
  position: absolute;
  top: 50%;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100rpx;

  &.start {
    left: 0;
  }
  &.end {
    /* left 由 style 设置 */
  }
}

.node-icon {
  width: 56rpx;
  height: 56rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28rpx;
  margin-bottom: 8rpx;
  border: 4rpx solid rgba(255, 255, 255, 0.5);
}

.node.active .node-icon {
  background: #ffd666;
  border-color: #fff;
}

.node-tag {
  position: absolute;
  top: -36rpx;
  font-size: 20rpx;
  opacity: 0.9;
}

.node-label {
  margin-top: 4rpx;
  font-size: 22rpx;
  opacity: 0.9;
}

.hero-actions {
  margin-top: 28rpx;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.coin-pill {
  display: flex;
  align-items: center;
  gap: 8rpx;
  background: #ffd666;
  color: #8a5a00;
  padding: 10rpx 24rpx;
  border-radius: 999rpx;
  font-weight: 700;

  .coin-yen {
    width: 32rpx;
    height: 32rpx;
    border-radius: 50%;
    background: $gold;
    color: #fff;
    font-size: 18rpx;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .coin-num {
    font-size: 28rpx;
  }
}

.switch-btn {
  background: rgba(255, 255, 255, 0.2);
  padding: 12rpx 24rpx;
  border-radius: 999rpx;
  font-size: 24rpx;
}

.card {
  background: #fff;
  border-radius: 28rpx;
  box-shadow: 0 8rpx 28rpx rgba(90, 60, 180, 0.06);
}

.login-tip {
  margin: 32rpx;
  padding: 48rpx;
  text-align: center;
  color: #666;
  font-size: 28rpx;

  .login-link {
    margin-top: 20rpx;
    color: $purple;
    font-weight: 600;
  }
}

.task-list {
  padding: 24rpx 28rpx 0;
}

.empty-tip {
  text-align: center;
  color: #aaa;
  font-size: 28rpx;
  padding: 80rpx 0;
}

.task-card {
  padding: 28rpx 28rpx 24rpx;
  margin-bottom: 24rpx;
}

.task-main {
  display: flex;
  align-items: flex-start;
  margin-bottom: 24rpx;
}

.task-icon {
  width: 96rpx;
  height: 96rpx;
  border-radius: 24rpx;
  background: #ffe8e8;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 24rpx;
  flex-shrink: 0;
  overflow: hidden;
}

.task-icon-img {
  width: 100%;
  height: 100%;
}

.task-emoji {
  font-size: 44rpx;
}

.task-info {
  flex: 1;
  min-width: 0;
  padding-top: 4rpx;
}

.task-name-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  min-width: 0;
}

.task-name {
  font-size: 32rpx;
  font-weight: 700;
  color: #1a1a1a;
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 保存成功后的完成勾 */
.done-badge {
  flex-shrink: 0;
  width: 44rpx;
  height: 44rpx;
  border-radius: 50%;
  background: linear-gradient(145deg, #52d68a 0%, #22c55e 55%, #16a34a 100%);
  box-shadow:
    0 4rpx 12rpx rgba(34, 197, 94, 0.45),
    0 0 0 4rpx rgba(34, 197, 94, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
}

.done-check {
  color: #fff;
  font-size: 26rpx;
  font-weight: 800;
  line-height: 1;
  transform: translateY(-1rpx);
}


.task-req {
  margin-top: 10rpx;
  font-size: 24rpx;
  color: #999;
  line-height: 1.4;
}

.task-actions {
  display: flex;
  gap: 16rpx;

  &.three-cols {
    flex-wrap: wrap;

    .btn {
      flex: 1 1 30%;
      min-width: 0;
      font-size: 24rpx;
      padding: 0 8rpx;
    }
  }
}

.btn {
  flex: 1;
  height: 76rpx;
  border-radius: 999rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 26rpx;
  font-weight: 500;

  .play-ico {
    margin-right: 8rpx;
    font-size: 22rpx;
  }

  &.ghost {
    background: #f0ebff;
    color: $purple;

    &.disabled {
      opacity: 0.45;
      color: #999;
      background: #f2f2f2;
    }
  }

  &.game {
    background: linear-gradient(90deg, #ffb347, #ff7eb3);
    color: #fff;
    box-shadow: 0 8rpx 20rpx rgba(255, 126, 179, 0.35);
    font-weight: 600;
  }

  &.primary {
    background: $purple;
    color: #fff;
    box-shadow: 0 8rpx 20rpx rgba(123, 92, 255, 0.35);
  }
}


.list-end {
  text-align: center;
  color: #c8c2d8;
  font-size: 24rpx;
  padding: 16rpx 0 32rpx;
}

.submit-bar {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  padding: 16rpx 32rpx calc(16rpx + env(safe-area-inset-bottom));
  background: linear-gradient(180deg, rgba(240, 238, 246, 0) 0%, $page-bg 30%);
  z-index: 20;
}

.submit-btn {
  height: 96rpx;
  border-radius: 999rpx;
  background: #e8e6f0;
  color: #999;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12rpx;
  font-size: 28rpx;

  .submit-ico {
    font-size: 30rpx;
  }

  &.ready {
    background: linear-gradient(90deg, $purple-deep, $purple);
    color: #fff;
    box-shadow: 0 10rpx 28rpx rgba(123, 92, 255, 0.4);
  }
}

/* ---------- 上传弹窗 ---------- */
.upload-mask {
  position: fixed;
  left: 0;
  right: 0;
  top: 0;
  /* 避开 tabBar：uni-app 提供 --window-bottom */
  bottom: var(--window-bottom, 0px);
  background: rgba(20, 16, 40, 0.45);
  z-index: 1000;
  display: flex;
  align-items: flex-end;
  justify-content: center;
}

.upload-sheet {
  position: relative;
  width: 100%;
  max-height: 78vh;
  background: #fff;
  border-radius: 32rpx 32rpx 0 0;
  padding: 28rpx 32rpx 24rpx;
  box-sizing: border-box;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.upload-body {
  /* uni-app scroll-view 需明确高度；预留标题区 */
  height: 62vh;
  max-height: calc(78vh - 140rpx);
  width: 100%;
  box-sizing: border-box;
}


.upload-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 28rpx;
}

.upload-title-wrap {
  flex: 1;
  min-width: 0;
  padding-right: 24rpx;
}

.upload-title {
  font-size: 36rpx;
  font-weight: 700;
  color: #1a1a1a;
  display: block;
}

.upload-sub {
  margin-top: 10rpx;
  font-size: 24rpx;
  color: #999;
  line-height: 1.4;
  display: block;
}

.upload-close {
  width: 56rpx;
  height: 56rpx;
  border-radius: 50%;
  color: #bbb;
  font-size: 40rpx;
  line-height: 52rpx;
  text-align: center;
  flex-shrink: 0;
}

.upload-section {
  margin-bottom: 32rpx;
}

.sec-label {
  font-size: 30rpx;
  font-weight: 600;
  color: #222;
  display: block;
}

.sec-hint {
  margin-top: 8rpx;
  font-size: 24rpx;
  color: #aaa;
  line-height: 1.4;
  display: block;
}

.sec-label-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16rpx;

  .sec-label {
    margin: 0;
  }
}

.sec-count {
  font-size: 24rpx;
  color: #bbb;
}

.media-row {
  margin-top: 20rpx;
  display: flex;
  flex-wrap: wrap;
  gap: 20rpx;
}

.media-add {
  width: 160rpx;
  height: 160rpx;
  border-radius: 20rpx;
  border: 2rpx dashed #c4b5fd;
  background: #f5f0ff;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: $purple;
}

.media-ico {
  font-size: 48rpx;
  line-height: 1.2;
  margin-bottom: 8rpx;

  &.upload-arrow {
    font-size: 44rpx;
    font-weight: 600;
  }
}

.media-add-text {
  font-size: 24rpx;
  color: $purple;
}

.media-preview {
  width: 160rpx;
  height: 160rpx;
  border-radius: 20rpx;
  overflow: hidden;
  position: relative;
  background: #f0ebff;
}

.preview-video,
.preview-img {
  width: 100%;
  height: 100%;
}

.video-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: $purple;
}

.media-del {
  position: absolute;
  top: 8rpx;
  right: 8rpx;
  width: 40rpx;
  height: 40rpx;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.55);
  color: #fff;
  font-size: 28rpx;
  line-height: 36rpx;
  text-align: center;
  z-index: 2;
}

.media-rechoose {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 48rpx;
  background: rgba(0, 0, 0, 0.45);
  color: #fff;
  font-size: 22rpx;
  text-align: center;
  line-height: 48rpx;
  z-index: 2;
}

.desc-input {
  width: 100%;
  height: 140rpx;
  min-height: 140rpx;
  box-sizing: border-box;
  background: #f7f6fb;
  border-radius: 20rpx;
  padding: 20rpx;
  font-size: 28rpx;
  color: #333;
  line-height: 1.5;
}

.desc-ph {
  color: #c0bcd0;
}

.upload-tip {
  display: flex;
  align-items: flex-start;
  gap: 12rpx;
  background: #fff8e6;
  border-radius: 16rpx;
  padding: 16rpx 20rpx;
  margin-bottom: 8rpx;
}

.tip-ico {
  font-size: 28rpx;
  flex-shrink: 0;
  line-height: 1.4;
}

.tip-text {
  flex: 1;
  font-size: 22rpx;
  color: #a67c00;
  line-height: 1.5;
}

.upload-footer {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 24rpx;
  margin-top: 28rpx;
  margin-bottom: 16rpx;
  padding: 8rpx 0 8rpx;
  box-sizing: border-box;
}

.footer-btn {
  min-width: 168rpx;
  height: 80rpx;
  padding: 0 40rpx;
  border-radius: 999rpx;
  font-size: 30rpx;
  display: flex;
  align-items: center;
  justify-content: center;

  &.cancel {
    color: #666;
    background: #f3f2f7;
  }

  &.save {
    background: linear-gradient(90deg, $purple-deep, $purple);
    color: #fff;
    font-weight: 600;
    box-shadow: 0 8rpx 20rpx rgba(123, 92, 255, 0.3);
  }

  &.save:not(.ready) {
    background: linear-gradient(90deg, #b5a6ff, #9b8cff);
    color: #fff;
    opacity: 0.9;
  }

  &.save.ready {
    background: linear-gradient(90deg, $purple-deep, $purple);
    color: #fff;
    box-shadow: 0 8rpx 20rpx rgba(123, 92, 255, 0.35);
    opacity: 1;
  }

  &.disabled {
    pointer-events: auto;
  }
}


</style>
