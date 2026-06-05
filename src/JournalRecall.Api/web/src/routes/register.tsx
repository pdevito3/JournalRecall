import { createFileRoute, Link, useNavigate } from '@tanstack/react-router'
import { AuthForm } from '@/features/auth/components/auth-form'
import { useLogin, useRegister } from '@/features/auth/useAuth'

export const Route = createFileRoute('/register')({
  component: RegisterPage,
})

function RegisterPage() {
  const navigate = useNavigate()
  const register = useRegister()
  const login = useLogin()

  return (
    <AuthForm
      title="Create your account"
      submitLabel="Create account"
      confirmPassword
      pending={register.isPending || login.isPending}
      error={register.error?.message ?? login.error?.message}
      onSubmit={(credentials) =>
        register.mutate(credentials, {
          // Registration doesn't sign you in — log in immediately so the cookie is set.
          onSuccess: () => login.mutate(credentials, { onSuccess: () => navigate({ to: '/' }) }),
        })
      }
      footer={
        <>
          Already have an account?{' '}
          <Link to="/login" className="text-accent hover:underline">
            Sign in
          </Link>
        </>
      }
    />
  )
}
