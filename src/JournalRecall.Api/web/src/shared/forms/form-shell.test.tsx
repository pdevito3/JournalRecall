import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { z } from 'zod'
import { useForm } from '@tanstack/react-form'
import { FormShell } from './form-shell'
import { TextField } from './fields'
import { applyServerErrors } from './apply-server-errors'
import { ProblemError } from '@/shared/api/problem'

function LoginHarness({ onSubmit }: { onSubmit: (values: { email: string }) => void | Promise<void> }) {
  const form = useForm({
    defaultValues: { email: '' },
    validators: { onBlur: z.object({ email: z.email('Enter a valid email address.') }) },
    onSubmit: async ({ value }) => {
      try {
        await onSubmit(value)
      } catch (error) {
        applyServerErrors(form, error)
      }
    },
  })
  return (
    <FormShell form={form} title="Sign in" submitLabel="Sign in" footer="Footer text">
      <form.Field name="email">{(field) => <TextField field={field} label="Email" type="email" />}</form.Field>
    </FormShell>
  )
}

describe('FormShell', () => {
  it('renders the title, footer, and a submit button', () => {
    render(<LoginHarness onSubmit={() => {}} />)
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByText('Footer text')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('disables the submit button once the form is invalid', async () => {
    const user = userEvent.setup()
    render(<LoginHarness onSubmit={() => {}} />)

    await user.type(screen.getByLabelText('Email'), 'bad')
    await user.tab()

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeDisabled()
  })

  it('surfaces a non-field server error in the top-level banner', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn(() => {
      throw new ProblemError('Invalid email or password', { title: 'Invalid email or password' })
    })
    render(<LoginHarness onSubmit={onSubmit} />)

    await user.type(screen.getByLabelText('Email'), 'me@example.com')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent('Invalid email or password')
  })
})
