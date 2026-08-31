# Lending Platform

A polyglot microservices workspace for a lending domain, consisting of a **YARP API gateway**, an **accounts-service** and a **loans-service**, backed by PostgreSQL and Elasticsearch, orchestrated with Docker Compose.

## Architecture

```
                    ┌───────────────────────┐
   client ──:8080──▶│  gateway-service      │
                    │  (YARP, .NET 9)       │
                    └──────────┬────────────┘
              ┌────────────────┼────────────────┐
              ▼ /accounts/*                     ▼ /loans/*
     ┌─────────────────┐               ┌────────────────┐
     │ accounts-service │               │  loans-service │
     │   (NestJS :3000) │               │ (.NET 9 :8080) │
     └────────┬────────┘               └───────┬────────┘
              │                                │
              └───────────────┬────────────────┘
                     ▼
        ┌─────────────────────────┐
        │     lending-network     │
        ├─────────────────────────┤
        │ postgres    pgadmin     │
        │ elasticsearch           │
        │ rabbitmq                │
        └─────────────────────────┘
```

## Tech Stack

### gateway-service (`/gateway-service`)
| Technology | Version | Purpose |
|---|---|---|
| YARP | 2.3.0 | Reverse proxy / API gateway |
| ASP.NET Core | 9.0 | Host runtime |

Routes: `/accounts/*` → accounts-service, `/loans/*` → loans-service (prefix stripped before forwarding). Route config lives in `gateway-service/appsettings.json`.

### accounts-service (`/accounts-service`)
| Technology | Version | Purpose |
|---|---|---|
| NestJS | 11.x | Application framework |
| TypeScript | 5.7.x | Language |
| Prisma ORM | 7.9 | Database access (PostgreSQL, driver adapter `@prisma/adapter-pg`) |
| Node.js | 22 (alpine) | Runtime |
| Jest + Supertest | 30 / 7 | Unit & e2e testing |
| ESLint + Prettier | 9 / 3 | Linting & formatting |

### loans-service (`/loans-service`)
| Technology | Version | Purpose |
|---|---|---|
| ASP.NET Core | 9.0 | Web API framework |
| C# | 13 | Language |
| EF Core + Npgsql | 9.0 | ORM & PostgreSQL provider (migrations in `Migrations/`) |
| RabbitMQ.Client | 7.2 | Event consumption |
| Elastic.Clients.Elasticsearch | 8.19 | Search projection |
| OpenAPI | 9.0.16 | API documentation |

### Infrastructure
| Technology | Image | Port | Notes |
|---|---|---|---|
| PostgreSQL | `postgres:17-alpine` | 5432 | Primary datastore, user/pass/db: `lending` |
| pgAdmin | `dpage/pgadmin4:latest` | **5050** | Login: `admin@lending.local` / `admin` |
| Elasticsearch | `elasticsearch:8.15.0` | 9200 | Single-node, security disabled, 512m heap |
| RabbitMQ | `rabbitmq:4-management` | 5672 / **15672** | AMQP broker; management UI login: `lending` / `lending` |

All containers communicate over the shared bridge network `lending-network`.

### Data storage

PostgreSQL and pgAdmin persist their data on the host filesystem under `./data/` (gitignored) instead of named volumes:

| Service | Host path | Container path | Notes |
|---|---|---|---|
| postgres | `./data/postgres` | `/var/lib/postgresql/data` | Ownership/permissions are fixed automatically by the image entrypoint |
| pgadmin | `./data/pgadmin` | `/var/lib/pgadmin` | The container runs as uid `5050`; make the directory writable once before first start |

One-time setup on Linux (required for pgAdmin):

```sh
sudo chown -R 5050:5050 data/pgadmin
```

Elasticsearch and RabbitMQ still use named volumes (`elasticsearch-data`, `rabbitmq-data`).

## Getting Started

### Prerequisites
- Node.js ≥ 22 and npm
- .NET SDK ≥ 9.0
- Docker & Docker Compose
- On Linux, Elasticsearch requires:
  ```sh
  sudo sysctl -w vm.max_map_count=262144
  ```

### Run everything with Docker (development)

```sh
docker compose up --build
```

This starts the full stack in development mode: each service runs its hot-reload watcher (`nest start --watch` for accounts-service, `dotnet watch run` for the .NET services) against bind-mounted source directories (`./accounts-service`, `./loans-service`, `./gateway-service`). Edits are picked up without rebuilding images. Container-only paths (`node_modules`, `src/generated`, `dist`, `bin`, `obj`) are kept isolated in anonymous volumes.

Database schema is applied automatically on startup: loans-service runs EF Core migrations itself, while a one-shot `accounts-db-setup` container applies the Prisma schema (`npx prisma migrate deploy`) before accounts-service starts.

### Production build

Each service has a separate production image next to its dev `Dockerfile`:

```sh
docker build -f gateway-service/Dockerfile.prod -t gateway-service:prod gateway-service
docker build -f accounts-service/Dockerfile.prod -t accounts-service:prod accounts-service
docker build -f loans-service/Dockerfile.prod -t loans-service:prod loans-service
```

These build a Release/publish image (no watch mode, no source bind mounts).

| Endpoint | URL |
|---|---|
| API gateway | http://localhost:8080 |
| accounts-service (direct) | http://localhost:3000 |
| loans-service (direct) | http://localhost:5120 |
| pgAdmin | http://localhost:5050 |
| Elasticsearch | http://localhost:9200 |

### Local development (without Docker)

Start infrastructure only:

