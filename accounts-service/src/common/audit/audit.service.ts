import {
  Injectable,
  Logger,
  OnModuleDestroy,
  OnModuleInit,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Client, errors } from '@elastic/elasticsearch';

export interface AuditEntry {
  eventType:
    | 'account.created'
    | 'account.updated'
    | 'account.deleted'
    | 'account.viewed'
    | 'account.view.denied';
  aggregate: 'account';
  aggregateId: string;
  actorId?: string | null;
  correlationId?: string | null;
  occurredAt: Date;
  before?: Record<string, unknown> | null;
  after?: Record<string, unknown> | null;
}

export interface AuditSearchParams {
  aggregateId?: string;
  eventType?: string;
  from?: Date;
  to?: Date;
  page?: number;
  pageSize?: number;
}

const STARTUP_ATTEMPTS = 8;
const RETRY_BASE_DELAY_MS = 1000;
const RETRY_MAX_DELAY_MS = 15000;

@Injectable()
export class AuditService implements OnModuleInit, OnModuleDestroy {
  public static readonly IndexName = 'account-audit';

  private readonly logger = new Logger(AuditService.name);
  private readonly client: Client;

  constructor(configService: ConfigService) {
    const node =
      configService.get<string>('ELASTICSEARCH_URL') ?? 'http://localhost:9200';
    this.client = new Client({ node });
  }

  async onModuleInit(): Promise<void> {
    // The app must boot even when Elasticsearch is unavailable: audit is
    // degraded (entries are dropped and logged) rather than taking the API down.
    for (let attempt = 1; attempt <= STARTUP_ATTEMPTS; attempt++) {
      try {
        await this.ensureIndex();
        return;
      } catch (err) {
        if (attempt === STARTUP_ATTEMPTS) {
          this.logger.error(
            `Elasticsearch unavailable after ${STARTUP_ATTEMPTS} attempts — continuing without the audit index. Audit entries will be dropped until the index is created.`,
          );
          return;
        }

        const delay = Math.min(
          RETRY_BASE_DELAY_MS * 2 ** (attempt - 1),
          RETRY_MAX_DELAY_MS,
        );
        this.logger.warn(
          `Elasticsearch not ready (attempt ${attempt}/${STARTUP_ATTEMPTS}), retrying in ${delay}ms: ${err}`,
        );
        await new Promise((resolve) => setTimeout(resolve, delay));
      }
    }
  }

  async onModuleDestroy(): Promise<void> {
    await this.client.close();
  }

  async record(entry: AuditEntry): Promise<void> {
    const document = {
      ...entry,
      occurredAt: entry.occurredAt.toISOString(),
    };

    if (await this.tryWrite(document)) {
      this.logger.log(
        `Recorded ${entry.eventType} for ${entry.aggregate} ${entry.aggregateId} (correlation ${entry.correlationId ?? 'n/a'})`,
      );
      return;
    }

    this.logger.error(
      `Failed to record audit entry ${entry.eventType} for ${entry.aggregate} ${entry.aggregateId} (correlation ${entry.correlationId ?? 'n/a'}) — audit is degraded, the request continues`,
    );
  }

  private async tryWrite(document: Record<string, unknown>): Promise<boolean> {
    try {
      await this.client.index({
        index: AuditService.IndexName,
        id: crypto.randomUUID(),
        document,
      });
      return true;
    } catch (err) {
      // A missing index is self-healed by recreating it and retrying once.
      if (err instanceof errors.ResponseError && err.statusCode === 404) {
        try {
          await this.ensureIndex();
          await this.client.index({
            index: AuditService.IndexName,
            id: crypto.randomUUID(),
            document,
          });
          this.logger.warn(
            'Audit index was missing — recreated it and flushed the pending entry',
          );
          return true;
        } catch (retryErr) {
          this.logger.error(
            `Audit write to Elasticsearch failed after index recreation: ${retryErr}`,
          );
          return false;
        }
      }

      this.logger.error(`Audit write to Elasticsearch failed: ${err}`);
      return false;
    }
  }

  async search(
    params: AuditSearchParams,
  ): Promise<{ hits: AuditEntry[]; total: number }> {
    const page = Math.max(params.page ?? 1, 1);
    const pageSize = Math.min(Math.max(params.pageSize ?? 20, 1), 100);

    const must: Array<Record<string, unknown>> = [];

    if (params.aggregateId) {
      must.push({ term: { aggregateId: params.aggregateId } });
    }
    if (params.eventType) {
      must.push({ term: { eventType: params.eventType } });
    }
    if (params.from || params.to) {
      const range: Record<string, unknown> = {};
      if (params.from) range.gte = params.from.toISOString();
      if (params.to) range.lte = params.to.toISOString();
      must.push({ range: { occurredAt: range } });
    }

    const query = must.length > 0 ? { bool: { must } } : { match_all: {} };

    const response = await this.client.search({
      index: AuditService.IndexName,
      query,
      sort: [{ occurredAt: { order: 'desc' } }],
      from: (page - 1) * pageSize,
      size: pageSize,
    });

    const hits = response.hits.hits.map((hit) => hit._source as AuditEntry);
    const total =
      typeof response.hits.total === 'number'
        ? response.hits.total
        : (response.hits.total?.value ?? 0);

    return { hits, total };
  }

  private async ensureIndex(): Promise<void> {
    const exists = await this.client.indices.exists({
      index: AuditService.IndexName,
    });

    if (exists) {
      return;
    }

    await this.client.indices.create({
      index: AuditService.IndexName,
      mappings: {
        properties: {
          eventType: { type: 'keyword' },
          aggregate: { type: 'keyword' },
          aggregateId: { type: 'keyword' },
          actorId: { type: 'keyword' },
          correlationId: { type: 'keyword' },
          occurredAt: { type: 'date' },
          before: { type: 'object', enabled: false },
          after: { type: 'object', enabled: false },
        },
      },
    });

    this.logger.log(`Created Elasticsearch index ${AuditService.IndexName}`);
  }
}
