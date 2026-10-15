import { Controller, Get } from '@nestjs/common';
import { HealthCheck, HealthCheckService } from '@nestjs/terminus';
import { DbHealthIndicator, RabbitHealthIndicator } from './health.indicators';

@Controller('health')
export class HealthController {
  constructor(
    private health: HealthCheckService,
    private readonly dbIndicator: DbHealthIndicator,
    private readonly rabbitIndicator: RabbitHealthIndicator,
  ) {}

  @Get()
  @HealthCheck()
  check() {
    return this.health.check([
      () => this.dbIndicator.check(),
      () => this.rabbitIndicator.check(),
    ]);
  }
}
