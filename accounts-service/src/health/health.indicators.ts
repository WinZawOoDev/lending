import { Injectable } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import {
  HealthIndicatorResult,
  HealthIndicatorService,
} from '@nestjs/terminus';
import { PrismaService } from '../prisma/prisma.service';

const NIL_UUID = '00000000-0000-0000-0000-000000000000';

/**
 * Minimal structural view of the amqplib connection. The full amqplib types
 * are not installed, so we avoid touching the library's typings directly.
 */
interface AmqpChannelLike {
  close(): Promise<void>;
}

interface AmqpConnectionLike {
  connection: {
    createChannel(): Promise<AmqpChannelLike>;
  };
}

@Injectable()
export class DbHealthIndicator {
  constructor(
    private readonly prisma: PrismaService,
    private readonly healthIndicatorService: HealthIndicatorService,
  ) {}

  async check(): Promise<HealthIndicatorResult> {
    const indicator = this.healthIndicatorService.check('database');
    const startedAt = Date.now();
    try {
      await this.prisma.db.orm.public.Account.where({ id: NIL_UUID }).first();
      return indicator.up({ responseTimeMs: Date.now() - startedAt });
    } catch (err) {
      return indicator.down({
        message: err instanceof Error ? err.message : String(err),
      });
    }
  }
}

@Injectable()
export class RabbitHealthIndicator {
  constructor(
    private readonly amqpConnection: AmqpConnection,
    private readonly healthIndicatorService: HealthIndicatorService,
  ) {}

  async check(): Promise<HealthIndicatorResult> {
    const indicator = this.healthIndicatorService.check('rabbitmq');
    try {
      const amqp = this.amqpConnection as unknown as AmqpConnectionLike;
      const channel = await amqp.connection.createChannel();
      await channel.close();
      return indicator.up();
    } catch (err) {
      return indicator.down({
        message: err instanceof Error ? err.message : String(err),
      });
    }
  }
}
