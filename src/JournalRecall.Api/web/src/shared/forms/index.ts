/** The shared forms pattern (PRD-0004 / ADR-0007): compose these into each form. */
export { CheckboxField, SelectField, TextField, type SelectOption } from './fields'
export { FormShell } from './form-shell'
export { applyServerErrors } from './apply-server-errors'
export { emailSchema, passwordSchema, passwordsMatch, PASSWORD_MIN_LENGTH } from './schema'
