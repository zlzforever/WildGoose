import { ProConfigProvider } from "@ant-design/pro-provider"
import "./App.css"
import { ConfigProvider, Dropdown, Modal } from "antd"
import { Route, Routes, useLocation, useNavigate } from "react-router-dom"
import ProLayout, { ProSettings } from "@ant-design/pro-layout"
import { LogoutOutlined } from "@ant-design/icons"
import { useEffect, useState } from "react"
import defaultLayoutSettings from "../config/layoutSettings"
import routes from "../config/routes"
import RolePage from "./pages/RolePage"
import UserPage from "./pages/UserPage"
import { getUser, removeUserInfo, signoutRedirect } from "./lib/auth"
import AccoutImg from "./assets/images/account.png"
import { ExclamationCircleOutlined } from "@ant-design/icons"

function App() {
  const navigate = useNavigate()
  const location = useLocation()
  const [user, setUser] = useState<any>()
  const [settings] = useState<Partial<ProSettings>>({
    fixSiderbar: true,
    layout: "mix",
    splitMenus: false,
  })
  const [pathname, setPathname] = useState(location.pathname)

  useEffect(() => {
    const fun = async () => {
      const user = await getUser()
      setUser(user)
    }
    fun()
  }, [])

  // 检查用户是否拥有指定角色
  const hasRole = (roleName: string) => {
    return user?.profile?.roles?.includes(roleName)
  }

  // 检查用户是否可以访问角色页面
  const canAccessRolePage = () => {
    return hasRole("admin")
  }

  // 检查用户是否可以访问用户页面
  const canAccessUserPage = () => {
    return hasRole("admin") || hasRole("organization_admin") // 需替换为实际的机构管理员角色名称
  }

  const onLogout = () => {
    Modal.confirm({
      content: "确定要注销登录吗?",
      icon: <ExclamationCircleOutlined />,
      onOk() {
        removeUserInfo()
        signoutRedirect()
      },
      cancelText: "取消",
      onCancel() {
        Modal.destroyAll()
      },
    })
  }

  if (typeof document === "undefined") {
    return <div />
  }

  // 过滤路由，根据用户角色显示菜单
  const filteredRoutes = {
    ...routes,
    routes: routes.routes.filter((route) => {
      if (route.path === "/role") return canAccessRolePage()
      if (route.path === "/user") return canAccessUserPage()
      return true
    })
  }

  return (
    <div
      id="socodb-layout"
      style={{
        height: "100vh",
        overflow: "auto",
      }}
    >
      <ProConfigProvider hashed={false}>
        <ConfigProvider
          getTargetContainer={() => {
            return document.getElementById("socodb-layout") || document.body
          }}
        >
          <Routes>
            <Route
              path="*"
              element={
                <ProLayout
                  avatarProps={{
                    src: AccoutImg,
                    size: "small",
                    title: user && user.profile && user.profile.name,
                    render: (_, dom) => {
                      return (
                        <Dropdown
                          menu={{
                            items: [
                              {
                                key: "logout",
                                icon: <LogoutOutlined />,
                                label: "退出登录",
                                onClick: () => {
                                  onLogout()
                                },
                              },
                            ],
                          }}
                        >
                          {dom}
                        </Dropdown>
                      )
                    },
                  }}
                  // actionsRender={(props) => {
                  //   if (props.isMobile) return []
                  //   if (typeof window === 'undefined') return []
                  //   return [
                  //     props.layout !== 'side' && document.body.clientWidth > 1400 ? <SearchInput /> : undefined,

                  //     <SoundOutlined key="InfoCircleFilled" />,
                  //     <QuestionCircleFilled key="QuestionCircleFilled" />,
                  //     // <GithubFilled key="GithubFilled" />,
                  //   ]
                  // }}
                  headerTitleRender={(logo, title) => {
                    const defaultDom = (
                      <a>
                        {logo}
                        {title}
                      </a>
                    )
                    return defaultDom
                  }}
                  menuFooterRender={(props) => {
                    if (props?.collapsed) return undefined
                    return (
                      <div
                        style={{
                          textAlign: "center",
                          paddingBlockStart: 12,
                        }}
                      >
                        <div>© 2023 Made with love</div>
                        <div>by wildgoose</div>
                      </div>
                    )
                  }}
                  onMenuHeaderClick={() => {
                    // console.log(e)
                  }}
                  menuItemRender={(item, dom) => (
                    <div
                      onClick={() => {
                        const path = item.path || "/webcome"
                        navigate(path)
                        setPathname(path)
                      }}
                    >
                      {dom}
                    </div>
                  )}
                  route={filteredRoutes}
                  {...defaultLayoutSettings}
                  location={{
                    pathname,
                  }}
                  {...settings}
                >
                  <Routes>
                    {canAccessRolePage() && (
                      <Route path="/role" element={<RolePage />} />
                    )}
                    {canAccessUserPage() && (
                      <Route path="/user" element={<UserPage />} />
                    )}
                  </Routes>
                </ProLayout>
              }
            />
          </Routes>
        </ConfigProvider>
      </ProConfigProvider>
    </div>
  )
}

export default App