```sh
docker compose up postgres pgadmin elasticsearch
```

Then run each service:

```sh
# accounts-service
cd accounts-service
npm install
npx prisma generate        # regenerate the Prisma client (gitignored)
npx prisma migrate dev     # apply schema to the database
npm run start:dev          # watches src/, listens on PORT ?? 3000

# loans-service
cd loans-service
dotnet run                 # http profile on :5120, https on :7004
```

Connection strings are already wired in `docker-compose.yml` — when running locally, point the services at `localhost` instead of container hostnames.

## Accounts API

Available through the gateway (`:8080`) or directly (`:3000`):

| Method | Route | Description |
|---|---|---|
| POST | `/accounts` | Create an account |
| GET | `/accounts` | List all accounts |
| GET | `/accounts/:id` | Get one account |
| PATCH | `/accounts/:id` | Partially update an account |
| DELETE | `/accounts/:id` | Delete an account (204) |

Body fields: `name` (string, required), `email` (email, required), `balance` (number ≥ 0, optional).

## Eventing

accounts-service publishes domain events to the RabbitMQ topic exchange **`lending.events`**:

| Routing key | Trigger | Payload |
|---|---|---|
| `account.created` | POST /accounts | `{ eventId, eventType, occurredAt, data }` |
| `account.updated` | PATCH /accounts/:id | same envelope |
| `account.deleted` | DELETE /accounts/:id | same envelope |

Connection is configured via `RABBITMQ_URL` (default local dev: `amqp://lending:lending@localhost:5672`).

**Consumers:**
- loans-service runs `AccountEventsConsumer` (a .NET `BackgroundService`): durable queue `loans-service.account-events` bound with `account.*`, manual ack, automatic reconnect with 5s retry. Configure via `RabbitMQ:Uri` / `RabbitMQ__Uri`.
- Received events are projected into Elasticsearch: `account.created`/`account.updated` upsert documents into the `accounts` index, `account.deleted` removes them. Configure via `Elasticsearch:Uri` / `Elasticsearch__Uri`. Query the projection with:
  ```sh
  curl http://localhost:9200/accounts/_search | jq
  ```

## Correlation ID

Requests are traced end-to-end via the **`x-correlation-id`** HTTP header:

- gateway-service generates one when absent and forwards it to both backend services.
- accounts-service and loans-service accept it (generating one otherwise), echo it in every response, attach it to log scopes/messages, and loans-service also honors it from consumed RabbitMQ events (`correlationId` field of the event envelope).

## Loans API

Available through the gateway (`:8080`) or directly (`:5120`):

| Method | Route | Description |
|---|---|---|
| POST | `/loans` | Create a loan (status starts at `Pending`) |
| GET | `/loans` | List all loans |
| GET | `/loans/:id` | Get one loan |
| PATCH | `/loans/:id` | Update loan status (`Pending`, `Active`, `Paid`, `Defaulted`) |
| DELETE | `/loans/:id` | Delete a loan (204) |

Create body: `accountId` (string, required), `principal` (number > 0), `interestRate` (0–100), `termMonths` (1–360). Schema is managed by EF Core migrations (auto-applied on startup).

## Development Guidelines

### General
- Keep services independently deployable; no shared code between services.
- Never commit secrets — use environment variables (see `docker-compose.yml`).
- Follow security best practices; do not log sensitive data.

### accounts-service (NestJS)
- Generate modules/controllers/services with the CLI:
  ```sh
  nest g resource <name>
  ```
- Schema changes go through `prisma/schema.prisma` — edit the model, then:
  ```sh
  npx prisma migrate dev --name <change>   # create + apply migration
  ```
- The generated Prisma client (`src/generated/`) is gitignored — run `npx prisma generate` after install or schema changes.
- Co-locate unit tests as `<name>.spec.ts` next to the source file.
- Commands:
  ```sh
  npm run lint        # eslint --fix
  npm run format      # prettier
  npm test            # jest unit tests
  npm run test:e2e    # supertest e2e
  ```

### loans-service (ASP.NET Core)
- Follow the default Web API controller pattern (`Controllers/`).
- Schema changes: update `Models/` + `Data/LoansDbContext.cs`, then:
  ```sh
  dotnet ef migrations add <Name>
  ```
- Migrations apply automatically on startup (`Database.MigrateAsync()` in `Program.cs`); to apply manually: `dotnet ef database update`.
- Nullable reference types are enabled — handle nullability explicitly.
- Test HTTP requests via `loans-service.http`.
- Build & test:
  ```sh
  dotnet build
  dotnet watch run   # hot reload during development
  ```

### Git workflow
- Commit early, commit small; write concise imperative commit messages.
- `main` tracks `origin/main`; keep it always green.

## Project Structure

```
lending/
├── docker-compose.yml       # Dev stack orchestration (hot reload)
├── gateway-service/         # YARP API gateway (.NET 9)
│   ├── appsettings.json     # Proxy routes & clusters
│   ├── Dockerfile           # Development image
│   └── Dockerfile.prod      # Production image
├── accounts-service/        # NestJS service
│   ├── prisma/              # Prisma schema & migrations
│   ├── src/                 # Application code (incl. generated Prisma client)
│   ├── test/                # e2e tests
│   ├── Dockerfile           # Development image
│   └── Dockerfile.prod      # Production image
└── loans-service/           # ASP.NET Core service
    ├── Controllers/         # API controllers
    ├── Program.cs
    ├── Dockerfile           # Development image
    └── Dockerfile.prod      # Production image
```
