import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import type { Request } from 'express';
import { ROLE_CLAIM_KEYS } from './auth.constants';
import type { JwtUser } from './jwt-auth.guard';
import { ROLES_KEY } from './roles.decorator';

@Injectable()
export class RolesGuard implements CanActivate {
  constructor(private readonly reflector: Reflector) {}

  canActivate(context: ExecutionContext): boolean {
    const requiredRoles = this.reflector.getAllAndOverride<string[]>(
      ROLES_KEY,
      [context.getHandler(), context.getClass()],
    );

    if (!requiredRoles?.length) {
      return true;
    }

    const request = context.switchToHttp().getRequest<Request>();
    const user: JwtUser | undefined = request.user;

    const userRoles = ROLE_CLAIM_KEYS.flatMap((claimKey) => {
      const claim: unknown = user?.[claimKey];
      if (typeof claim === 'string') {
        return [claim];
      }
      return Array.isArray(claim) ? (claim as string[]) : [];
    });

    const hasRole = requiredRoles.some((role) => userRoles.includes(role));
    if (!hasRole) {
      throw new ForbiddenException(
        `Requires one of the following roles: ${requiredRoles.join(', ')}.`,
      );
    }

    return true;
  }
}
