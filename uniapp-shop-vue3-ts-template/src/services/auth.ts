import { httpPost } from '@/utils/http'
import type { LoginInput, LoginOutput, RegisterInput, ResetPasswordInput } from '@/types/api'
import { isMockUserName, mockDelay, mockLoginOutput } from '@/utils/mock'

/** POST /api/auth/login（用户名 mock 免校验，进入本地演示模式） */
export const loginApi = async (data: LoginInput): Promise<LoginOutput> => {
    if (isMockUserName(data.user_name)) {
        await mockDelay(200)
        return mockLoginOutput()
    }
    return httpPost<LoginOutput>('/api/auth/login', data)
}


/** POST /api/auth/register */
export const registerApi = (data: RegisterInput) =>
    httpPost('/api/auth/register', data)

/** POST /api/auth/reset-password */
export const resetPasswordApi = (data: ResetPasswordInput) =>
    httpPost('/api/auth/reset-password', data)

/** POST /api/auth/change-password（需登录） */
export const changePasswordApi = (data: { old_password: string; new_password: string }) =>
    httpPost('/api/auth/change-password', data)
