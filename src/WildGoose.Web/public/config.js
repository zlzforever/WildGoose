var authority = 'https://sample.xxxx.cc/sts';
window.wildgoose = {
  applicationId: '1',
  baseName: '${BASE_PATH}',
  backend: 'http://localhost:5600/api',
  pageSize: 10,
  enableEncryption: true,
  disablePasswordLogin: false,
  gateway: {
    enabled: false,  // true when deployed behind a gateway that handles OIDC
  },
  oidc: {
    // 1. 保留 authority（作为 issuer 标识），但禁用发现
    authority: authority,
    // 关键配置：禁用自动发现
    disableDiscovery: true,
    // 2. 手动指定所有核心端点（必须从 IdP 文档获取准确地址）
    metadata: {
      // 发行者标识（必须与 authority 一致）
      issuer: authority,
      // 授权码请求地址
      authorization_endpoint: authority + '/connect/authorize',
      // Token 获取地址 (code换token, refresh token)
      token_endpoint: authority + '/connect/token',
      // 用户信息获取地址
      userinfo_endpoint: authority + '/connect/userinfo',
      // 注销重定向地址
      end_session_endpoint: authority + '/connect/endsession',
      // JWT 签名公钥地址 (必配，用于验证 Token)
      jwks_uri: authority + '/.well-known/jwks',
      // 支持的响应类型
      response_types_supported: [
        "code",
        "token",
        "id_token",
        "id_token token",
        "code id_token",
        "code token",
        "code id_token token"
      ],
      // 支持的签名算法
      id_token_signing_alg_values_supported: ['RS256']
    },
    // 3. 【关键】把公钥直接写在这里
    // 替换成你自己的 RSA 公钥（JWK 格式）
    signingKeys: [
      {
        "kty": "RSA",
        "use": "sig",
        "alg": "RS256",
        "n": "qIeW8xgwoWuQWvX45b4MaJPBtchHuPwojFukw93zaRlcqAebN6BOemX2NILBP4nKy0ZuCt6tnjyE7iJcQIXeSTW_YEygo7x-WbANPm7GKebmy2cL_Xd7AMc5Ex8S1tP_f67ya13noGHxb1MBlwGq7VIISagq-uybOJmSlQJVQXQQUUcMwi2PKUAJLErOXOjhBHcHuvS5wKS4eOVf_4YgGcPtG36jCNtLlM0hh-miaT-FCcH14Rp7sRSFFckX2bYPrXqKH7PVCD2UFTGyJD-jdBnKE1lrLHbl2gz-dng5GmrMzg_c3CFQ2H52LRJoBw11atxCWxSHpRYJFGUDCf8TvQ",
        "e": "AQAB"
      }
    ],
    client_id: 'sample-app',
    client_secret: 'secret',
    response_type: 'code',
    redirect_uri: 'http://localhost:5174/signin-redirect-callback',
    silent_redirect_uri: 'http://localhost:5174/signin-silent-callback',
    scope: 'openid profile role sample-wildgoose-api',
    post_logout_redirect_uri: 'http://localhost:5174/signout-callback-oidc',
    accessTokenExpiringNotificationTime: 10,
    automaticSilentRenew: false,
    filterProtocolClaims: true,
    loadUserInfo: true,
    monitorSession: false,
    checkSessionInterval: 36000000,
  },
}
