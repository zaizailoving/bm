import { BASE_URL } from '@/utils/config'
import { useMemberStore } from '@/stores'
import type { ResultModel, UploadCheckinResult } from '@/types/api'

export type UploadCheckinParams = {
    checkin_id: number
    description?: string
    /** 本地临时路径，视频仅一个 */
    videoPath?: string
    /** 本地临时路径列表 */
    imagePaths?: string[]
    /** 服务端已有图片/视频（允许仅改描述时提交） */
    hasExistingMedia?: boolean
}

/**
 * 上传打卡内容：POST /api/checkin/upload (multipart)
 * 至少需要一个图片或视频（本次新选或已有）；首次完成返回 coins_awarded=5
 */
export async function uploadCheckinApi(params: UploadCheckinParams): Promise<UploadCheckinResult> {
    const {
        checkin_id,
        description,
        videoPath,
        imagePaths = [],
        hasExistingMedia = false,
    } = params
    const hasVideo = !!videoPath
    const hasImages = imagePaths.length > 0
    const hasDesc = !!(description && description.trim())

    if (!hasVideo && !hasImages && !hasExistingMedia) {
        throw new Error('请至少上传一张图片或一个视频')
    }

    const memberStore = useMemberStore()
    const token = memberStore.profile?.access_token
    if (!token) {
        throw new Error('请先登录')
    }

    const header: Record<string, string> = {
        Authorization: `Bearer ${token}`,
    }

    // 无新文件：仅描述（服务端已有媒体）
    if (!hasVideo && !hasImages) {
        return formPostUpload(checkin_id, hasDesc ? description!.trim() : '', header)
    }

    let firstDone = false
    const descOnce = hasDesc ? description!.trim() : undefined
    let lastResult: UploadCheckinResult = {
        uploaded: true,
        coins_awarded: 0,
        available_coins: 0,
    }

    if (hasVideo && videoPath) {
        lastResult = await uploadOneFile({
            filePath: videoPath,
            name: 'video',
            checkin_id,
            description: !firstDone ? descOnce : undefined,
            header,
        })
        firstDone = true
    }

    for (const path of imagePaths) {
        lastResult = await uploadOneFile({
            filePath: path,
            name: 'images',
            checkin_id,
            description: !firstDone ? descOnce : undefined,
            header,
        })
        firstDone = true
    }

    return lastResult
}

function formPostUpload(
    checkin_id: number,
    description: string,
    header: Record<string, string>,
): Promise<UploadCheckinResult> {
    return new Promise((resolve, reject) => {
        uni.request({
            url: BASE_URL + '/api/checkin/upload',
            method: 'POST',
            header: {
                ...header,
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            data: {
                checkin_id,
                description,
            },
            timeout: 60000,
            success: (res) => {
                handleUploadResponse(res.statusCode, res.data, resolve, reject)
            },
            fail: (err) => {
                uni.showToast({ icon: 'none', title: '网络异常，请稍后重试' })
                reject(err)
            },
        })
    })
}

function uploadOneFile(opts: {
    filePath: string
    name: 'video' | 'images'
    checkin_id: number
    description?: string
    header: Record<string, string>
}): Promise<UploadCheckinResult> {
    const formData: Record<string, string> = {
        checkin_id: String(opts.checkin_id),
    }
    if (opts.description !== undefined && opts.description !== '') {
        formData.description = opts.description
    }

    return new Promise((resolve, reject) => {
        uni.uploadFile({
            url: BASE_URL + '/api/checkin/upload',
            filePath: opts.filePath,
            name: opts.name,
            formData,
            header: opts.header,
            timeout: 180000,
            success: (res) => {
                let body: unknown = res.data
                if (typeof body === 'string') {
                    try {
                        body = JSON.parse(body)
                    } catch {
                        /* keep string */
                    }
                }
                handleUploadResponse(res.statusCode, body, resolve, reject)
            },
            fail: (err) => {
                uni.showToast({ icon: 'none', title: '上传失败，请检查网络' })
                reject(err)
            },
        })
    })
}

function handleUploadResponse(
    statusCode: number,
    body: unknown,
    resolve: (v: UploadCheckinResult) => void,
    reject: (e: Error) => void,
) {
    if (statusCode === 401) {
        const memberStore = useMemberStore()
        memberStore.clearProfile()
        uni.navigateTo({ url: '/pages/login/login' })
        reject(new Error('请先登录'))
        return
    }

    if (statusCode < 200 || statusCode >= 300) {
        const msg =
            body && typeof body === 'object' && 'errorMessage' in body
                ? String((body as ResultModel).errorMessage || `上传失败(${statusCode})`)
                : `上传失败(${statusCode})`
        const tip =
            msg.includes('at least one image or video') || msg.includes('image or video')
                ? '请至少上传一张图片或一个视频'
                : msg
        uni.showToast({ icon: 'none', title: tip })
        reject(new Error(tip))
        return
    }

    if (body && typeof body === 'object' && 'isSuccess' in body) {
        const result = body as ResultModel<UploadCheckinResult>
        if (result.isSuccess) {
            resolve(
                result.data || {
                    uploaded: true,
                    coins_awarded: 0,
                    available_coins: 0,
                },
            )
            return
        }
        if (result.code === 401) {
            const memberStore = useMemberStore()
            memberStore.clearProfile()
            uni.navigateTo({ url: '/pages/login/login' })
        }
        const msg = result.errorMessage || '上传失败'
        const tip =
            msg.includes('at least one image or video') || msg.includes('image or video')
                ? '请至少上传一张图片或一个视频'
                : msg
        uni.showToast({ icon: 'none', title: tip })
        reject(new Error(tip))
        return
    }

    resolve({ uploaded: true, coins_awarded: 0, available_coins: 0 })
}
