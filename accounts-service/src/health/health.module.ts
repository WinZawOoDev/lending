import { Module } from '@nestjs/common';
import { TerminusModule } from '@nestjs/terminus';
import { HealthController } from './health.controller';
import { DbHealthIndicator, RabbitHealthIndicator } from './health.indicators';
import { MessagingModule } from '../messaging/messaging.module';

@Module({
  imports: [TerminusModule, MessagingModule],
  controllers: [HealthController],
  providers: [DbHealthIndicator, RabbitHealthIndicator],
})
export class HealthModule {}
