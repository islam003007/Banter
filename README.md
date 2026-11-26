# Banter

A real-time chat API built with ASP.NET Core, SignalR, PostgreSQL, and Redis.

Banter demonstrates modern backend engineering practices including Clean Architecture, CQRS, distributed infrastructure, structured observability, and real-time communication.

## Features
* Cookie-based authentication with ASP.NET Identity
* Direct and group conversations
* Real-time message notifications using SignalR
* Connection-aware online/offline presence tracking
* Cursor pagination
* Structured error handling with Problem Details
* Health and readiness endpoints
* Centralized logging with Serilog and Seq
* Dockerized development environment
## Architecture

The project follows Clean Architecture with CQRS implemented through MediatR.
```
Banter.API
Banter.Application
Banter.Domain
Banter.Infrastructure
Banter.SharedKernel
```
Business logic is implemented through command and query handlers, while Minimal API endpoints remain thin and focused on request handling.

## Tech Stack
* ASP.NET Core 10
* Entity Framework Core
* PostgreSQL
* ASP.NET Identity
* SignalR
* Redis
* MediatR
* FluentValidation
* Serilog
* Seq
* Docker & Docker Compose
## Notable Engineering Decisions
### Redis for Multiple Distributed Concerns

Redis is used for:

* Presence tracking
* SignalR backplane
* Data Protection key storage

This allows multiple application instances to share real-time state, authentication keys, and messaging infrastructure.

Presence is connection-based rather than a single per-user flag. Each SignalR connection is a field
in a Redis hash (`presence:{userId}`) carrying its last heartbeat, with a two-minute TTL and a
25-second client heartbeat. Multiple tabs keep a user online until the last connection disconnects;
stale fields left by a crashed instance are pruned when presence is read.

### Cookie Security

The auth cookie is explicit about its transport settings: `HttpOnly`, `SameSite=Lax`, a
14-day sliding expiry, and `Secure` in production (`SameAsRequest` locally so HTTP development
still works). Identity's security-stamp validation is preserved by configuring the existing cookie
events instead of replacing them.

### Result Pattern + Problem Details

Expected business failures are returned through a Result Pattern, while unexpected failures are handled centrally and exposed through standardized Problem Details responses.

### Cursor Pagination

Conversation endpoints use cursor pagination to provide stable ordering and better scalability than offset-based pagination.

### Case-insensitive Search

User search is case-insensitive for display names and emails. The query lives in the Application
layer, so it lower-cases both sides (Npgsql translates this to `lower(column) LIKE lower(pattern)`)
rather than taking a PostgreSQL-only `ILike` dependency that the EF in-memory unit tests cannot
translate. The integration tests exercise the real PostgreSQL SQL.

## Running Locally
1. Copy `.env.example` to `.env` and provide the database/Redis secrets plus
   `DATA_SEED_USERS_PASSWORD`. Replace the placeholder; a fresh seed fails if
   the value is missing or invalid.
2. Start the application:
```
docker compose up -d
```

`DATA_SEED_USERS_PASSWORD` is a public demo credential used only for the seeded
accounts. It is never a production secret. It must have at least 8 characters,
uppercase, lowercase, a digit, and a non-alphanumeric character. Do not reuse it
outside a disposable demo database.

Changing this value does not update users that were already seeded. Reset the
demo database or let the enabled demo-reset service reseed it.

### Demo reset and rate limiting

- `Demo:Reset:Enabled` is off by default. When `Demo:Enabled` and
  `Demo:Reset:Enabled` are both true at startup, a background service wipes the
  demo rows and re-runs the seeders every `Demo:Reset:IntervalHours` hours.
- Only enable the reset service on a dedicated demo database. It is not a
  production maintenance job.
- `RateLimit` applies a per-IP fixed window (default: 120 requests per minute).
  It can be disabled or tuned through `RateLimit__Enabled`,
  `RateLimit__PermitLimit`, and `RateLimit__WindowSeconds`.
### Services

| Service  | URL |
| ------------- |:-------------:|
| API           | http://localhost:8080|
| Swagger       | http://localhost:8080/swagger|
| Seq           | http://localhost:5431|

## Testing

Both test projects use xUnit v3 on the Microsoft.Testing.Platform runner (`global.json` selects the
runner; use `--project`/`--solution` with `dotnet test`).

Fast tests (no Docker):

```bash
dotnet test --project Banter.UnitTests/Banter.UnitTests.csproj
```

Integration tests use real Testcontainers PostgreSQL and Redis, apply the EF migrations, and run the
Development seeders. Respawn resets the database between tests while preserving
`__EFMigrationsHistory`, then the seeders are re-run; Redis presence state is flushed. The suite
covers auth, conversations, messages, bots, search, health, SignalR messages/presence, and a
migration/seed smoke test. Docker must be available:

```bash
dotnet test --project Banter.IntegrationTests/Banter.IntegrationTests.csproj
```

Run everything:

```bash
dotnet test --solution Banter.slnx
```

If Docker/Testcontainers is unavailable, the integration tests report a skip with the Docker error
instead of failing with a misleading connection error.

### Health checks

`GET /health` is a dependency-free liveness probe. `GET /health/ready` checks PostgreSQL and Redis
and returns `200` when both are healthy or `503` with per-dependency status when they are not.

## Public demo credentials

The seeded demo users share a password supplied by
`DataSeed:UsersPassword` / `DATA_SEED_USERS_PASSWORD`. It is intentionally
public for this disposable portfolio demo. The one-click roster does not need
it, and the password must never be reused for a real account or deployment.
No real user data is present; demo data may be reset.

## Frontend

The React client lives in a separate repository:
[Banter.Frontend](https://github.com/islam003007/Banter.Frontend) (`Banter.Web/`, port 5173).

The demo-login endpoints are enabled only when `Demo:Enabled` is true; it is true in
`appsettings.Development.json` and false by default elsewhere.

CORS is environment-only. Set the exact frontend origin (no trailing slash) with
`Frontend__AllowedOrigins__0=https://<frontend-origin>`. An empty list denies
cross-origin requests rather than allowing everything.

## AI bot chats

AI bot chats are disabled by default (`Bots:Enabled=false`). When enabled, three seeded AI
contacts — **Ada**, **Milo** and **Nova** — appear in user search with an AI badge. Start a direct
conversation with one and send a message. A hosted background worker emits a typing event,
generates a persona-aware reply (offline demo brain by default), persists it as the bot and
delivers it over SignalR. Bots only reply in one-to-one chats and never to other bots.

Bots have `IsBot=true`, no passwords, and are excluded from standard/demo login. The migration
`20261118044042_AddBotUsers` is applied automatically on startup in Development. The API change log
at `docs/api-changes/2026-11-18-local-unreleased.md` records the bot schema, additive fields and
SignalR event.

The reply worker is in-process and intended for the demo; production should use a durable queue.


## Third-Party Libraries

This project uses MediatR, which requires a commercial license for production use.
This repository is intended for educational and portfolio purposes only.
