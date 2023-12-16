import { ProConfigProvider } from '@ant-design/pro-provider'
import './App.css'
import { ConfigProvider, Dropdown } from 'antd'
import { Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import ProLayout, { ProSettings } from '@ant-design/pro-layout'
import { LogoutOutlined } from '@ant-design/icons'
import { useState } from 'react'
import defaultLayoutSettings from '../config/layoutSettings'
import routes from '../config/routes'
import RolePage from './pages/RolePage'
import UserPage from './pages/UserPage'

function App() {
  const navigate = useNavigate()
  const location = useLocation()
  const [settings] = useState<Partial<ProSettings>>({
    fixSiderbar: true,
    layout: 'mix',
    splitMenus: false,
  })
  const [pathname, setPathname] = useState(location.pathname)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any

  if (typeof document === 'undefined') {
    return <div />
  }
  return (
    <div
      id="socodb-layout"
      style={{
        height: '100vh',
        overflow: 'auto',
      }}>
      <ProConfigProvider hashed={false}>
        <ConfigProvider
          getTargetContainer={() => {
            return document.getElementById('socodb-layout') || document.body
          }}>
          <Routes>
            <Route
              path="*"
              element={
                <ProLayout
                  avatarProps={{
                    src: 'https://gw.alipayobjects.com/zos/antfincdn/efFD%24IOql2/weixintupian_20170331104822.jpg',
                    size: 'small',
                    title: '周正',
                    render: (_, dom) => {
                      return (
                        <Dropdown
                          menu={{
                            items: [
                              {
                                key: 'logout',
                                icon: <LogoutOutlined />,
                                label: '退出登录',
                              },
                            ],
                          }}>
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
                          textAlign: 'center',
                          paddingBlockStart: 12,
                        }}>
                        <div>© 2023 Made with love</div>
                        <div>by wildgoods</div>
                      </div>
                    )
                  }}
                  onMenuHeaderClick={(e) => {
                    console.log(e)
                  }}
                  menuItemRender={(item, dom) => (
                    <div
                      onClick={() => {
                        const path = item.path || '/webcome'
                        navigate(path)
                        setPathname(path)
                      }}>
                      {dom}
                    </div>
                  )}
                  route={routes}
                  {...defaultLayoutSettings}
                  location={{
                    pathname,
                  }}
                  {...settings}>
                  <Routes>
                    <Route path="/role" element={<RolePage></RolePage>} />
                    <Route path="/user" element={<UserPage></UserPage>} />
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
