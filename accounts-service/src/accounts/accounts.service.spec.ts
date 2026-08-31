import { NotFoundException } from '@nestjs/common';
import { Test } from '@nestjs/testing';
import { AmqpConnection } from '@golevelup/nestjs-rabbitmq';
import { PrismaService } from '../prisma/prisma.service';
import { CorrelationContextService } from '../common/correlation-context.service';
import { AccountsService } from './accounts.service';
import { ACCOUNT_EVENTS_EXCHANGE } from './events/account.event';

jest.mock('../prisma/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

const mockAccount = {
  id: '5f0b1e2c-3a4d-4b5c-8d9e-0a1b2c3d4e5f',
  name: 'Alice',
  email: 'alice@example.com',
  balance: '100',
  createdAt: '2024-01-01T00:00:00.000Z',
  updatedAt: '2024-01-01T00:00:00.000Z',
};

describe('AccountsService', () => {
  let service: AccountsService;
  let collection: {
    first: jest.Mock;
    update: jest.Mock;
    delete: jest.Mock;
  };
  let prisma: {
    db: {
      orm: {
        public: {
          Account: {
            create: jest.Mock;
            all: jest.Mock;
            where: jest.Mock;
          };
        };
      };
    };
  };
  let amqpConnection: { publish: jest.Mock };

  beforeEach(async () => {
    collection = {
      first: jest.fn(),
      update: jest.fn(),
      delete: jest.fn(),
    };
    prisma = {
      db: {
        orm: {
          public: {
            Account: {
              create: jest.fn().mockResolvedValue(mockAccount),
              all: jest.fn().mockResolvedValue([mockAccount]),
              where: jest.fn().mockReturnValue(collection),
            },
          },
        },
      },
    };
    amqpConnection = { publish: jest.fn() };

    const moduleRef = await Test.createTestingModule({
      providers: [
        AccountsService,
        { provide: PrismaService, useValue: prisma },
        { provide: AmqpConnection, useValue: amqpConnection },
        CorrelationContextService,
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
      expect(prisma.db.orm.public.Account.create).toHaveBeenCalledWith({
        name: 'Alice',
        email: 'alice@example.com',
      });
      expect(result).toEqual(mockAccount);
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.created',
        expect.objectContaining({
          eventType: 'account.created',
          data: { ...mockAccount },
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
      collection.first.mockResolvedValue(mockAccount);
      expect(await service.findOne(mockAccount.id)).toEqual(mockAccount);
      expect(prisma.db.orm.public.Account.where).toHaveBeenCalledWith({
        id: mockAccount.id,
      });
    });

    it('should throw NotFoundException when not found', async () => {
      collection.first.mockResolvedValue(null);
      await expect(service.findOne('missing-id')).rejects.toThrow(
        NotFoundException,
      );
    });
  });

  describe('update', () => {
    it('should update and return the account', async () => {
      collection.first.mockResolvedValue(mockAccount);
      collection.update.mockResolvedValue({ ...mockAccount, name: 'Bob' });
      const result = await service.update(mockAccount.id, { name: 'Bob' });
      expect(prisma.db.orm.public.Account.where).toHaveBeenCalledWith({
        id: mockAccount.id,
      });
      expect(collection.update).toHaveBeenCalledWith({ name: 'Bob' });
      expect(result).toEqual({ ...mockAccount, name: 'Bob' });
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.updated',
        expect.objectContaining({ eventType: 'account.updated' }),
      );
    });

    it('should throw NotFoundException when account does not exist', async () => {
      collection.first.mockResolvedValue(null);
      await expect(
        service.update('missing-id', { name: 'Bob' }),
      ).rejects.toThrow(NotFoundException);
    });
  });

  describe('remove', () => {
    it('should delete the account when found', async () => {
      collection.first.mockResolvedValue(mockAccount);
      collection.delete.mockResolvedValue(mockAccount);
      await expect(service.remove(mockAccount.id)).resolves.toBeUndefined();
      expect(prisma.db.orm.public.Account.where).toHaveBeenCalledWith({
        id: mockAccount.id,
      });
      expect(collection.delete).toHaveBeenCalled();
      expect(amqpConnection.publish).toHaveBeenCalledWith(
        ACCOUNT_EVENTS_EXCHANGE,
        'account.deleted',
        expect.objectContaining({ eventType: 'account.deleted' }),
      );
    });

    it('should throw NotFoundException when account does not exist', async () => {
      collection.first.mockResolvedValue(null);
      await expect(service.remove('missing-id')).rejects.toThrow(
        NotFoundException,
      );
    });
  });
});
