/**
 * Role issued by the gateway on every access token. Services must reject
 * tokens that do not carry it.
 */
export const LENDING_API_ROLE = 'lending.api';

/**
 * Claim names under which the gateway may encode roles. .NET's
 * JwtSecurityTokenHandler maps `ClaimTypes.Role` to the short `role` claim
 * on write; the full URI form is accepted for robustness.
 */
export const ROLE_CLAIM_KEYS = [
  'role',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
] as const;
