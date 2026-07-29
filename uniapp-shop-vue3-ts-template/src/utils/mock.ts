/**
 * Mock 模式：用户名 mock 免校验登录，所有业务接口走本地模拟数据，
 * 不请求后端，但打卡/提交/金币等交互效果尽量真实。
 */
import type {
    DailyTaskItem,
    DailyToday,
    LoginOutput,
    UploadCheckinResult,
    UserProfile,
} from '@/types/api'

export const MOCK_USER_NAME = 'mock'
export const MOCK_TOKEN_PREFIX = 'mock_token_'

const STORAGE_KEY = 'bm_mock_state_v1'

/** 固定训练任务（与后端 Seed 一致） */
const FIXED_TASKS: { task_id: number; task_name: string; requirement: string }[] = [
    { task_id: 1, task_name: '贴闭口贴', requirement: '白天20min视情况+晚上' },
    { task_id: 2, task_name: '抿唇', requirement: '2组*10min' },
    { task_id: 3, task_name: '弹唇啵啵操', requirement: '1组*15个' },
    { task_id: 4, task_name: '拉纽扣', requirement: '1组*各方向连续5次 每次3-10s' },
    { task_id: 5, task_name: '捏鼻踱步', requirement: '1组*憋不住放手算1组' },
    { task_id: 6, task_name: 'N点训练', requirement: '1组*3min' },
    { task_id: 7, task_name: '吹水花', requirement: '1组*3min' },
    { task_id: 8, task_name: '啊咿呜哎', requirement: '1组（四个字-每个字发音3s）*15组' },
    { task_id: 9, task_name: '吹气球', requirement: '1组连续吹5个' },
    { task_id: 10, task_name: '抿压舌板', requirement: '2组*15min' },
    { task_id: 11, task_name: '腹式呼吸', requirement: '1组*3min' },
]

export type MockState = {
    total_coins: number
    available_coins: number
    plan_date: string
    week_no: number
    day_no: number
    status: 'draft' | 'submitted' | 'commented' | string
    tasks: DailyTaskItem[]
}

function todayStr(): string {
    const d = new Date()
    const y = d.getFullYear()
    const m = String(d.getMonth() + 1).padStart(2, '0')
    const day = String(d.getDate()).padStart(2, '0')
    return `${y}-${m}-${day}`
}

function buildFreshTasks(): DailyTaskItem[] {
    return FIXED_TASKS.map((t, i) => ({
        checkin_id: 1000 + i + 1,
        task_id: t.task_id,
        task_name: t.task_name,
        icon_url: null,
        requirement: t.requirement,
        teach_video_url: null,
        status: 'unfinished',
        video_url: null,
        image_urls: [],
        description: null,
    }))
}

function createDefaultState(): MockState {
    return {
        total_coins: 30,
        available_coins: 30,
        plan_date: todayStr(),
        week_no: 1,
        day_no: 1,
        status: 'draft',
        tasks: buildFreshTasks(),
    }
}

/**
 * 跨日（本地 0 点后）：
 * - 所有任务勾选 / 已上传 / 已提交状态全部清空为 unfinished
 * - 计划状态回到 draft
 * - 金币 total_coins / available_coins 累计保留，不清空
 */
function rollToNewDay(prev: {
    total_coins?: number
    available_coins?: number
    week_no?: number
    day_no?: number
}): MockState {
    const total = Math.max(0, Number(prev.total_coins) || 0)
    const available = Math.max(0, Number(prev.available_coins) || 0)
    let week_no = Math.max(1, Number(prev.week_no) || 1)
    let day_no = Math.max(1, Number(prev.day_no) || 1)

    // 进入新一天：day+1，满 7 天后 week+1
    day_no += 1
    if (day_no > 7) {
        day_no = 1
        week_no += 1
    }

    return {
        total_coins: total,
        available_coins: available,
        plan_date: todayStr(),
        week_no,
        day_no,
        status: 'draft',
        tasks: buildFreshTasks(),
    }
}

function loadState(): MockState {
    try {
        const raw = uni.getStorageSync(STORAGE_KEY)
        if (!raw) return createDefaultState()
        const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw
        if (!parsed || !Array.isArray(parsed.tasks)) return createDefaultState()

        // 跨天（0 点后 plan_date 变化）：勾选重置，金币保留
        if (String(parsed.plan_date || '') !== todayStr()) {
            const next = rollToNewDay(parsed)
            saveState(next)
            return next
        }
        return {
            total_coins: Math.max(0, Number(parsed.total_coins) || 0),
            available_coins: Math.max(0, Number(parsed.available_coins) || 0),
            plan_date: String(parsed.plan_date),
            week_no: Number(parsed.week_no) || 1,
            day_no: Number(parsed.day_no) || 1,
            status: parsed.status || 'draft',
            tasks: parsed.tasks as DailyTaskItem[],
        }
    } catch {
        return createDefaultState()
    }
}

