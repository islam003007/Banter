# Banter

A real-time chat API built with ASP.NET Core, SignalR, PostgreSQL, and Redis.

Banter demonstrates modern backend engineering practices including Clean Architecture, CQRS, distributed infrastructure, structured observability, and real-time communication.

## Features
* Cookie-based authentication with ASP.NET Identity
* Direct and group conversations
* Real-time message notifications using SignalR
* Online/offline presence tracking
* Cursor pagination
* Structured error handling with Problem Details
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

### Result Pattern + Problem Details

Expected business failures are returned through a Result Pattern, while unexpected failures are handled centrally and exposed through standardized Problem Details responses.

### Cursor Pagination

Conversation endpoints use cursor pagination to provide stable ordering and better scalability than offset-based pagination.

## Running Locally
1. Copy .env.example to .env and provide secure values.
2. Start the application:
```
docker compose up -d
```
### Services

| Service  | URL |
| ------------- |:-------------:|
| API           | http://localhost:8080|
| Swagger       | http://localhost:8080/swagger|
| Seq           | http://localhost:5431

## Testing

The unit test project covers cursor pagination, inbox/conversation queries, demo auth, realtime
payloads, and the shared Result/Error contracts without requiring PostgreSQL or Redis:

```bash
dotnet test Banter.UnitTests/Banter.UnitTests.csproj
```

## Frontend

The React client lives in a separate repository:
[Banter.Frontend](https://github.com/islam003007/Banter.Frontend) (`Banter.Web/`, port 5173).

The demo-login endpoints are enabled only when `Demo:Enabled` is true; it is true in
`appsettings.Development.json` and false by default elsewhere.

## AI bot chats

Three seeded AI contacts — **Ada**, **Milo** and **Nova** — appear in user search with an AI badge.
Start a direct conversation with one and send a message. A hosted background worker emits a typing
event, generates a persona-aware reply (offline demo brain by default), persists it as the bot and
delivers it over SignalR. Bots only reply in one-to-one chats and never to other bots.

Bots have `IsBot=true`, no passwords, and are excluded from standard/demo login. The migration
`20261118044042_AddBotUsers` is applied automatically on startup in Development. The API change log
at `docs/api-changes/2026-11-18-local-unreleased.md` records the bot schema, additive fields and
SignalR event.

The reply worker is in-process and intended for the demo; production should use a durable queue.


## Third-Party Libraries

This project uses MediatR, which requires a commercial license for production use.
This repository is intended for educational and portfolio purposes only.