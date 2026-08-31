export interface AccountEventPayload {
  id: string;
  name: string;
  email: string;
  /**
   * Decimal columns are read as decimal strings.
   */
  balance: string;
  createdAt: string;
  updatedAt: string;
}

export interface AccountEvent {
  eventId: string;
  eventType: 'account.created' | 'account.updated' | 'account.deleted';
  occurredAt: Date;
  correlationId?: string;
  data: Partial<AccountEventPayload>;
}

export const ACCOUNT_EVENTS_EXCHANGE = 'lending.events';
