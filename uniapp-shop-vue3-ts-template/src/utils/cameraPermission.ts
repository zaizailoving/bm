/**
 * App 端摄像头权限：申请 + 被拒时弹窗引导去系统设置
 */

export type CameraPermissionResult = {
    granted: boolean
    /** 用户永久拒绝，只能去设置页打开 */
    needSettings: boolean
    message: string
}

function isAppPlus(): boolean {
    try {
        // #ifdef APP-PLUS
        return true
        // #endif
        // #ifndef APP-PLUS
        return false
        // #endif
    } catch {
        return false
    }
}

function getPlus(): any {
    try {
        return (globalThis as unknown as { plus?: any }).plus
    } catch {
        return null
    }
}

/** 打开本应用的系统权限设置页 */
export function openAppPermissionSettings(): void {
    try {
        // 新版 uni-app
        const uniAny = uni as any
        if (typeof uniAny.openAppAuthorizeSetting === 'function') {
            uniAny.openAppAuthorizeSetting()
            return
        }
    } catch {
        /* fallthrough */
    }

    const plus = getPlus()
    if (!plus) {
        uni.showToast({ icon: 'none', title: '请到系统设置中开启摄像头权限' })
        return
    }

    try {
        // iOS
        if (plus.os && String(plus.os.name || '').toLowerCase() === 'ios') {
            plus.runtime.openURL('app-settings:')
            return
        }
    } catch {
        /* fallthrough */
    }

    try {
        // Android 应用详情
        const main = plus.android.runtimeMainActivity()
        const Intent = plus.android.importClass('android.content.Intent')
        const Settings = plus.android.importClass('android.provider.Settings')
        const Uri = plus.android.importClass('android.net.Uri')
        const intent = new Intent()
        intent.setAction(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
        const uri = Uri.fromParts('package', main.getPackageName(), null)
        intent.setData(uri)
        main.startActivity(intent)
        return
    } catch {
        /* fallthrough */
    }

    uni.showToast({ icon: 'none', title: '请到系统设置 → 应用权限 中开启摄像头' })
}

/**
 * 弹窗引导用户去开启摄像头
 * @returns 用户是否点了「去设置」
 */
export function promptOpenCameraSettings(
    content = '训练需要使用前置摄像头识别抿唇/弹唇。请在系统设置中允许本应用使用「摄像头」后返回重试。',
): Promise<boolean> {
    return new Promise((resolve) => {
        uni.showModal({
            title: '需要开启摄像头权限',
            content,
            confirmText: '去设置',
            cancelText: '取消',
            success: (res) => {
                if (res.confirm) {
                    openAppPermissionSettings()
                    resolve(true)
                } else {
                    resolve(false)
                }
            },
            fail: () => resolve(false),
        })
    })
}

function checkAndroidCameraGranted(): boolean | null {
    const plus = getPlus()
    if (!plus || !plus.android) return null
    try {
        const main = plus.android.runtimeMainActivity()
        const ContextCompat = plus.android.importClass('androidx.core.content.ContextCompat')
        // 部分基座没有 androidx，再试 support
        let check: any
        try {
            check = ContextCompat.checkSelfPermission(
                main,
                'android.permission.CAMERA',
            )
        } catch {
            try {
                const Support = plus.android.importClass(
                    'android.support.v4.content.ContextCompat',
                )
                check = Support.checkSelfPermission(main, 'android.permission.CAMERA')
            } catch {
                const PackageManager = plus.android.importClass('android.content.pm.PackageManager')
                check = main.checkSelfPermission('android.permission.CAMERA')
                return check === PackageManager.PERMISSION_GRANTED
            }
        }
        const PackageManager = plus.android.importClass('android.content.pm.PackageManager')
        return check === PackageManager.PERMISSION_GRANTED
    } catch {
        return null
    }
}

function requestAndroidCamera(): Promise<CameraPermissionResult> {
    return new Promise((resolve) => {
        const plus = getPlus()
        if (!plus || !plus.android || typeof plus.android.requestPermissions !== 'function') {
            resolve({ granted: true, needSettings: false, message: '' })
            return
        }

        // 已授权则直接过
        const already = checkAndroidCameraGranted()
        if (already === true) {
            resolve({ granted: true, needSettings: false, message: '' })
            return
        }

        plus.android.requestPermissions(
            ['android.permission.CAMERA'],
            (e: {
                deniedAlways?: string[]
                deniedPresent?: string[]
                granted?: string[]
            }) => {
                const always = (e.deniedAlways && e.deniedAlways.length > 0) || false
                const present = (e.deniedPresent && e.deniedPresent.length > 0) || false
                const grantedList = e.granted || []
                const ok =
                    !always &&
                    !present &&
                    (grantedList.length === 0 ||
                        grantedList.some((p) => String(p).indexOf('CAMERA') >= 0) ||
                        checkAndroidCameraGranted() === true)

                // 再读一次系统状态更准
                const recheck = checkAndroidCameraGranted()
                if (recheck === true) {
                    resolve({ granted: true, needSettings: false, message: '' })
                    return
                }
                if (always) {
                    resolve({
                        granted: false,
                        needSettings: true,
                        message: '摄像头权限已被永久拒绝，请到系统设置中开启',
                    })
                    return
                }
                if (present || !ok) {
                    resolve({
                        granted: false,
                        needSettings: false,
                        message: '未获得摄像头权限，请允许后重试',
                    })
                    return
                }
                resolve({ granted: true, needSettings: false, message: '' })
            },
            () => {
                resolve({
                    granted: false,
                    needSettings: true,
                    message: '无法申请摄像头权限，请到系统设置中开启',
                })
            },
        )
    })
}

function requestIosCameraHint(): Promise<CameraPermissionResult> {
    // iOS 由 getUserMedia 触发系统弹窗；这里只做预检提示
    // 若曾拒绝，WebView 内 getUserMedia 会直接失败，由业务层再弹「去设置」
    return Promise.resolve({ granted: true, needSettings: false, message: '' })
}

/**
 * 申请摄像头权限。被拒时自动弹窗，可跳转系统设置。
 * @param autoPrompt 被拒是否自动弹窗（默认 true）
 */
export async function ensureCameraPermission(
    autoPrompt = true,
): Promise<CameraPermissionResult> {
    // 非 App：交给浏览器 getUserMedia
    // #ifndef APP-PLUS
    if (!isAppPlus()) {
        return { granted: true, needSettings: false, message: '' }
    }
    // #endif

    const plus = getPlus()
    if (!plus) {
        return { granted: true, needSettings: false, message: '' }
    }

    const osName = String((plus.os && plus.os.name) || '').toLowerCase()
    let result: CameraPermissionResult

    if (osName === 'android') {
        result = await requestAndroidCamera()
    } else {
        result = await requestIosCameraHint()
    }

    if (!result.granted && autoPrompt) {
        await promptOpenCameraSettings(result.message || undefined)
    }

    return result
}
