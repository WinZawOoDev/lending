import { Injectable, NestMiddleware } from '@nestjs/common';
import { randomUUID } from 'node:crypto';
import { NextFunction, Request, Response } from 'express';
import { RequestContextService } from './request-context.service';

export const REQUEST_ID_HEADER = 'x-request-id';

@Injectable()
export class RequestIdMiddleware implements NestMiddleware {
  constructor(private readonly context: RequestContextService) {}

  use(req: Request, res: Response, next: NextFunction) {
    const requestId =
      (req.headers[REQUEST_ID_HEADER] as string) || randomUUID();

    res.setHeader(REQUEST_ID_HEADER, requestId);

    this.context.run({ requestId }, next);
  }
}
