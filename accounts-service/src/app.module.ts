import { Module, MiddlewareConsumer, NestModule } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { PrismaModule } from './prisma/prisma.module';
import { AccountsModule } from './accounts/accounts.module';
import { HealthModule } from './health/health.module';
import { CorrelationIdMiddleware } from './common/correlation-id.middleware';
import { CorrelationContextModule } from './common/correlation-context.module';
import { AuthModule } from './auth/auth.module';
import { MessagingModule } from './messaging/messaging.module';
import { validateEnv } from './config/env.validation';

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true, validate: validateEnv }),
    PrismaModule,
    CorrelationContextModule,
    MessagingModule,
    AuthModule,
    AccountsModule,
    HealthModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule implements NestModule {
  configure(consumer: MiddlewareConsumer) {
    consumer.apply(CorrelationIdMiddleware).forRoutes('*');
  }
}
