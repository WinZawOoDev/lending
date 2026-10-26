import { Module } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { RabbitMQModule } from '@golevelup/nestjs-rabbitmq';
import { ACCOUNT_EVENTS_EXCHANGE } from '../accounts/events/account.event';

@Module({
  imports: [
    RabbitMQModule.forRootAsync({
      inject: [ConfigService],
      useFactory: (configService: ConfigService) => ({
        uri: configService.getOrThrow<string>('RABBITMQ_URL'),
        exchanges: [
          {
            name: ACCOUNT_EVENTS_EXCHANGE,
            type: 'topic',
          },
        ],
        defaultPublishOptions: {
          persistent: true,
        },
        // Connect in the background so the API can start (and serve traffic)
        // while the broker is down; publishes no-op until it returns.
        connectionInitOptions: { wait: false, timeout: 10000, retries: 5 },
      }),
    }),
  ],
  exports: [RabbitMQModule],
})
export class MessagingModule {}
