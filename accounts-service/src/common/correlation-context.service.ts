import { Injectable } from '@nestjs/common';
import { AsyncLocalStorage } from 'node:async_hooks';

export interface CorrelationContext {
  correlationId: string;
}

@Injectable()
export class CorrelationContextService {
  private readonly als = new AsyncLocalStorage<CorrelationContext>();

  run<T>(context: CorrelationContext, callback: () => T): T {
    return this.als.run(context, callback);
  }

  get correlationId(): string | undefined {
    return this.als.getStore()?.correlationId;
  }
}
