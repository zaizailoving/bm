import { httpGet } from '@/utils/http'
import type { UserProfile } from '@/types/api'

/** GET /api/user/profile */
export const getUserProfileApi = () => httpGet<UserProfile>('/api/user/profile')
