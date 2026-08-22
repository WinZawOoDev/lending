import { NotFoundException } from '@nestjs/common';
import { Test } from '@nestjs/testing';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import { AccountsService } from './accounts.service';
import { ACCOUNT_EVENTS_EXCHANGE } from './events/account.event';

const mockAccount = {
  id: '5f0b1e2c-3a4d-4b5c-8d9e-0a1b2c3d4e5f',
  name: 'Alice',
  email: 'alice@example.com',
  balance: 100,
  createdAt: new Date(),
  updatedAt: new Date(),
};

describe('AccountsService', () => {
  let service: AccountsService;
  let prisma: {
    account: Record<string, jest.Mock>;
  };
  let amqpConnection: { publish: jest.Mock };

  beforeEach(async () => {
    prisma = {
      account: {
        create: jest.fn().mockResolvedValue(mockAccount),
        findMany: jest.fn().mockResolvedValue([mockAccount]),
        findUnique: jest.fn(),
        update: jest.fn().mockResolvedValue({ ...mockAccount, name: 'Bob' }),
        delete: jest.fn().mockResolvedValue(mockAccount),
      },
    };
    amqpConnection = { publish: jest.fn() };

    const moduleRef = await Test.createTestingModule({
      providers: [
        AccountsService,
        { provide: PrismaService, useValue: prisma },
        { provide: AmqpConnection, useValue: amqpConnection },
      ],
    }).compile();

    service = moduleRef.get(AccountsService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });

  describe('create', () => {
    it('should create an account and publish account.created', async () => {
      const result = await service.create({
        name: 'Alice',
        email: 'alice@example.com',
      });
      expect(prisma.account.create).toHaveBeenCalledWith({
        data: { name: 'Alice', email: 'alice@example.com' },
      });
      expect(result).toEqual(mockAccount);
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.created',
        expect.objectContaining({
          eventType: 'account.created',
          data: { ...mockAccount, balance: String(mockAccount.balance) },
        }),
      );
    });
  });

  describe('findAll', () => {
    it('should return all accounts', async () => {
      expect(await service.findAll()).toEqual([mockAccount]);
    });
  });

  describe('findOne', () => {
    it('should return an account when found', async () => {
      prisma.account.findUnique.mockResolvedValue(mockAccount);
      expect(await service.findOne(mockAccount.id)).toEqual(mockAccount);
    });

    it('should throw NotFoundException when not found', async () => {
      prisma.account.findUnique.mockResolvedValue(null);
      await expect(service.findOne('missing-id')).rejects.toThrow(
        NotFoundException,
      );
    });
  });

  describe('update', () => {
    it('should update and return the account', async () => {
      prisma.account.findUnique.mockResolvedValue(mockAccount);
      const result = await service.update(mockAccount.id, { name: 'Bob' });
      expect(prisma.account.update).toHaveBeenCalledWith({
        where: { id: mockAccount.id },
        data: { name: 'Bob' },
      });
      expect(result).toEqual({ ...mockAccount, name: 'Bob' });
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.updated',
        expect.objectContaining({ eventType: 'account.updated' }),
      );
    });

    it('should throw NotFoundException when account does not exist', async () => {
      prisma.account.findUnique.mockResolvedValue(null);
      await expect(
        service.update('missing-id', { name: 'Bob' }),
      ).rejects.toThrow(NotFoundException);
    });
  });

  describe('remove', () => {
    it('should delete the account when found', async () => {
      prisma.account.findUnique.mockResolvedValue(mockAccount);
      await expect(service.remove(mockAccount.id)).resolves.toBeUndefined();
      expect(prisma.account.delete).toHaveBeenCalledWith({
        where: { id: mockAccount.id },
      });
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.deleted',
        expect.objectContaining({ eventType: 'account.deleted' }),
      );
    });

    it('should throw NotFoundException when account does not exist', async () => {
      prisma.account.findUnique.mockResolvedValue(null);
      await expect(service.remove('missing-id')).rejects.toThrow(
        NotFoundException,
      );
    });
  });
});
