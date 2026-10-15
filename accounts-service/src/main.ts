import { Logger, ValidationPipe } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap(): Promise<void> {
  const app = await NestFactory.create(AppModule);
  const configService = app.get(ConfigService);

  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      transform: true,
    }),
  );
  app.enableShutdownHooks();

  await app.listen(configService.get<number>('PORT') ?? 3000);
}

bootstrap().catch((err) => {
  new Logger('bootstrap').error(
    'Failed to start accounts-service',
    err as Error,
  );
  process.exitCode = 1;
});
