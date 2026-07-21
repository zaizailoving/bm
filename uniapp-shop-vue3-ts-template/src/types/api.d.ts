/** 后端统一响应 ResultModel<T> */
export type ResultModel<T = unknown> = {
    isSuccess: boolean
    code: number
    errorMessage: string
    data: T
}

/** 登录请求体 */
export type LoginInput = {
    user_name: string
    password: string
}

/** 登录成功 data */
export type LoginOutput = {
    user_num: string
    user_name: string
    user_id: number
    user_role: string
    userrole_id: number
    tenant_id: number
    expire: number
    access_token: string
    refresh_token: string
}

/** 注册请求体 */
export type RegisterInput = {
    user_name: string
    password: string
    nickname?: string
    phone?: string
}

/** 用户个人信息 */
export type UserProfile = {
    id: number
    nickname: string | null
    avatar: string | null
    phone: string | null
    role: string
    archive_no: string | null
    total_coins: number
    available_coins: number
    train_camp_status: string
}

/** 本地会员会话（登录后缓存） */
export type MemberProfile = LoginOutput & {
    nickname?: string | null
    avatar?: string | null
}
