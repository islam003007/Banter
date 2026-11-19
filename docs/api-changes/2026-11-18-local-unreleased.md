# Banter — API changes (2026-11-18)

Baseline: `787e3cb` (`origin/main`). The React client lives in the separate
[Banter.Frontend](https://github.com/islam003007/Banter.Frontend) repository.

## API surface

| Method | Route | Auth | Change |
|---|---|---|---|
| GET | `/api/v1/auth/me` | cookie | Current user `{ id, email, displayName, profilePictureUrl }`. |
| GET | `/api/v1/auth/demo-users` | anonymous | Seeded demo roster; only when `Demo:Enabled`; returns an array. |
| POST | `/api/v1/auth/demo-login` | anonymous | `{ userId }`; signs in an allow-listed seeded user; no password; demo mode only. |
| GET | `/api/v1/conversations` | cookie | Adds `unreadCount`; includes conversations with no messages; nullable last-message fields; cursor paging covers empty threads. |
| GET | `/api/v1/conversations/{conversationId}` | cookie | Conversation metadata plus participants with presence; 403 for non-participants. |

## Realtime

- `ReceiveMessage` now carries the persisted message `id`, `content`, `senderDisplayName`,
  `senderProfilePictureUrl`, `conversationId`, and `createdAt`.
- Presence heartbeats remain `Heartbeat` on `/hubs/chat`.

## Configuration

- `Demo:Enabled` is `false` by default and `true` in `appsettings.Development.json`.
- Demo-user allowlist is derived from the embedded seeded `Users.json`; no new password is exposed.
- No database migration for these changes.

## Tests

- Unit tests cover current user, demo allowlist/gating, conversation access, empty-conversation inbox,
  unread counts, pagination, mark-read and realtime payload content. Build and tests pass locally.


## AI bot pass

- Adds `User.IsBot` and migration `20261118044042_AddBotUsers`
  (`identity."Users"."IsBot" boolean NOT NULL DEFAULT false`).
- Seeds bots Ada, Milo and Nova plus an Ahmed↔Ada DM. Bot users have no passwords and are
  excluded from demo-login and standard login.
- Additive response field `isBot` on user search, inbox, conversation participants and messages.
- `ReceiveMessage` adds `isBot`.
- New SignalR client method `ReceiveBotTyping(conversationId, botName)`.
- Human messages in a one-to-one human↔bot conversation enqueue a bot reply. A background service
  waits 1200–1800 ms, emits typing, persists the bot message, updates the conversation, and
  broadcasts it. Bots never reply in groups or to bots. The queue is in-process; no durable retry.
- `IAiChatClient` is the extension point. The default `DemoAiChatClient` is deterministic and
  offline; the three persona prompts are available for a future real model client.
- Bot seed data is Development-only. Added tests cover the reply processor, persona client, bot seed
  data and search flags.


## Group conversation seed expansion

- Added an idempotent Development `GroupDataSeeder` so existing demo databases receive new groups
  without a reset.
- Seeded **Launch Room** (Ahmed, Sara, Mohamed, Nour; 7 messages; 4 unread for Ahmed) and
  **Design Crit** (Ahmed, Nour, Omar, Fatma; 5 messages). Ahmed is also a member of Weekend Trip
  and Backend Team.
- Added more messages to the existing Backend Team and Weekend Trip threads. The seeder resolves
  conversation `lastMessageId` and participant `lastSeenMessageId`.
- Tests now cover group membership, unread state, idempotent insertion and inbox output.
- Banter suite: 57 tests pass.

## Current client shell

- Banter now uses the **Night Signal** identity built around the supplied green speech-bubble B mark: dark forest surfaces, mint/emerald accents, Manrope + IBM Plex Mono, and no cream/paper/serif theme.
- Auth remains a single full-bleed signal board (no split).
- The signed-in workspace restores the useful **permanent conversation rail + main pane** split. The rail becomes a drawer on mobile.
- Group seed is visible in the rail: Launch Room, Design Crit, Backend Team, Weekend Trip. Ahmed is a member of every group; Launch Room has unread messages.
- Chat bubbles, bot/AI badges, presence, details and the welcome activity cards were restyled without changing API behavior.

## Channel desk refinement

- Rebalanced Night Signal: neutral graphite canvas/panels, green reserved for brand, sent messages,
  online and active states, amber unread/typing attention, ice-blue focus/links.
- Conversation rail restyled as a channel index with signal meter, channel numbers, active waveform
  and distinct group/AI treatments.
- Added a transmission status band, channel identity header, uplink sent bubbles, graphite received
  bubbles and a transmit composer.
- Welcome page reframed as a live channel desk with operator/AI desk and recent transmissions.
- No API or data changes.

## Hardening pass (2026-11-19)

No database migration: the schema is unchanged.

### Presence

- `IPresenceService` is now connection-aware: `ConnectAsync`, `DisconnectAsync`, `HeartbeatAsync`,
  `IsOnlineAsync`, and `GetConnectionCountAsync`. `IsOnlineAsync` keeps its query semantics.
- `ChatHub` registers `Context.ConnectionId` on connect/heartbeat and removes it in
  `OnDisconnectedAsync`; closing one tab no longer marks a user offline while another tab is open.
- Redis stores `presence:{userId}` as a hash of connection id → last heartbeat (Unix ms), with a
  two-minute key TTL refreshed by heartbeats. Reads prune per-connection stale fields, and all-stale
  keys are deleted, so a crashed server cannot leave a user online forever.
- The internal `PresenseService` typo was fixed to `PresenceService`.

### Conversations and messages

- Creating a conversation validates that every participant id exists and returns
  `404 Conversations.ParticipantsNotFound` instead of an opaque foreign-key 500.
- Group titles are trimmed, required, and limited to 200 characters; participant lists are deduped
  and limited to 50 ids (excluding the implicit creator).
- The creator is added to the group's participant list. Previously the creator could create a group
  but was not a member of it (could not read or post).
- Direct-conversation reuse now filters `IsGroup == false`, so a group that has shrunk to two
  members is never reused as a DM.
- `SendMessageCommand` now observes the request token for all pre-commit reads/writes. Post-commit
  work (the realtime notification and bot enqueue) deliberately uses `CancellationToken.None`: a
  client disconnect must not suppress consequences of an already-persisted message. Message content
  rejects whitespace-only values.
- Validators are now registered with FluentValidation. The validation pipeline previously received
  an empty `IEnumerable<IValidator<T>>`, so no request validator ran.

### Bots

- `BotReplyQueue.TryEnqueue` logs a warning and returns `false` when the bounded queue is full; it
  never throws or blocks the already-committed send.
- The background worker's per-request failure isolation is extracted and unit tested: one failed
  reply is logged, and the worker continues with the next request.

### Security, config, and search

- Auth cookie: explicit `HttpOnly`, `SameSite=Lax`, 14-day sliding expiry, and
  `SecurePolicy` of `SameAsRequest` in Development / `Always` elsewhere. The existing cookie event
  object is configured in place so Identity's security-stamp validation stays wired.
- Added anonymous `GET /health` (liveness) and `GET /health/ready` (PostgreSQL + Redis; 200/503).
- User search is case-insensitive for display names and emails.
- Fixed internal typos (`DependencyInjection` filenames, `Users.NotAuthenticated`,
  `LeaveConversationCommand.ConversationId`) and stale production comments.

### Integration tests

New `Banter.IntegrationTests` project modeled on FalkElTawfik:

- `WebApplicationFactory<Program>` (enabled by `InternalsVisibleTo` in `Banter.API.csproj`) with
  real Testcontainers PostgreSQL 14 and Redis 8.
- `IntegrationTestBase` + shared collection fixture; parallelization disabled.
- Respawn reset per test, preserving `__EFMigrationsHistory`; the Development seeders are re-run and
  Redis presence state is flushed.
- Helpers for register/login/demo login, authenticated clients, database queries, ProblemDetails
  parsing, cookie extraction, and polling.
- Coverage: auth (register/login/logout/me, validation, 401/403 shape, demo gating, bot login),
  conversations (DM/group, duplicate DM, missing participants, title limits, creator membership,
  inbox/read/detail access, leave), messages (send/validation/non-participant/unread/pagination),
  bots (typing/reply/no group reply), realtime (authenticated SignalR `ReceiveMessage` and
  connection-count presence), search case-insensitivity, health, and migrations/seed.
- Docker unavailable: the factory records the Testcontainers error and integration tests call
  `Assert.Skip` with that reason.

### Commands

```bash
dotnet build Banter.slnx
dotnet test --project Banter.UnitTests/Banter.UnitTests.csproj
dotnet test --project Banter.IntegrationTests/Banter.IntegrationTests.csproj
dotnet test --solution Banter.slnx
```

Both test projects now use xUnit v3 on Microsoft.Testing.Platform; `global.json` selects the runner.
Current local result: 75 unit + 46 integration = 121 passing.
