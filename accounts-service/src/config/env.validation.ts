const REQUIRED_ENV_VARS = [
  'DATABASE_URL',
  'RABBITMQ_URL',
  'JWT_KEY',
  'JWT_ISSUER',
  'JWT_AUDIENCE',
] as const;

export const PLACEHOLDER_JWT_KEY =
  'replace-me-with-a-secret-at-least-32-characters-long';

export function validateEnv(
  config: Record<string, unknown>,
): Record<string, unknown> {
  const missing = REQUIRED_ENV_VARS.filter((key) => {
    const value: unknown = config[key];
    return typeof value !== 'string' || value.trim() === '';
  });
  if (missing.length > 0) {
    throw new Error(
      `Missing required environment variables: ${missing.join(', ')}. ` +
        'Copy .env.example to .env and fill in the values.',
    );
  }

  const jwtKey = String(config.JWT_KEY);
  if (jwtKey.length < 32) {
    throw new Error('JWT_KEY must be at least 32 characters long.');
  }
  if (config.NODE_ENV !== 'development' && jwtKey === PLACEHOLDER_JWT_KEY) {
    throw new Error(
      'JWT_KEY is still the placeholder value. Replace it with a strong secret outside development.',
    );
  }

  return config;
}
