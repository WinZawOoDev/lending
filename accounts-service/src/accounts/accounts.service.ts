import { Injectable, NotFoundException } from '@nestjs/common';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import type { Account } from '../generated/prisma/client';
import { CreateAccountDto } from './dto/create-account.dto';
import { UpdateAccountDto } from './dto/update-account.dto';
import { ACCOUNT_EVENTS_EXCHANGE, AccountEvent } from './events/account.event';

@Injectable()
export class AccountsService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly amqpConnection: AmqpConnection,
  ) {}

  async create(createAccountDto: CreateAccountDto): Promise<Account> {
    const account = await this.prisma.account.create({
      data: createAccountDto,
    });
    await this.publishEvent('account.created', account);
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
  ): Promise<Account> {
    await this.findOne(id);
    const account = await this.prisma.account.update({
      where: { id },
      data: updateAccountDto,
    });
    await this.publishEvent('account.updated', account);
    return account;
  }

  async remove(id: string): Promise<void> {
    const account = await this.findOne(id);
    await this.prisma.account.delete({ where: { id } });
    await this.publishEvent('account.deleted', account);
  }

  private async publishEvent(
    eventType: AccountEvent['eventType'],
    account: Account,
  ): Promise<void> {
    const event: AccountEvent = {
      eventId: crypto.randomUUID(),
      eventType,
      occurredAt: new Date(),
      data: { ...account, balance: account.balance.toString() },
    };
    await this.amqpConnection.publish(
      ACCOUNT_EVENTS_EXCHANGE,
      eventType,
      event,
    );
  }
}
