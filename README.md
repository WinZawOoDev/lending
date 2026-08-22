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
        └─────────────────────────┘
```

## Tech Stack

### accounts-service (`/accounts-service`)
| Technology | Version | Purpose |
|---|---|---|
| NestJS | 11.x | Application framework |
| TypeScript | 5.7.x | Language |
| Node.js | 22 (alpine) | Runtime |
| Jest + Supertest | 30 / 7 | Unit & e2e testing |
| ESLint + Prettier | 9 / 3 | Linting & formatting |

### loans-service (`/loans-service`)
| Technology | Version | Purpose |
|---|---|---|
| ASP.NET Core | 9.0 | Web API framework |
| C# | 13 | Language |
| OpenAPI | 9.0.16 | API documentation |

### gateway-service (`/gateway-service`)
| Technology | Version | Purpose |
|---|---|---|
| YARP | 2.3.0 | Reverse proxy / API gateway |
| ASP.NET Core | 9.0 | Host runtime |

Routes: `/accounts/*` → accounts-service, `/loans/*` → loans-service (prefix stripped before forwarding). Route config lives in `gateway-service/appsettings.json`.

### Infrastructure
| Technology | Image | Port | Notes |
|---|---|---|---|
| PostgreSQL | `postgres:17-alpine` | 5432 | Primary datastore, user/pass/db: `lending` |
| pgAdmin | `dpage/pgadmin4:latest` | **5050** | Login: `admin@lending.local` / `admin` |
| Elasticsearch | `elasticsearch:8.15.0` | 9200 | Single-node, security disabled, 512m heap |

All containers communicate over the shared bridge network `lending-network`.

## Getting Started

### Prerequisites
- Node.js ≥ 22 and npm
- .NET SDK ≥ 9.0
- Docker & Docker Compose
- On Linux, Elasticsearch requires:
  ```sh
  sudo sysctl -w vm.max_map_count=262144
  ```

### Run everything with Docker

```sh
docker compose up --build
```

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
npm run start:dev          # watches src/, listens on PORT ?? 3000

# loans-service
cd loans-service
dotnet run                 # http profile on :5120, https on :7004
```

Connection strings are already wired in `docker-compose.yml` — when running locally, point the services at `localhost` instead of container hostnames.

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
├── docker-compose.yml       # Full stack orchestration
├── gateway-service/         # YARP API gateway (.NET 9)
│   ├── appsettings.json     # Proxy routes & clusters
│   └── Dockerfile
├── accounts-service/        # NestJS service
│   ├── src/                 # Application code
│   ├── test/                # e2e tests
│   └── Dockerfile
└── loans-service/           # ASP.NET Core service
    ├── Controllers/         # API controllers
    ├── Program.cs
    └── Dockerfile
```
