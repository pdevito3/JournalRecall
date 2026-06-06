/** The shared forms pattern (PRD-0004 / ADR-0007): compose these into each form. */
export { CheckboxField, SelectField, TextField, type SelectOption } from './fields'
export { Form, FormShell } from './form-shell'
export { useFormContext } from './form-context'
export { applyServerErrors } from './apply-server-errors'
export {
  usernameSchema,
  passwordSchema,
  passwordsMatch,
  PASSWORD_MIN_LENGTH,
  USERNAME_MIN_LENGTH,
  USERNAME_MAX_LENGTH,
} from './schema'