function saveState(state: MockState) {
    try {
        uni.setStorageSync(STORAGE_KEY, JSON.stringify(state))
    } catch {
        /* ignore */
    }
}

let memoryState: MockState | null = null

/**
 * 每次读取都校验是否已过 0 点。
 * 应用挂在前台跨过 0 点时，下次任意 mock 接口也会触发重置。
 */
function getState(): MockState {
    if (!memoryState) {
        memoryState = loadState()
        return memoryState
    }
    if (memoryState.plan_date !== todayStr()) {
        memoryState = rollToNewDay(memoryState)
        saveState(memoryState)
    }
    return memoryState
}

function setState(patch: Partial<MockState>) {
    // 写入前也先跨日校验，避免旧日数据被覆盖写回
    const cur = getState()
    memoryState = { ...cur, ...patch }
    saveState(memoryState)
    return memoryState
}


export function isMockUserName(userName: string): boolean {
    return (userName || '').trim().toLowerCase() === MOCK_USER_NAME
}

export function isMockToken(token?: string | null): boolean {
    return !!(token && String(token).startsWith(MOCK_TOKEN_PREFIX))
}

/** 登录成功后的会话数据 */
export function mockLoginOutput(): LoginOutput {
    // 确保有本地状态
    getState()
    return {
        user_num: 'MOCK001',
        user_name: MOCK_USER_NAME,
        user_id: 90001,
        user_role: 'student',
        userrole_id: 1,
        tenant_id: 1,
        expire: Math.floor(Date.now() / 1000) + 7 * 24 * 3600,
        access_token: `${MOCK_TOKEN_PREFIX}${Date.now()}`,
        refresh_token: `${MOCK_TOKEN_PREFIX}refresh_${Date.now()}`,
    }
}

export function mockUserProfile(): UserProfile {
    const s = getState()
    return {
        id: 90001,
        nickname: '学员',
        avatar: null,
        phone: '138****0000',
        role: 'student',
        archive_no: 'MOCK-2026-001',
        total_coins: s.total_coins,
        available_coins: s.available_coins,
        train_camp_status: 'ongoing',
    }
}

export function mockDailyToday(): DailyToday {
    const s = getState()
    const done = s.tasks.filter((t) => t.status === 'uploaded' || t.status === 'submitted').length
    return {
        plan_date: s.plan_date,
        week_no: s.week_no,
        day_no: s.day_no,
        status: s.status,
        progress: `${done}/${s.tasks.length}`,
        tasks: s.tasks.map((t) => ({
            ...t,
            image_urls: [...(t.image_urls || [])],
        })),
    }
}

export function mockSubmitDaily(plan_date: string): { submitted: boolean } {
    const s = getState()
    if (plan_date && plan_date !== s.plan_date) {
        throw new Error('计划日期不匹配')
    }
    if (s.status === 'submitted' || s.status === 'commented') {
        throw new Error('今日已提交')
    }
    const unfinished = s.tasks.some((t) => t.status === 'unfinished')
    if (unfinished) {
        throw new Error('还有未完成的任务')
    }
    const tasks = s.tasks.map((t) => ({
        ...t,
        status: t.status === 'uploaded' ? 'submitted' : t.status,
    }))
    // 一键提交额外奖励 10 金币
    const bonus = 10
    setState({
        status: 'submitted',
        tasks,
        total_coins: s.total_coins + bonus,
        available_coins: s.available_coins + bonus,
    })
    return { submitted: true }
}

export type MockUploadParams = {
    checkin_id: number
    description?: string
    videoPath?: string
    imagePaths?: string[]
    hasExistingMedia?: boolean
}

/**
 * 游戏打卡完成：无需媒体，标记 uploaded，首次 +5 金币
 */
export function mockCompleteByGame(params: {
    checkin_id: number
    description?: string
}): UploadCheckinResult {
    const s = getState()
    if (s.status === 'submitted' || s.status === 'commented') {
        throw new Error('今日已提交，不可再改')
    }

    const idx = s.tasks.findIndex((t) => t.checkin_id === params.checkin_id)
    if (idx < 0) {
        throw new Error('打卡任务不存在')
    }

    const task = s.tasks[idx]
    if (task.status === 'submitted') {
        throw new Error('该动作已提交，不可再改')
    }

    // 已完成：不重复发奖
    if (task.status === 'uploaded') {
        return {
            uploaded: true,
            coins_awarded: 0,
            available_coins: s.available_coins,
        }
    }

    const wasUnfinished = task.status === 'unfinished'
    const next: DailyTaskItem = {
        ...task,
        status: 'uploaded',
        description:
            params.description?.trim() ||
            task.description ||
            '游戏打卡完成（弹唇啵啵操）',
        // 占位媒体，表示已通过游戏完成
        image_urls:
            task.image_urls && task.image_urls.length > 0
                ? [...task.image_urls]
                : ['game://bobo-complete'],
        video_url: task.video_url,
    }

    const coins_awarded = wasUnfinished ? 5 : 0
    const tasks = s.tasks.slice()
    tasks[idx] = next
    setState({
        tasks,
        total_coins: s.total_coins + coins_awarded,
        available_coins: s.available_coins + coins_awarded,
    })

    const after = getState()
    return {
        uploaded: true,
        coins_awarded,
        available_coins: after.available_coins,
    }
}

