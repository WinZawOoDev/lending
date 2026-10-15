import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { JwtPayload } from 'jsonwebtoken';
import type { Request } from 'express';
import { LENDING_API_ROLE, ROLE_CLAIM_KEYS } from './auth.constants';

export interface AuthenticatedRequest extends Request {
  user?: JwtPayload;
}

@Injectable()
export class JwtAuthGuard implements CanActivate {
  constructor(private readonly jwtService: JwtService) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const request = context.switchToHttp().getRequest<AuthenticatedRequest>();
    const token = this.extractToken(request);

    if (!token) {
      throw new UnauthorizedException('Missing bearer token.');
    }

    const payload = await this.verifyToken(token);

    if (!this.hasRequiredRole(payload)) {
      throw new ForbiddenException(
        `Token is missing the '${LENDING_API_ROLE}' role.`,
      );
    }

    request.user = payload;
    return true;
  }

  private async verifyToken(token: string): Promise<JwtPayload> {
    let payload: JwtPayload | string;
    try {
      payload = await this.jwtService.verifyAsync<JwtPayload>(token);
    } catch {
      throw new UnauthorizedException('Invalid or expired token.');
    }

    if (typeof payload === 'string' || typeof payload !== 'object') {
      throw new UnauthorizedException('Invalid token payload.');
    }

    return payload;
  }

  private hasRequiredRole(payload: JwtPayload): boolean {
    return ROLE_CLAIM_KEYS.some((claimKey) => {
      const claim: unknown = payload[claimKey];
      if (typeof claim === 'string') {
        return claim === LENDING_API_ROLE;
      }
      return (
        Array.isArray(claim) && (claim as unknown[]).includes(LENDING_API_ROLE)
      );
    });
  }

  private extractToken(request: Request): string | undefined {
    const [type, token] = request.headers.authorization?.split(' ') ?? [];
    return type === 'Bearer' ? token : undefined;
  }
}
