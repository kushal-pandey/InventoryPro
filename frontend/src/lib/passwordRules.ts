export interface PasswordRule {
  id: 'length' | 'uppercase' | 'lowercase' | 'number' | 'symbol'
  label: string
  valid: boolean
}

export function getPasswordRules(password: string): PasswordRule[] {
  return [
    { id: 'length', label: 'At least 12 characters', valid: password.length >= 12 },
    { id: 'uppercase', label: 'An uppercase letter', valid: /\p{Lu}/u.test(password) },
    { id: 'lowercase', label: 'A lowercase letter', valid: /\p{Ll}/u.test(password) },
    { id: 'number', label: 'A number', valid: /\p{Nd}/u.test(password) },
    { id: 'symbol', label: 'A symbol', valid: /[^\p{L}\p{N}]/u.test(password) },
  ]
}
