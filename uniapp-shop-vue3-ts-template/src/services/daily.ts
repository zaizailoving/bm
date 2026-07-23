import { httpGet, httpPost } from '@/utils/http'
import type { DailyToday } from '@/types/api'

/** GET /api/daily/today */
export const getDailyTodayApi = () => httpGet<DailyToday>('/api/daily/today')

/** POST /api/daily/submit */
export const submitDailyApi = (plan_date: string) =>
    httpPost<{ submitted: boolean }>('/api/daily/submit', { plan_date })
