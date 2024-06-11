import type { User, SignoutResponse } from 'oidc-client'
import { Log, UserManager } from 'oidc-client'
import oidc from 'oidc-client'

const oidcSettings = window.wildgoods.oidc
oidcSettings.userStore = new oidc.WebStorageStateStore({ store: window.localStorage })

const userManager = new UserManager(oidcSettings)
Log.logger = console
Log.level = Log.INFO

export async function getUser(): Promise<User | null> {
  const user = await userManager.getUser()
  if (user && user.profile.role) {
    user.profile.role = Object.prototype.toString.call(user.profile.role) ===
      "[object Array]"
      ? user.profile.role
      : user.profile.role.split(" ")
  }
  return user
}

export async function signinRedirect(): Promise<void> {
  return userManager.signinRedirect()
}

export async function signinRedirectCallback(): Promise<User> {
  return userManager.signinRedirectCallback()
}

export async function signinSilent(): Promise<User> {
  return userManager.signinSilent()
}

export async function signinSilentCallback(): Promise<User | undefined> {
  return userManager.signinSilentCallback()
}

export async function signoutRedirect(): Promise<void> {
  return userManager.signoutRedirect()
}

export async function signoutRedirectCallback(): Promise<SignoutResponse> {
  return userManager.signoutRedirectCallback()
}

export const removeUserInfo = () => {
  // 遍历localstorage，清空localstorage
  if (window.localStorage) {
    const lsKeys = Object.keys(window.localStorage)
    lsKeys &&
      lsKeys.length &&
      lsKeys.forEach((item) => {
        localStorage.removeItem(item)
      })
  }
  // 遍历cookie，清空cookie
  if (document.cookie) {
    // eslint-disable-next-line no-useless-escape
    const ckKeys: any = document.cookie.match(/[^ =;]+(?=\=)/g)
    if (ckKeys && ckKeys.length) {
      for (let i = ckKeys.length; i--; )
        document.cookie = ckKeys[i] + "=0;expires=" + new Date(0).toUTCString()
    }
  }
}
