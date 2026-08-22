import { Module } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { RabbitMQModule } from '@golevelup/nestjs-rabbitmq';
import { AccountsController } from './accounts.controller';
import { AccountsService } from './accounts.service';
import { ACCOUNT_EVENTS_EXCHANGE } from './events/account.event';

@Module({
  imports: [
    RabbitMQModule.forRootAsync({
      inject: [ConfigService],
      useFactory: (configService: ConfigService) => ({
        uri:
          configService.get<string>('RABBITMQ_URL') ??
          'amqp://lending:lending@localhost:5672',
        exchanges: [
          {
            name: ACCOUNT_EVENTS_EXCHANGE,
            type: 'topic',
          },
        ],
      }),
    }),
  ],
  controllers: [AccountsController],
  providers: [AccountsService],
})
export class AccountsModule {}
