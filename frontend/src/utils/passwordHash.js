const PASSWORD_PREFIX = 'tjuer'

export async function hashPassword(password) {
  const data = new TextEncoder().encode(`${PASSWORD_PREFIX}${password}`)
  const digest = await crypto.subtle.digest('SHA-256', data)
  return Array.from(new Uint8Array(digest))
    .map(byte => byte.toString(16).padStart(2, '0'))
    .join('')
}
