window.wildgoose = {
  applicationId: '1',
  baseName: '${BASE_PATH}',
  backend: 'http://localhost:5181/api',
  pageSize: 10,
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