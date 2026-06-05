import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { AuthForm } from '@/features/auth/components/auth-form'
import { useAuthConfig, useLogin } from '@/features/auth/useAuth'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()
  const { data: config } = useAuthConfig()

  return (
    <AuthForm
      title="Sign in"
      submitLabel="Sign in"
      pending={login.isPending}
      error={login.error?.message}
      onSubmit={(credentials) => login.mutate(credentials, { onSuccess: () => navigate({ to: '/' }) })}
      footer={
        // The "create an account" link appears only when the operator has opened self-registration.
        config?.selfRegistrationEnabled ? (
          <>
            New here?{' '}
            <Link to="/register" className="text-accent hover:underline">
              Create an account
            </Link>
          </>
        ) : null
      }
    />
  )
}
