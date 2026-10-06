# LifeFit AI

LifeFit AI is the operating system for modern fitness businesses: personal coaches, gyms, yoga studios, and CrossFit boxes.

This repository is the production foundation. It contains the backend host, the multi-tenant persistence model, the mobile and web shells, Docker, and CI. Product workflows are not implemented yet.

## Architecture

The backend follows Clean Architecture and vertical slices.

- **Api** hosts HTTP, API versioning, Swagger, Serilog, health checks, and ProblemDetails.
- **Modules** are the home for future use cases. Each module is its own project.
- **Application** registers MediatR and FluentValidation and runs logging and validation behaviors.
- **Domain** holds the skeleton entities.
- **Infrastructure** implements EF Core for SQL Server, including audit stamping and soft-delete filters.
- **SharedKernel** holds `BaseEntity` and the clock abstraction.

Controllers dispatch MediatR requests. They do not contain business logic. A fuller map is in [docs/architecture.md](docs/architecture.md).

```text
LifeFitAI/
├── backend/
│   ├── src/
│   │   ├── Api/
│   │   ├── Application/
│   │   ├── Domain/
│   │   ├── Infrastructure/
│   │   ├── SharedKernel/
│   │   └── Modules/
│   └── tests/
├── database/
├── mobile/
├── web/
├── docs/
└── docker-compose.yml
```

## Tech stack

| Area | Choice |
| --- | --- |
| Backend | ASP.NET Core 9, EF Core 9, SQL Server 2022 |
| Application | MediatR 12, FluentValidation, Serilog, Swagger, API versioning |
| Data | SQL Server 2022, EF Core, Liquibase |
| Mobile | Expo, React Native, TypeScript, React Navigation, TanStack Query, Axios, React Hook Form |
| Web | Next.js, TypeScript, App Router, ESLint, Prettier |
| Delivery | Docker Compose, GitHub Actions |

MediatR stays on 12.x and FluentAssertions stays on 7.x. Later major versions of both are commercially licensed.

JWT issuer, audience, and token lifetimes are present in configuration. Authentication and refresh tokens are not wired.

Redis and Cloudflare R2 are intentionally absent.

## Prerequisites

- .NET SDK 9
- Node.js 22 or newer
- Docker Desktop, when running the full stack

## Run the backend

```powershell
dotnet restore backend/LifeFitAI.sln
dotnet build backend/LifeFitAI.sln --configuration Release
dotnet test backend/LifeFitAI.sln --configuration Release --no-build
dotnet run --project backend/src/Api/LifeFitAI.Api.csproj --launch-profile http
```

The local API listens on `http://localhost:5080`.

| Endpoint | Purpose |
| --- | --- |
| `GET /api/v1/platform` | Foundation status |
| `GET /health/live` | Process liveness |
| `GET /health/ready` | SQL Server readiness |
| `GET /swagger` | OpenAPI UI in Development |

Development configuration does not contain a password. Set `ConnectionStrings__SqlServer` in the environment before `dotnet run`. That connection string is for runtime data access. Docker creates the local database, then the Liquibase service applies `database/changelog`. The API does not run migrations.

```powershell
$env:ConnectionStrings__SqlServer = "Server=localhost,1433;Database=LifeFitAI;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False"
dotnet run --project backend/src/Api/LifeFitAI.Api.csproj --launch-profile http
```

## Docker

Copy `.env.example` to `.env` and set `MSSQL_SA_PASSWORD`.

```powershell
docker compose up --build
```

| Service | URL |
| --- | --- |
| API | `http://localhost:8080` |
| SQL Server | `localhost,1433` |

SQL Server starts first. A one-shot container creates the `LifeFitAI` database. The Liquibase image from `database/` validates and applies the changelog, then exits. The API starts only after that succeeds.

```powershell
docker compose down
```

## Web

```powershell
npm install --prefix web
npm run dev --prefix web
```

The site is served at `http://localhost:3000`.

```powershell
npm run lint --prefix web
npm run build --prefix web
```

## Mobile

The mobile app is an Expo project on React Native.

```powershell
npm install --prefix mobile
npm run start --prefix mobile
npm run typecheck --prefix mobile
```

Set `EXPO_PUBLIC_API_URL` from `mobile/.env.example`. Android emulators use `http://10.0.2.2:8080` to reach an API on the host.

## Development workflow

1. Add a use case under `backend/src/Modules/{Module}/` when a feature is requested.
2. Expose it through a versioned minimal endpoint that only sends a MediatR request.
3. Add a Liquibase changeset under `database/` when the schema changes. Do not add an EF Core migration.
4. Build and test before committing.

Commit messages follow Conventional Commits. GitHub Actions restores, builds, and tests the backend on pull requests.

## Not in this foundation

- Sign-in, refresh tokens, and authorization
- Tenant isolation filters
- Membership, attendance, workout, nutrition, AI, and notification workflows
- Redis and object storage
