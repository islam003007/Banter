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
The inbox welcome now includes recent-thread cards, live stats and an AI desk; session copy was
cleaned of cookie, user-id and SignalR implementation details. No API shape changed.

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
