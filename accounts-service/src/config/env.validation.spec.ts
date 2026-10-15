import { PLACEHOLDER_JWT_KEY, validateEnv } from './env.validation';

const validConfig = {
  DATABASE_URL: 'postgresql://user:pass@localhost:5432/db',
  RABBITMQ_URL: 'amqp://user:pass@localhost:5672',
  JWT_KEY: 'a'.repeat(32),
  JWT_ISSUER: 'lending-gateway',
  JWT_AUDIENCE: 'lending-services',
};

describe('validateEnv', () => {
  it('should pass for a complete configuration', () => {
    expect(() => validateEnv(validConfig)).not.toThrow();
  });

  it('should fail when required variables are missing', () => {
    expect(() => validateEnv({ ...validConfig, JWT_KEY: undefined })).toThrow(
      /Missing required environment variables: JWT_KEY/,
    );
  });

  it('should fail when JWT_KEY is too short', () => {
    expect(() => validateEnv({ ...validConfig, JWT_KEY: 'short' })).toThrow(
      /at least 32 characters/,
    );
  });

  it('should accept the placeholder key in development', () => {
    expect(() =>
      validateEnv({
        ...validConfig,
        NODE_ENV: 'development',
        JWT_KEY: PLACEHOLDER_JWT_KEY,
      }),
    ).not.toThrow();
  });

  it('should reject the placeholder key outside development', () => {
    expect(() =>
      validateEnv({ ...validConfig, JWT_KEY: PLACEHOLDER_JWT_KEY }),
    ).toThrow(/placeholder/);
  });
});
