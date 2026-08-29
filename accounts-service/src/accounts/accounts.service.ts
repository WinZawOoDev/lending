import { Injectable, NotFoundException } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import { CorrelationContextService } from '../common/correlation-context.service';
import { AuditService } from '../common/audit/audit.service';
import type { Account } from '../generated/prisma/client';
import { CreateAccountDto } from './dto/create-account.dto';
import { UpdateAccountDto } from './dto/update-account.dto';
import { ACCOUNT_EVENTS_EXCHANGE, AccountEvent } from './events/account.event';

@Injectable()
export class AccountsService {
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
    const account = await this.prisma.account.create({
      data: createAccountDto,
    });
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

  findAll(): Promise<Account[]> {
    return this.prisma.account.findMany();
  }

  async findOne(id: string): Promise<Account> {
    const account = await this.prisma.account.findUnique({ where: { id } });
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
    const account = await this.prisma.account.update({
      where: { id },
      data: updateAccountDto,
    });
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
    await this.prisma.account.delete({ where: { id } });
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

  private async publishEvent(
    eventType: AccountEvent['eventType'],
    account: Account,
  ): Promise<void> {
    const event: AccountEvent = {
      eventId: crypto.randomUUID(),
      eventType,
      occurredAt: new Date(),
      correlationId: this.correlationContext.correlationId,
      data: { ...account, balance: account.balance.toString() },
    };
    await this.amqpConnection.publish(
      ACCOUNT_EVENTS_EXCHANGE,
      eventType,
      event,
    );
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
