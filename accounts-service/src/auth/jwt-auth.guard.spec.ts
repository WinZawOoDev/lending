import { ForbiddenException, UnauthorizedException } from '@nestjs/common';
import { JwtAuthGuard } from './jwt-auth.guard';
import { LENDING_API_ROLE } from './auth.constants';

jest.mock('@nestjs/jwt', () => ({
  JwtService: class JwtService {},
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { JwtService } = require('@nestjs/jwt') as {
  JwtService: new () => unknown;
};

const ROLE_CLAIM = 'role';

function createGuard(verifyResult: unknown): {
  guard: JwtAuthGuard;
  verifyAsync: jest.Mock;
} {
  const verifyAsync = jest.fn().mockResolvedValue(verifyResult);
  const jwtService = new JwtService() as { verifyAsync: jest.Mock };
  jwtService.verifyAsync = verifyAsync;
  return { guard: new JwtAuthGuard(jwtService as never), verifyAsync };
}

function createContext(headers: Record<string, string | undefined>): {
  switchToHttp: () => {
    getRequest: () => {
      headers: Record<string, string | undefined>;
      user?: unknown;
    };
  };
} {
  const request: {
    headers: Record<string, string | undefined>;
    user?: unknown;
  } = {
    headers,
  };
  return {
    switchToHttp: () => ({ getRequest: () => request }),
  };
}

describe('JwtAuthGuard', () => {
  it('should reject requests without a bearer token', async () => {
    const { guard } = createGuard({});
    await expect(guard.canActivate(createContext({}) as never)).rejects.toThrow(
      UnauthorizedException,
    );
  });

  it('should reject invalid tokens', async () => {
    const { guard, verifyAsync } = createGuard({});
    verifyAsync.mockRejectedValue(new Error('bad token'));
    await expect(
      guard.canActivate(
        createContext({ authorization: 'Bearer nope' }) as never,
      ),
    ).rejects.toThrow(UnauthorizedException);
  });

  it('should reject tokens without the lending.api role', async () => {
    const { guard } = createGuard({ sub: 'client-1' });
    await expect(
      guard.canActivate(
        createContext({ authorization: 'Bearer valid' }) as never,
      ),
    ).rejects.toThrow(ForbiddenException);
  });

  it('should accept tokens carrying the lending.api role and expose the payload', async () => {
    const payload = { sub: 'client-1', [ROLE_CLAIM]: [LENDING_API_ROLE] };
    const { guard } = createGuard(payload);
    const context = createContext({ authorization: 'Bearer valid' }) as never;

    await expect(guard.canActivate(context)).resolves.toBe(true);
  });

  it('should reject string payloads', async () => {
    const { guard } = createGuard('not-an-object');
    await expect(
      guard.canActivate(
        createContext({ authorization: 'Bearer valid' }) as never,
      ),
    ).rejects.toThrow(UnauthorizedException);
  });
});
