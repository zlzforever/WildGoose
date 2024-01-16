import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.css";
import { BrowserRouter } from "react-router-dom";
const signinRedirectCallbackPath = "/signin-redirect-callback";
const signinSilentCallbackPath = "/signin-silent-callback";
import {
  getUser,
  signinRedirect,
  signinRedirectCallback,
  signinSilentCallback,
} from "./lib/auth";

try {
  let user;
  let oidcCallback;
  if (location.pathname === signinRedirectCallbackPath) {
    user = await signinRedirectCallback();
    oidcCallback = true;
  } else if (location.pathname === signinSilentCallbackPath) {
    user = await signinSilentCallback();
    oidcCallback = true;
  } else {
    user = await getUser();
    if (user == null) {
      await signinRedirect();
    }
  }
  if (oidcCallback) {
    let redirectPath = localStorage.getItem("RedirectPath");
    redirectPath = redirectPath ? redirectPath : "/";
    if (location.pathname != redirectPath) {
      window.location.href = redirectPath;
    }
  } else {
    localStorage.setItem("RedirectPath", "");
  }
} catch (error) {
  await signinRedirect();
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <BrowserRouter>
    <App />
  </BrowserRouter>
);
