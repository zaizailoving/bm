/**
 * 本地教学视频映射
 * 后端 teach_video_url 为空时，按任务名称匹配 static/videos 下文件
 * 文件名使用英文，避免 H5 中文路径编码导致黑屏
 */

/** 本地视频清单（file 需与 static/videos 下文件名一致，建议 ASCII） */
const LOCAL_VIDEOS: { keywords: string[]; file: string }[] = [
    { keywords: ['弹唇', '啵啵'], file: 'tan-chun-bobo.mp4' },
    { keywords: ['抿唇'], file: 'min-chun.mp4' },
    { keywords: ['拉纽扣', '纽扣'], file: 'la-niu-kou.mp4' },
    { keywords: ['捏鼻', '踱步'], file: 'nie-bi-duo-bu.mp4' },
    { keywords: ['吹水花', '水花'], file: 'chui-shui-hua.mp4' },
    { keywords: ['啊咿唔', '啊伊唔'], file: 'a-yi-wu.mp4' },
]

/**
 * 拼装可播放的静态视频 URL
 * H5 使用 origin 绝对路径，避免路由 base 影响
 */
export function buildLocalVideoUrl(fileName: string): string {
    const name = (fileName || '').trim()
    if (!name) return ''

    // 已是完整 URL
    if (/^https?:\/\//i.test(name)) return name

    // 已是 /static/... 路径
    if (name.startsWith('/')) {
        return absolutizeStaticPath(name)
    }

    const path = `/static/videos/${name}`
    return absolutizeStaticPath(path)
}

function absolutizeStaticPath(path: string): string {
    // #ifdef H5
    if (typeof window !== 'undefined' && window.location?.origin) {
        return `${window.location.origin}${path}`
    }
    // #endif

    return path
}

/**
 * 解析教学视频地址
 * 优先后端 teach_video_url，否则按任务名匹配本地文件
 */
export function resolveTeachVideo(
    taskName: string,
    teachVideoUrl?: string | null,
): string | null {
    const remote = (teachVideoUrl || '').trim()
    if (remote) {
        if (/^https?:\/\//i.test(remote)) return remote
        if (remote.startsWith('/')) return absolutizeStaticPath(remote)
        return buildLocalVideoUrl(remote)
    }

    const name = taskName || ''
    for (const item of LOCAL_VIDEOS) {
        if (item.keywords.some((k) => name.includes(k))) {
            return buildLocalVideoUrl(item.file)
        }
    }
    return null
}

/** 仅返回本地文件名（用于页面传参，避免长 URL 在 query 被截断） */
export function resolveTeachVideoFile(
    taskName: string,
    teachVideoUrl?: string | null,
): string | null {
    const remote = (teachVideoUrl || '').trim()
    if (remote) {
        if (/^https?:\/\//i.test(remote)) return remote
        const parts = remote.replace(/\\/g, '/').split('/')
        return parts[parts.length - 1] || remote
    }

    const name = taskName || ''
    for (const item of LOCAL_VIDEOS) {
        if (item.keywords.some((k) => name.includes(k))) {
            return item.file
        }
    }
    return null
}

export function hasTeachVideo(taskName: string, teachVideoUrl?: string | null) {
    return !!resolveTeachVideo(taskName, teachVideoUrl)
}
