export interface AccountEventPayload {
  id: string;
  name: string;
  email: string;
  /**
   * Prisma Decimal serializes to a string over the wire.
   */
  balance: number | string;
  createdAt: Date;
  updatedAt: Date;
}

export interface AccountEvent {
  eventId: string;
  eventType: 'account.created' | 'account.updated' | 'account.deleted';
  occurredAt: Date;
  requestId?: string;
  data: Partial<AccountEventPayload>;
}

export const ACCOUNT_EVENTS_EXCHANGE = 'lending.events';
