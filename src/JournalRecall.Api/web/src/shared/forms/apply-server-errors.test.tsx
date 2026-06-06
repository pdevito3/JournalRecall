import { act } from 'react'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { type AnyFormApi, useForm } from '@tanstack/react-form'
import { applyServerErrors } from './apply-server-errors'
import { ProblemError } from '@/shared/api/problem'

/**
 * Render a tiny form and hand its instance back, so each test can call applyServerErrors and assert
 * what a consumer actually observes — the error text rendered under a field and in the banner — never
 * react-form's internal state shape.
 */
function renderForm(onReady: (form: AnyFormApi) => void) {
  function Harness() {
    const form = useForm({ defaultValues: { email: '', password: '' } })
    onReady(form)
    return (
      <form>
        <form.Field name="email">
          {(field) => <p data-testid="email-error">{field.state.meta.errors.join(', ')}</p>}
        </form.Field>
        <form.Field name="password">
          {(field) => <p data-testid="password-error">{field.state.meta.errors.join(', ')}</p>}
        </form.Field>
        <form.Subscribe selector={(state) => state.errors}>
          {(errors) => <p data-testid="banner">{errors.filter(Boolean).join(', ')}</p>}
        </form.Subscribe>
      </form>
    )
  }
  render(<Harness />)
}

function apply(error: unknown): AnyFormApi {
  let form!: AnyFormApi
  renderForm((f) => {
    form = f
  })
  act(() => applyServerErrors(form, error))
  return form
}

describe('applyServerErrors', () => {
  it('routes per-field server errors onto the matching field (PascalCase → camelCase)', () => {
    apply(
      new ProblemError('flattened', {
        errors: { Email: ['Email is already taken.'], Password: ['Password is too short.'] },
      }),
    )

    expect(screen.getByTestId('email-error')).toHaveTextContent('Email is already taken.')
    expect(screen.getByTestId('password-error')).toHaveTextContent('Password is too short.')
    // Every error landed on a field, so the banner stays empty.
    expect(screen.getByTestId('banner')).toHaveTextContent('')
  })

  it('routes an unmatched server key to the form-level banner', () => {
    apply(new ProblemError('flattened', { errors: { UserName: ['That username is reserved.'] } }))

    expect(screen.getByTestId('banner')).toHaveTextContent('That username is reserved.')
    expect(screen.getByTestId('email-error')).toHaveTextContent('')
  })

  it('routes a bare title/detail problem (no errors dict) to the banner', () => {
    apply(new ProblemError('Invalid email or password', { title: 'Invalid email or password' }))

    expect(screen.getByTestId('banner')).toHaveTextContent('Invalid email or password')
    expect(screen.getByTestId('email-error')).toHaveTextContent('')
  })

  it('routes a non-ProblemError to the banner via its message', () => {
    apply(new Error('Network request failed'))

    expect(screen.getByTestId('banner')).toHaveTextContent('Network request failed')
  })

  it('splits matched and unmatched keys between fields and the banner together', () => {
    apply(
      new ProblemError('flattened', {
        errors: { Email: ['Email is already taken.'], Captcha: ['Captcha expired.'] },
      }),
    )

    expect(screen.getByTestId('email-error')).toHaveTextContent('Email is already taken.')
    expect(screen.getByTestId('banner')).toHaveTextContent('Captcha expired.')
  })
})
