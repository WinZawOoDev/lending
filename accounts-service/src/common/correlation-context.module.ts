import { Global, Module } from '@nestjs/common';
import { CorrelationContextService } from './correlation-context.service';

@Global()
@Module({
  providers: [CorrelationContextService],
  exports: [CorrelationContextService],
})
export class CorrelationContextModule {}
