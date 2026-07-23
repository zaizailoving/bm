import { BASE_URL } from '@/utils/config'
import { useMemberStore } from '@/stores'
import type { ResultModel } from '@/types/api'

export type UploadCheckinParams = {
    checkin_id: number
    description?: string
    /** 本地临时路径，视频仅一个 */
    videoPath?: string
    /** 本地临时路径列表 */
    imagePaths?: string[]
}

/**
 * 上传打卡内容：POST /api/checkin/upload (multipart)
 * uni.uploadFile 每次只能传一个文件，故视频/图片会顺序上传；
 * 描述与 checkin_id 放在第一次请求的 formData 中。
 */
export async function uploadCheckinApi(params: UploadCheckinParams): Promise<{ uploaded: boolean }> {
    const { checkin_id, description, videoPath, imagePaths = [] } = params
    const hasVideo = !!videoPath
    const hasImages = imagePaths.length > 0
    const hasDesc = !!(description && description.trim())

    if (!hasVideo && !hasImages && !hasDesc) {
        throw new Error('请上传视频/图片或填写描述')
    }

    const memberStore = useMemberStore()
    const token = memberStore.profile?.access_token
    if (!token) {
        throw new Error('请先登录')
    }

    const header: Record<string, string> = {
        Authorization: `Bearer ${token}`,
    }

    // 无文件时：用 form 提交（仅描述）
    if (!hasVideo && !hasImages) {
        return formPostUpload(checkin_id, description!.trim(), header)
    }

    let firstDone = false
    const descOnce = hasDesc ? description!.trim() : undefined

    if (hasVideo && videoPath) {
        await uploadOneFile({
            filePath: videoPath,
            name: 'video',
            checkin_id,
            description: !firstDone ? descOnce : undefined,
            header,
        })
        firstDone = true
    }

    for (const path of imagePaths) {
        await uploadOneFile({
            filePath: path,
            name: 'images',
            checkin_id,
            description: !firstDone ? descOnce : undefined,
            header,
        })
        firstDone = true
    }

    return { uploaded: true }
}

function formPostUpload(
    checkin_id: number,
    description: string,
    header: Record<string, string>,
): Promise<{ uploaded: boolean }> {
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
}): Promise<{ uploaded: boolean }> {
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
    resolve: (v: { uploaded: boolean }) => void,
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
        uni.showToast({ icon: 'none', title: msg })
        reject(new Error(msg))
        return
    }

    if (body && typeof body === 'object' && 'isSuccess' in body) {
        const result = body as ResultModel<{ uploaded: boolean }>
        if (result.isSuccess) {
            resolve(result.data || { uploaded: true })
            return
        }
        if (result.code === 401) {
            const memberStore = useMemberStore()
            memberStore.clearProfile()
            uni.navigateTo({ url: '/pages/login/login' })
        }
        const msg = result.errorMessage || '上传失败'
        uni.showToast({ icon: 'none', title: msg })
        reject(new Error(msg))
        return
    }

    resolve({ uploaded: true })
}
