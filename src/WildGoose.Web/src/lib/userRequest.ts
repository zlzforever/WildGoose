import { message } from "antd"
import axios from "axios"
import type { AxiosError, AxiosInstance, AxiosRequestConfig } from "axios"
import { getUser, signinRedirect, signinSilent } from "./auth"
import { ApiResult } from "./request"
import { AESEnc, randomKey, uuid } from "./utils"

class CustomAxiosInstance {
  instance: AxiosInstance
  constructor(axiosConfig: AxiosRequestConfig) {
    this.instance = axios.create(axiosConfig)
    this.setInterceptor()
  }

  setInterceptor() {
    this.instance.interceptors.request.use(async (config) => {
      const handleConfig = { ...config }
      handleConfig.headers["z-application-id"] = "wildgoose-web"
      const user = await getUser()

      if (user) {
        handleConfig.headers["z-user-id"] = user.profile.sub
        let displayName = `${user.profile.family_name}${user.profile.given_name}`
        if (!displayName && user.profile.name) {
          displayName = user.profile.name
        }
        if (!displayName && user.profile.nickname) {
          displayName = user.profile.nickname
        }
        handleConfig.headers["z-user-name"] = encodeURIComponent(displayName)
        if (user.access_token) {
          handleConfig.headers["Authorization"] = `Bearer ${user.access_token}`
        }
      }

      handleConfig.withCredentials = true
      const key = uuid()
      const bkey = key.split("")
      bkey.splice(10, 0, randomKey())

      console.log(document.cookie)

      handleConfig.headers["Z-Encrypt-Version"] = "v1.1"
      handleConfig.headers["Z-Encrypt-Key"] = bkey.join("")

      // 只处理 JSON 格式的数据加密
      const dataText = JSON.stringify(handleConfig.data)
      const encryptData = AESEnc(key, dataText)
      handleConfig.data = encryptData
      handleConfig.headers["Content-Type"] = "application/json"

      return handleConfig
    })
    this.instance.interceptors.response.use(
      (response) => {
        const result = response.data as ApiResult
        if (!result) {
          // 1. 若没有返回数据，则根据 statusCode 来判断
          if (response.status < 200 && response.status >= 300) {
            const msg = "服务请求错误: " + response.status
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
                return `${e.name}： ${e.messages} `
              })
              message.error(msg)
              throw msg
            } else {
              throw "未知的错误"
            }
          }
        }

        return response
      },
      (error: AxiosError) => {
        if (error.response?.status === 401) {
          const handle401 = async () => {
            const user = await getUser()
            if (user && user.refresh_token) {
              await signinSilent()
            } else {
              await signinRedirect()
            }
          }
          handle401()
        }

        let errorInfo = "未知错误"
        const apiResult = error.response?.data as ApiResult
        if (apiResult) {
          if (apiResult.errors && apiResult.errors.length > 0) {
            errorInfo = apiResult.errors.map((x) => "\n".concat(...x.messages)).join("")
          } else if (apiResult.msg) {
            errorInfo = apiResult.msg
          }
        }
        message.error(errorInfo)
        throw errorInfo
      }
    )
  }
}

const createRequest = (axiosConfig: AxiosRequestConfig) => {
  const axiosInstance = new CustomAxiosInstance(axiosConfig)
  return axiosInstance.instance
}

export { createRequest }
