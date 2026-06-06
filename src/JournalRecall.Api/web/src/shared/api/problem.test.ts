import { describe, expect, it } from 'vitest'
import { ProblemError, problemError } from './problem'

/** A minimal Response stand-in whose json() resolves to the given body (or throws for opaque bodies). */
function jsonResponse(body: unknown): Response {
  return { json: () => Promise.resolve(body) } as unknown as Response
}

function opaqueResponse(): Response {
  return { json: () => Promise.reject(new SyntaxError('Unexpected token')) } as unknown as Response
}

describe('problemError', () => {
  it('keeps the parsed problem and flattens the errors dict into .message', async () => {
    const body = {
      type: 'https://httpstatuses.io/400',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        Email: ['Email is already taken.'],
        Password: ['Password is too short.', 'Password needs a digit.'],
      },
    }

    const error = await problemError(jsonResponse(body), 'Could not create user')

    expect(error).toBeInstanceOf(ProblemError)
    expect(error.problem).toEqual(body)
    expect(error.problem?.errors?.Email).toEqual(['Email is already taken.'])
    // Flattened fallback joins every field message, preserving the old problem() behavior.
    expect(error.message).toBe('Email is already taken. Password is too short. Password needs a digit.')
  })

  it('falls back to title (then detail) when there is no errors dict', async () => {
    const titleOnly = await problemError(jsonResponse({ title: 'This instance has already been set up.' }), 'Setup failed')
    expect(titleOnly.problem).toEqual({ title: 'This instance has already been set up.' })
    expect(titleOnly.message).toBe('This instance has already been set up.')

    const detailOnly = await problemError(jsonResponse({ detail: 'The provider endpoint is unreachable.' }), 'Save failed')
    expect(detailOnly.message).toBe('The provider endpoint is unreachable.')
  })

  it('uses the supplied fallback for a non-problem / opaque body', async () => {
    const opaque = await problemError(opaqueResponse(), 'Login failed')
    expect(opaque.problem).toBeNull()
    expect(opaque.message).toBe('Login failed')

    // A JSON object with nothing problem-shaped still falls back, but preserves the parsed body.
    const noisy = await problemError(jsonResponse({ foo: 'bar' }), 'Login failed')
    expect(noisy.problem).toEqual({ foo: 'bar' })
    expect(noisy.message).toBe('Login failed')
  })
})
