import { Injectable, OnModuleDestroy, OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { createDb, Db } from './db';

@Injectable()
export class PrismaService implements OnModuleInit, OnModuleDestroy {
  readonly db: Db;

  constructor(configService: ConfigService) {
    this.db = createDb(configService.getOrThrow<string>('DATABASE_URL'));
  }

  async onModuleInit(): Promise<void> {
    await this.db.connect();
  }

  async onModuleDestroy(): Promise<void> {
    await this.db.close();
  }
}
