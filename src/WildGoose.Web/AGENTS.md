# WildGoose.Web — React SPA Frontend

## OVERVIEW

React 19 + Ant Design 5 (ProLayout) SPA for identity management. Two pages: UserPage (admin+org-admin+user-admin) and RolePage (admin only). OIDC auth gate in main.tsx.

## STRUCTURE

```
WildGoose.Web/
├── config/
│   ├── routes.ts               # Sidebar menu (Identity > User, Role)
│   └── layoutSettings.ts       # ProLayout theme config
├── public/
│   └── config.js               # Runtime config (OIDC, backend URL, encryption toggle)
├── src/
│   ├── main.tsx                # OIDC auth gate → React root render
│   ├── App.tsx                 # ProLayout shell + role-based route filtering
│   ├── wildgoose.ts            # Global type declarations (Window config + all DTOs)
│   ├── pages/
│   │   ├── UserPage.tsx        # Org tree + user table + CRUD (admin scoped)
│   │   └── RolePage.tsx        # Role table + assignable roles (superadmin only)
│   ├── components/             # Modal forms: UserModal, RoleModal, OrganizationModal, RoleStatementModal, ChangePasswordModal
│   ├── services/wildgoose/
│   │   ├── api.ts              # All API functions (~283 lines)
│   │   └── typings.d.ts        # API namespace type declarations
│   ├── lib/
│   │   ├── auth.ts             # OIDC UserManager wrapper
│   │   ├── request.ts          # Axios with Bearer token injection + AES encryption
│   │   └── utils.ts            # UUID, AES helpers (CryptoJS)
│   └── iconfont/               # Custom icon component
├── nginx.conf                  # Production SPA serving + gzip
├── docker-entrypoint.sh        # Runtime config.js env var substitution
└── vite.config.ts              # Vite 7 + React plugin + gzip compression
```

## WHERE TO LOOK

| Task | Location |
|------|----------|
| Add new page | `src/pages/` → add to `App.tsx` routes + `config/routes.ts` menu |
| Add API call | `src/services/wildgoose/api.ts` (function) + `src/wildgoose.ts` (type) |
| Modify auth flow | `src/lib/auth.ts` + `public/config.js` (OIDC settings) |
| Modify theme/layout | `config/layoutSettings.ts` |
| Add form modal | `src/components/` — follow UserModal.tsx pattern |
| Change request encryption | `src/lib/request.ts` Axios interceptor |
| Configure for deployment | `public/config.js` + `docker-entrypoint.sh` env var substitution |

## CONVENTIONS

- **No semicolons**, double quotes, 2-space indent, 100-char line width (Prettier)
- **ESLint**: `@typescript-eslint/no-explicit-any` is OFF — `any` type permitted
- **TypeScript**: `strict: true` in dev, `strict: false` in build (intentional relaxation)
- **State**: Local React useState/useEffect only — no Redux/Zustand
- **Auth**: OIDC code flow via oidc-client v1 — main.tsx redirects unauthenticated users
- **Roles**: Read from `user.profile.role` OIDC claims, filter routes in App.tsx
- **API responses**: Auto-unwrapped from `{Code, Success, Data}` envelope by Axios interceptor
- **Runtime config**: `window.wildgoose` object in `public/config.js`, substituted at Docker deploy
- **Encryption**: When `enableEncryption` is true, POST bodies encrypted with AES-ECB (CryptoJS)

## ANTI-PATTERNS

- **`src/lib/userRequest.ts`** — entirely commented out, dead code
- **No code splitting** — all pages bundled together, no lazy loading
- **`yarn.lock` gitignored** — non-reproducible builds
- **`tsconfig.build.json`** relaxes strict mode — type errors may slip through production builds
