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
