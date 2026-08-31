import { Injectable, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { createDb, Db } from './db';

@Injectable()
export class PrismaService implements OnModuleInit, OnModuleDestroy {
  readonly db: Db;

  constructor(configService: ConfigService) {
    const url =
      configService.get<string>('DATABASE_URL') ??
      'postgresql://lending:lending@localhost:5432/lending';
    this.db = createDb(url);
  }

  async onModuleInit(): Promise<void> {
    await this.db.connect();
  }

  async onModuleDestroy(): Promise<void> {
    await this.db.close();
  }
}
