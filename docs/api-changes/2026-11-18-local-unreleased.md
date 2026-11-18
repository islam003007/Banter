# Banter — local API changes (not yet in `origin/main`)

Baseline: `787e3cb` (`origin/main`). These changes are local-only and should be reviewed before
push. The demo frontend is in `Banter.Web/`.

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

## Frontend

`Banter.Web/` is a local addition, not an API change: demo/standard auth, inbox, DM/group creation,
chat history, SignalR, presence, read state and responsive layouts. The bot UI adds AI badges,
persona hints, bot-only DM selection, bot message identity and the `ReceiveBotTyping` indicator.

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

