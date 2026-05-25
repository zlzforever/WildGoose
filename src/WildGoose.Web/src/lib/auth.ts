import { UserManager, User, SignoutResponse, WebStorageStateStore } from "oidc-client-ts"

const oidcSettings = window.wildgoose.oidc
oidcSettings.userStore = new WebStorageStateStore({ store: window.localStorage })

const userManager = new UserManager(oidcSettings)
// Log.logger = console
// Log.level = Log.INFO

export async function getUser(): Promise<User | null> {
  const user = await userManager.getUser()
  if (user && user.profile.role) {
    let role = user.profile.role as any
    user.profile.role =
      Object.prototype.toString.call(role) === "[object Array]" ? role : role.split(" ")
  }
  return user
}

export async function signinRedirect(): Promise<void> {
  return userManager.signinRedirect()
}

export async function signinRedirectCallback(): Promise<User> {
  return userManager.signinRedirectCallback()
}

export async function signinSilent(): Promise<User | null> {
  return userManager.signinSilent()
}

export async function signinSilentCallback() {
  await userManager.signinSilentCallback()
  return await userManager.getUser()
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
