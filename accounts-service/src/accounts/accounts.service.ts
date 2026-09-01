import { Injectable, NotFoundException } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import { Account } from '../prisma/db';
import { CorrelationContextService } from '../common/correlation-context.service';
import { CreateAccountDto } from './dto/create-account.dto';
import { UpdateAccountDto } from './dto/update-account.dto';
import { ACCOUNT_EVENTS_EXCHANGE, AccountEvent } from './events/account.event';

@Injectable()
export class AccountsService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly amqpConnection: AmqpConnection,
    private readonly correlationContext: CorrelationContextService,
  ) {}

  async create(createAccountDto: CreateAccountDto): Promise<Account> {
    const account = await this.prisma.db.orm.public.Account.create(
      this.toWriteData(createAccountDto),
    );
    await this.publishEvent('account.created', account);
    return account;
  }

  async findAll(): Promise<Account[]> {
    return await this.prisma.db.orm.public.Account.all();
  }

  async findOne(id: string): Promise<Account> {
    const account = await this.prisma.db.orm.public.Account.where({
      id,
    }).first();
    if (!account) {
      throw new NotFoundException(`Account #${id} not found`);
    }
    return account;
  }

  async update(
    id: string,
    updateAccountDto: UpdateAccountDto,
  ): Promise<Account> {
    await this.findOne(id);
    const account = await this.prisma.db.orm.public.Account.where({
      id,
    }).update(this.toWriteData(updateAccountDto));
    if (!account) {
      throw new NotFoundException(`Account #${id} not found`);
    }
    await this.publishEvent('account.updated', account);
    return account;
  }

  async remove(id: string): Promise<void> {
    const account = await this.findOne(id);
    await this.prisma.db.orm.public.Account.where({ id }).delete();
    await this.publishEvent('account.deleted', account);
  }

  private toWriteData<T extends { balance?: number }>(
    dto: T,
  ): Omit<T, 'balance'> & { balance?: string } {
    const { balance, ...rest } = dto;
    return balance === undefined
      ? rest
      : { ...rest, balance: balance.toString() };
  }

  private async publishEvent(
    eventType: AccountEvent['eventType'],
    account: Account,
  ): Promise<void> {
    const event: AccountEvent = {
      eventId: crypto.randomUUID(),
      eventType,
      occurredAt: new Date(),
      correlationId: this.correlationContext.correlationId,
      data: {
        ...account,
        createdAt: new Date(account.createdAt).toISOString(),
        updatedAt: new Date(account.updatedAt).toISOString(),
      },
    };
    await this.amqpConnection.publish(
      ACCOUNT_EVENTS_EXCHANGE,
      eventType,
      event,
    );
  }
}
