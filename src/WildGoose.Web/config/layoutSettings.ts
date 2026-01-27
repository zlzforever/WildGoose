import { ProLayoutProps } from '@ant-design/pro-layout'

/**
 * @name
 */
const Settings: ProLayoutProps & {
  pwa?: boolean
  logo?: string
} = {
  title: '权限管理',
  prefixCls: 'wildgoose-web',
  fixSiderbar: true,
  layout: 'mix',
  splitMenus: false,
  iconfontUrl: '//at.alicdn.com/t/c/font_4325784_47jwv7zybd6.js',
  waterMarkProps: {
    content: '',
  },
  navTheme: 'light',
  // 拂晓蓝
  colorPrimary: '#1890ff',

  contentWidth: 'Fluid',
  fixedHeader: false,
  colorWeak: false,
  siderMenuType: 'group',
  pwa: true,
  logo: 'https://gw.alipayobjects.com/zos/rmsportal/KDpgvguMpGfqaHPjicRK.svg',
  token: {
    // 参见ts声明，demo 见文档，通过token 修改样式
    //https://procomponents.ant.design/components/layout#%E9%80%9A%E8%BF%87-token-%E4%BF%AE%E6%94%B9%E6%A0%B7%E5%BC%8F
    header: {
      colorBgMenuItemSelected: 'rgba(0,0,0,0.04)',
    },
  },
  menu: {
    collapsedShowGroupTitle: true,
  },
}

export default Settings
