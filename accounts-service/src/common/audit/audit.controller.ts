import {
  BadRequestException,
  Controller,
  DefaultValuePipe,
  Get,
  ParseIntPipe,
  ParseUUIDPipe,
  Query,
  UseGuards,
} from '@nestjs/common';
import { AuditService, AuditEntry } from './audit.service';
import { JwtAuthGuard } from '../../auth/jwt-auth.guard';
import { RolesGuard } from '../../auth/roles.guard';
import { Roles } from '../../auth/roles.decorator';

@Controller('audit')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles('admin')
export class AuditController {
  constructor(private readonly auditService: AuditService) {}

  @Get()
  async search(
    @Query('aggregateId', new ParseUUIDPipe({ optional: true }))
    aggregateId?: string,
    @Query('eventType') eventType?: string,
    @Query('from') from?: string,
    @Query('to') to?: string,
    @Query('page', new DefaultValuePipe(1), ParseIntPipe) page: number = 1,
    @Query('pageSize', new DefaultValuePipe(20), ParseIntPipe)
    pageSize: number = 20,
  ): Promise<{ hits: AuditEntry[]; total: number }> {
    return this.auditService.search({
      aggregateId,
      eventType,
      from: this.parseDate('from', from),
      to: this.parseDate('to', to),
      page,
      pageSize,
    });
  }

  private parseDate(name: string, value?: string): Date | undefined {
    if (value === undefined) {
      return undefined;
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      throw new BadRequestException(`${name} must be a valid ISO 8601 date`);
    }
    return date;
  }
}