export function mockUploadCheckin(params: MockUploadParams): UploadCheckinResult {

    const s = getState()
    if (s.status === 'submitted' || s.status === 'commented') {
        throw new Error('今日已提交，不可再改')
    }

    const idx = s.tasks.findIndex((t) => t.checkin_id === params.checkin_id)
    if (idx < 0) {
        throw new Error('打卡任务不存在')
    }

    const task = s.tasks[idx]
    if (task.status === 'submitted') {
        throw new Error('该动作已提交，不可再改')
    }

    const hasNewVideo = !!params.videoPath
    const hasNewImages = !!(params.imagePaths && params.imagePaths.length > 0)
    const hasExisting =
        params.hasExistingMedia ||
        !!task.video_url ||
        (task.image_urls && task.image_urls.length > 0)

    if (!hasNewVideo && !hasNewImages && !hasExisting) {
        throw new Error('请至少上传一张图片或一个视频')
    }

    const wasUnfinished = task.status === 'unfinished'
    const next: DailyTaskItem = {
        ...task,
        status: 'uploaded',
        description:
            params.description !== undefined ? params.description || null : task.description,
        video_url: hasNewVideo
            ? params.videoPath || task.video_url
            : task.video_url || (hasExisting ? task.video_url : null),
        image_urls: hasNewImages
            ? [
                ...(task.image_urls || []).filter(Boolean),
                ...(params.imagePaths || []),
            ].slice(0, 9)
            : [...(task.image_urls || [])],
    }

    // 首次完成奖励 5 金币
    const coins_awarded = wasUnfinished ? 5 : 0
    const tasks = s.tasks.slice()
    tasks[idx] = next
    setState({
        tasks,
        total_coins: s.total_coins + coins_awarded,
        available_coins: s.available_coins + coins_awarded,
    })

    const after = getState()
    return {
        uploaded: true,
        coins_awarded,
        available_coins: after.available_coins,
    }
}

/** 模拟网络延迟，让操作更真实 */
export function mockDelay(ms = 280): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms))
}

/**
 * 统一 mock HTTP 路由（与真实 API 路径对齐）
 */
export async function mockHttpRequest<T>(opts: {
    url: string
    method?: string
    data?: unknown
}): Promise<T> {
    await mockDelay()
    const path = (opts.url || '').split('?')[0].replace(/\/+$/, '')
    const method = (opts.method || 'GET').toUpperCase()

    if (path === '/api/auth/login' && method === 'POST') {
        const body = (opts.data || {}) as { user_name?: string }
        if (!isMockUserName(body.user_name || '')) {
            throw new Error('Mock 仅支持用户名 mock')
        }
        return mockLoginOutput() as T
    }

    if (path === '/api/user/profile' && method === 'GET') {
        return mockUserProfile() as T
    }

    if (path === '/api/daily/today' && method === 'GET') {
        return mockDailyToday() as T
    }

    if (path === '/api/daily/submit' && method === 'POST') {
        const body = (opts.data || {}) as { plan_date?: string }
        return mockSubmitDaily(body.plan_date || todayStr()) as T
    }

    if (path === '/api/checkin/game-complete' && method === 'POST') {
        const body = (opts.data || {}) as { checkin_id?: number; description?: string }
        return mockCompleteByGame({
            checkin_id: Number(body.checkin_id) || 0,
            description: body.description,
        }) as T
    }


    if (path === '/api/auth/change-password' && method === 'POST') {
        return { changed: true } as T
    }

    if (path === '/api/auth/register' && method === 'POST') {
        throw new Error('Mock 模式不支持注册')
    }

    // 未知接口：返回空成功，避免页面崩溃
    console.warn('[mock] unhandled API', method, path)
    return {} as T
}

/**
 * 强制按「新一天」重置任务勾选（保留当前金币累计）。
 * 调试或手动清勾选时可用。
 */
export function resetMockDailyTasksKeepCoins() {
    const cur = memoryState || loadState()
    memoryState = rollToNewDay({
        total_coins: cur.total_coins,
        available_coins: cur.available_coins,
        // 不推进周/天序号：把 day 减回去再 roll，或直接写 fresh
        week_no: cur.week_no,
        day_no: Math.max(0, cur.day_no - 1),
    })
    // rollToNewDay 会 day+1，上面 -1 后保持原 week/day
    memoryState.week_no = cur.week_no
    memoryState.day_no = cur.day_no
    memoryState.plan_date = todayStr()
    saveState(memoryState)
}

/** 完全重置演示数据（含金币回到初始 30） */
export function resetMockState() {
    memoryState = createDefaultState()
    saveState(memoryState)
}


