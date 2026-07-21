import { httpPost } from '@/utils/http'
import type { LoginInput, LoginOutput, RegisterInput } from '@/types/api'

/** POST /api/auth/login */
export const loginApi = (data: LoginInput) =>
    httpPost<LoginOutput>('/api/auth/login', data)

/** POST /api/auth/register */
export const registerApi = (data: RegisterInput) =>
    httpPost('/api/auth/register', data)

/** POST /api/auth/change-password（需登录） */
export const changePasswordApi = (data: { old_password: string; new_password: string }) =>
    httpPost('/api/auth/change-password', data)
