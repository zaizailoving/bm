import { BASE_URL, REQUEST_TIMEOUT } from './config'
import { useMemberStore } from '@/stores'
import type { ResultModel } from '@/types/api'
import { isMockToken, mockHttpRequest } from './mock'

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'

type RequestOptions = {
    url: string
    method?: HttpMethod
    data?: Record<string, unknown> | unknown
    header?: Record<string, string>
    /** 是否跳过 401 自动跳转登录，默认 false */
    skipAuthRedirect?: boolean
}

/**
 * 封装 uni.request，对接 BM.Service ResultModel
 * 若当前为 mock 登录会话，则走本地模拟，不请求后端
 */
export const http = <T = unknown>(options: RequestOptions): Promise<T> => {
    const memberStore = useMemberStore()
    const token = memberStore.profile?.access_token

    // Mock 模式：本地模拟接口
    if (isMockToken(token)) {
        return mockHttpRequest<T>({
            url: options.url,
            method: options.method,
            data: options.data,
        }).catch((err: unknown) => {
            const msg = err instanceof Error ? err.message : '请求失败'
            uni.showToast({ icon: 'none', title: msg })
            return Promise.reject(err instanceof Error ? err : new Error(msg))
        })
    }

    const header: Record<string, string> = {
        'Content-Type': 'application/json',
        ...(options.header || {}),
    }
    if (token) {
        header.Authorization = `Bearer ${token}`
    }

    return new Promise((resolve, reject) => {
        uni.request({
            url: BASE_URL + options.url,
            method: options.method || 'GET',
            data: options.data as UniApp.RequestOptions['data'],
            header,
            timeout: REQUEST_TIMEOUT,
            success: (res) => {
                const status = res.statusCode
                const body = res.data as ResultModel<T> | string

                // HTTP 层错误
                if (status < 200 || status >= 300) {
                    if (status === 401) {
                        handleUnauthorized(options.skipAuthRedirect)
                    }
                    const msg =
                        typeof body === 'object' && body && 'errorMessage' in body
                            ? (body as ResultModel).errorMessage || `请求失败(${status})`
                            : `请求失败(${status})`
                    uni.showToast({ icon: 'none', title: msg })
                    reject(new Error(msg))
                    return
                }

                // 业务 ResultModel
                if (body && typeof body === 'object' && 'isSuccess' in body) {
                    const result = body as ResultModel<T>
                    if (result.isSuccess) {
                        resolve(result.data)
                        return
                    }
                    if (result.code === 401) {
                        handleUnauthorized(options.skipAuthRedirect)
                    }
                    const msg = result.errorMessage || '请求失败'
                    uni.showToast({ icon: 'none', title: msg })
                    reject(new Error(msg))
                    return
                }

                // 非标准包装，直接返回
                resolve(body as T)
            },
            fail: (err) => {
                uni.showToast({ icon: 'none', title: '网络异常，请稍后重试' })
                reject(err)
            },
        })
    })
}


function handleUnauthorized(skip?: boolean) {
    const memberStore = useMemberStore()
    memberStore.clearProfile()
    if (skip) return
    // 避免重复跳转
    const pages = getCurrentPages()
    const current = pages[pages.length - 1] as { route?: string } | undefined
    if (current?.route?.includes('login')) return
    uni.navigateTo({ url: '/pages/login/login' })
}

export const httpGet = <T = unknown>(url: string, data?: Record<string, unknown>) =>
    http<T>({ url, method: 'GET', data })

export const httpPost = <T = unknown>(url: string, data?: unknown) =>
    http<T>({ url, method: 'POST', data })
