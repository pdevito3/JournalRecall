import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { AuthForm } from '@/features/auth/components/auth-form'
import { useLogin } from '@/features/auth/useAuth'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()

  return (
    <AuthForm
      title="Sign in"
      submitLabel="Sign in"
      pending={login.isPending}
      error={login.error?.message}
      onSubmit={(credentials) => login.mutate(credentials, { onSuccess: () => navigate({ to: '/' }) })}
      footer={
        <>
          New here?{' '}
          <Link to="/register" className="text-accent hover:underline">
            Create an account
          </Link>
        </>
      }
    />
  )
}
