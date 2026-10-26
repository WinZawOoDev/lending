import { Injectable, Logger, NotFoundException } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import { Account } from '../prisma/db';
import { CorrelationContextService } from '../common/correlation-context.service';
import { AuditService } from '../common/audit/audit.service';
import { CreateAccountDto } from './dto/create-account.dto';
import { UpdateAccountDto } from './dto/update-account.dto';
import { ACCOUNT_EVENTS_EXCHANGE, AccountEvent } from './events/account.event';

@Injectable()
export class AccountsService {
  private readonly logger = new Logger(AccountsService.name);

  constructor(
    private readonly prisma: PrismaService,
    private readonly amqpConnection: AmqpConnection,
    private readonly correlationContext: CorrelationContextService,
    private readonly auditService: AuditService,
  ) {}

  async create(
    createAccountDto: CreateAccountDto,
    actorId?: string,
  ): Promise<Account> {
    const account = await this.prisma.db.orm.public.Account.create(
      this.toWriteData(createAccountDto),
    );
    await this.publishEvent('account.created', account);
    await this.auditService.record({
      eventType: 'account.created',
      aggregate: 'account',
      aggregateId: account.id,
      actorId,
      correlationId: this.correlationContext.correlationId,
      occurredAt: new Date(),
      before: null,
      after: this.toAuditPayload(account),
    });
    return account;
  }

  async findAll(): Promise<Account[]> {
    return await this.prisma.db.orm.public.Account.all();
  }

  async view(id: string, actorId?: string): Promise<Account> {
    const account = await this.prisma.db.orm.public.Account.where({
      id,
    }).first();
    if (!account) {
      await this.auditService.record({
        eventType: 'account.view.denied',
        aggregate: 'account',
        aggregateId: id,
        actorId,
        correlationId: this.correlationContext.correlationId,
        occurredAt: new Date(),
        before: null,
        after: { reason: 'not_found' },
      });
      throw new NotFoundException(`Account #${id} not found`);
    }
    await this.auditService.record({
      eventType: 'account.viewed',
      aggregate: 'account',
      aggregateId: account.id,
      actorId,
      correlationId: this.correlationContext.correlationId,
      occurredAt: new Date(),
      before: null,
      after: {
        id: account.id,
        email: account.email,
        updatedAt: account.updatedAt,
      },
    });
    return account;
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
    actorId?: string,
  ): Promise<Account> {
    const before = await this.findOne(id);
    const account = await this.prisma.db.orm.public.Account.where({
      id,
    }).update(this.toWriteData(updateAccountDto));
    if (!account) {
      throw new NotFoundException(`Account #${id} not found`);
    }
    await this.publishEvent('account.updated', account);
    await this.auditService.record({
      eventType: 'account.updated',
      aggregate: 'account',
      aggregateId: account.id,
      actorId,
      correlationId: this.correlationContext.correlationId,
      occurredAt: new Date(),
      before: this.toAuditPayload(before),
      after: this.toAuditPayload(account),
    });
    return account;
  }

  async remove(id: string, actorId?: string): Promise<void> {
    const account = await this.findOne(id);
    await this.prisma.db.orm.public.Account.where({ id }).delete();
    await this.publishEvent('account.deleted', account);
    await this.auditService.record({
      eventType: 'account.deleted',
      aggregate: 'account',
      aggregateId: account.id,
      actorId,
      correlationId: this.correlationContext.correlationId,
      occurredAt: new Date(),
      before: this.toAuditPayload(account),
      after: null,
    });
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
    try {
      // The broker queues publishes while disconnected and never settles
      // them, which would hang the request forever — bound the wait and
      // degrade to dropping the event instead.
      await Promise.race([
        this.amqpConnection.publish(ACCOUNT_EVENTS_EXCHANGE, eventType, event),
        new Promise<never>((_, reject) => {
          const timer = setTimeout(
            () => reject(new Error('publish timed out after 2s')),
            2000,
          );
          timer.unref();
        }),
      ]);
    } catch (err) {
      this.logger.warn(
        `Failed to publish ${eventType} event for account ${account.id} (correlation ${this.correlationContext.correlationId ?? 'n/a'}) — event dropped: ${err}`,
      );
    }
  }

  private toAuditPayload(account: Account): Record<string, unknown> {
    return {
      id: account.id,
      name: account.name,
      email: account.email,
      balance: account.balance.toString(),
      createdAt: account.createdAt,
      updatedAt: account.updatedAt,
    };
  }
}
