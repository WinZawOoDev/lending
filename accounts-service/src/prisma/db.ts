import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import postgres from '@prisma/orm-postgres/runtime';
import type { Contract, FieldOutputTypes } from './contract.d';

export type Account = FieldOutputTypes['public']['Account'];

const contractJson: unknown = JSON.parse(
  readFileSync(join(__dirname, 'contract.json'), 'utf-8'),
);

export function createDb(url: string) {
  return postgres<Contract>({ contractJson, url });
}

export type Db = ReturnType<typeof createDb>;
