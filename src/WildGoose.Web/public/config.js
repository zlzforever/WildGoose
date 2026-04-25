window.wildgoose = {
  applicationId: '1',
  baseName: '${BASE_PATH}',
  backend: 'http://localhost:5600/api',
  pageSize: 10,
  disablePasswordLogin: true,
  // 标签名称配置，按 "路由路径/组件名.字段名" 格式
  // 例如: user/UserModal.zhiweiTitle 配置用户弹窗中的职位字段名
  labels: {
    "user/UserModal.zhiweiTitle": "分工",
  },
  oidc: {
    authority: 'http://localhost:8099',
    client_id: 'wildgoose-web',
    client_secret: 'secret',
    response_type: 'code',
    redirect_uri: 'http://localhost:5174/signin-redirect-callback',
    silent_redirect_uri: 'http://localhost:5174/signin-silent-callback',
    scope: 'openid profile role wildgoose-api',
    post_logout_redirect_uri: 'http://localhost:5174/signout-callback-oidc',
    accessTokenExpiringNotificationTime: 10,
    automaticSilentRenew: false,
    filterProtocolClaims: true,
    loadUserInfo: true,
    monitorSession: false,
    checkSessionInterval: 36000000,
  },
}
