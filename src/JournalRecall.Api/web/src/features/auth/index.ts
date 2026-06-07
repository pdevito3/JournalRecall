// Public API for the auth feature. Routes (and sanctioned cross-feature consumers) import from here;
// internal modules (`api.ts`) are not re-exported wholesale — only the cross-boundary surface is.
export {
  authConfigQueryOptions,
  meQueryOptions,
  selectIsAdmin,
  selectRoles,
  useAuthConfig,
  useAuthRoles,
  useChangePassword,
  useIsAdmin,
  useLogin,
  useLogout,
  useMe,
  useRegister,
  useSetup,
} from './useAuth'
export { authKeys } from './keys'
export type { AuthConfig, AuthUser, Credentials } from './api'
