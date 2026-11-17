# Banter Demo Frontend — Design

## Intent
A low-friction portfolio demo of Banter's real chat API. It must feel like a finished
product, not API documentation with buttons. Recruiters should be able to enter as a
seeded user in one click and use a populated workspace.

## Goals
- One-click demo sessions for seeded users. Passwords never appear in the UI or bundle.
- Show the real capabilities: cookie auth, user search, DM/group creation, inbox,
  cursor-paginated history, SignalR delivery, presence, mark-as-read and leave.
- Keep a normal email/password login path for capability completeness.
- Look intentional: warm cream, charcoal, acid-lime live signal, coral accents;
  editorial split-pane chat, not a Discord clone.

## Non-goals
Public production signup, email confirmation, password reset, avatar uploads,
attachments, admin tooling, typing indicators, read receipts, moderation.

## Demo data
Use the existing six seeded users: Ahmed, Sara, Mohamed, Nour, Omar, Fatma.
Seed password stays server-side (`DataSeed:UsersPassword`).
Default demo user: Ahmed. Existing four conversations/messages populate the workspace.

## API changes
- `GET /api/v1/auth/me` → current user `{ id, email, displayName, profilePictureUrl }`.
- `GET /api/v1/auth/demo-users` → seeded demo users, demo mode only.
- `POST /api/v1/auth/demo-login` `{ userId }` → sign in a seeded user, demo mode only.
- `GET /api/v1/conversations/{conversationId}` → conversation metadata and participants,
  with online state and access check.
- Inbox: include conversations with no messages (left join), add `unreadCount`.
- `MessageNotification` must include message content.
- Demo endpoints gated by `Demo:Enabled`; normal login remains.
- No CORS dependency: SPA uses Vite/nginx same-origin proxy.

## Demo auth
The login page presents seeded profile cards (`Continue as Ahmed`, etc.) plus a quiet
"Use email and password" link. Clicking a card calls demo-login and lands in the inbox.
A note explains that realtime can be seen by opening a private window as Sara.

## UI
Routes:
- `/login` — demo picker + standard login.
- `/inbox` — conversation rail, unread, presence, last message.
- `/c/:id` — chat feed, older-page loading, composer, details drawer, leave.
- `/new` — user search, DM/group builder.
- `/me` — session sheet/logout.
- `/register` — secondary registration form (auto signs in; no email gate).

Design:
- Cream paper canvas, charcoal ink, lime live accents, coral send/unread.
- Large Bricolage/Fraunces-style display wordmark, Instrument Sans body, IBM Plex Mono
  metadata.
- Conversation rail and message feed separated by crisp rules; initials monograms.
- Mobile: rail becomes a drawer; chat is primary.
- Real states: connection/reconnect, loading, empty, error, pagination.

## Frontend stack
Vite + React + TypeScript, React Router, TanStack Query, `@microsoft/signalr`.
Cookie credentials on all REST calls. Vite proxy `/api` and `/hubs` to the API.

## Acceptance
- A fresh visitor can choose Ahmed and reach a populated inbox without credentials.
- Ahmed can open a conversation, send a message and see the optimistic/history state.
- A private-window Sara session receives the message over SignalR.
- Search, new DM/group, mark-read, details, leave and logout work.
- `npm run build` passes; API build/tests pass.
