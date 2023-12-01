import { message } from 'antd'
import axios, { AxiosError, AxiosResponse } from 'axios'

export interface ApiResult {
  code: number
  success: boolean
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  data: undefined | object | PageData
  msg: string
  errors: ApiError[]
}

export interface ApiError {
  name: string
  messages: string[]
}

export interface PageData {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  data: any
  limit: number
  page: number
  total: number
}

export interface Error {}

axios.defaults.withCredentials = true
// 创建 axios 实例
const instance = axios.create({
  timeout: 300 * 1000, // 请求超时时间
})

// Request interceptor
instance.interceptors.request.use((requestConfig) => {
  requestConfig.headers['z-application-id'] = 'wildgoods-web'
  requestConfig.headers['z-user-id'] = '6526b85d74727cbf608de79b'
  requestConfig.headers['z-user-name'] = 'Lewis Zou'
  // requestConfig.headers['z-trace-id'] = new ObjectId()

  return requestConfig
  // }
})

// Response interceptor
instance.interceptors.response.use(
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (response: AxiosResponse<ApiResult | any, any>) => {
    if (response.status === 401) {
      // localStorage.setItem(E_Storage.LOGOUT_PAGE, window && window.location && window.location.pathname)
      // localStorage.removeItem(E_Storage.USER)
      // localStorage.removeItem(E_Storage.EXPIRE_TIME)
      // localStorage.removeItem(E_Storage.USER_PASSWORD_STRENGTH)
      // window.location.href = `${config.pathPrefix}/`
      throw '401'
    }

    const result = response.data as ApiResult
    if (!result) {
      // 1. 若没有返回数据，则根据 statusCode 来判断
      if (response.status < 200 && response.status >= 300) {
        const msg = '服务请求错误: ' + response.status
        message.error(msg)
        throw msg
      }
      return response
    }

    // 请求失败
    if (!result.success || result.code !== 0) {
      if (result.msg) {
        message.error(result.msg)
        throw result.msg
      } else {
        if (result.errors) {
          const msg = result.errors.map((e) => {
            return `${e.name}： ${e.message} `
          })
          message.error(msg)
          throw msg
        } else {
          throw '未知的错误'
        }
      }
    }

    return response
  },
  (error: AxiosError) => {
    const apiResult = error.response?.data as ApiResult
    if (!apiResult) {
      message.error('未知错误')
    } else {
      if (apiResult.errors || message.error.length > 0) {
        const msg = apiResult.errors.map((x) => {
          return ''.concat(...x.messages)
        })
        message.error(msg)
      } else {
        if (apiResult.msg) {
          message.error(apiResult.msg)
        } else {
          message.error('未知错误')
        }
      }
    }
    throw ''
  }
)

export { instance as request }
