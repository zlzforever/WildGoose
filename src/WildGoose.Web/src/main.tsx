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
import "@ant-design/v5-patch-for-react-19"

let baseName = window.wildgoose.baseName || "/"
baseName = baseName === "${BASE_PATH}" || baseName === "${PATH_BASE}" ? "/" : baseName

try {
  let user
  let oidcCallback
  if (signinRedirectCallbackPath.find((x) => location.pathname.endsWith(x))) {
    console.log("Handling OIDC callback for signin...")
    user = await signinRedirectCallback()
    oidcCallback = true
  } else if (signoutRedirectCallbackPath.find((x) => location.pathname.endsWith(x))) {
    console.log("Handling OIDC callback for signout...")
    await signoutRedirectCallback()
  } else if (location.pathname.endsWith(signinSilentCallbackPath)) {
    console.log("Handling OIDC callback for silent signin...")
    user = await signinSilentCallback()
    oidcCallback = true
  } else {
    if (window.wildgoose.gateway?.enabled) {
      // Gateway mode: gateway handles OIDC at the proxy level.
      // No eager auth check — first API call triggers gateway auth flow.
    } else {
      user = await getUser()
      if (user == null) {
        console.log("No user found, redirecting to signin...")
        await signinRedirect()
      }
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
  console.error("Authentication error:", error)
  await signinRedirect()
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <BrowserRouter basename={baseName}>
    <App />
  </BrowserRouter>,
)
