import ReactDOM from "react-dom/client"
import App from "./App.tsx"
import "./index.css"
import { BrowserRouter } from "react-router-dom"
const signinRedirectCallbackPath = ["/signin-redirect-callback", "/signin-oidc"]
const signoutRedirectCallbackPath = ["/signout-redirect-callback", "/signout-callback-oidc"]
const signinSilentCallbackPath = "/signin-silent-callback"
import {
  getUser,
  signinRedirect,
  signinRedirectCallback,
  signinSilentCallback,
  signoutRedirectCallback,
} from "./lib/auth"

let baseName = window.wildgoose.baseName || "/"
baseName = baseName === "${BASE_PATH}" || baseName === "${PATH_BASE}" ? "/" : baseName

try {
  let user
  let oidcCallback
  if (signinRedirectCallbackPath.find((x) => location.pathname.endsWith(x))) {
    user = await signinRedirectCallback()
    oidcCallback = true
  } else if (signoutRedirectCallbackPath.find((x) => location.pathname.endsWith(x))) {
    await signoutRedirectCallback()
  } else if (location.pathname.endsWith(signinSilentCallbackPath)) {
    user = await signinSilentCallback()
    oidcCallback = true
  } else {
    user = await getUser()
    if (user == null) {
      await signinRedirect()
    }
  }
  if (oidcCallback) {
    let redirectPath = localStorage.getItem("RedirectPath")
    redirectPath = redirectPath ? redirectPath : "/"
    if (location.pathname != redirectPath) {
      window.location.href = redirectPath
    }
  } else {
    localStorage.setItem("RedirectPath", "")
  }
} catch (error) {
  await signinRedirect()
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <BrowserRouter basename={baseName}>
    <App />
  </BrowserRouter>
)
