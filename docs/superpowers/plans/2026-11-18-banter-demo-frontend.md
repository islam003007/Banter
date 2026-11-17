# Banter Demo Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Harden the Banter API for a demo SPA and build a polished React chat client.

**Architecture:** Minimal API endpoints remain thin; demo auth is a backend-gated allowlist. The SPA is a Vite/React client using cookie auth, TanStack Query and SignalR through a same-origin proxy.

**Tech Stack:** ASP.NET Core 10, MediatR, PostgreSQL, Redis, SignalR; React 19, TypeScript, Vite, TanStack Query, @microsoft/signalr.

**Spec:** `docs/superpowers/specs/2026-11-18-banter-demo-frontend-design.md`

## Global Constraints
- No password in frontend code or UI.
- Demo login only for seeded users; gate with `Demo:Enabled`.
- Do not change existing `/api/v1` endpoint shapes except additive fields.
- Same-origin proxy; do not add a dependency on permissive CORS.
- No new auth scheme; use existing Identity cookies.

## Review Focus
- Demo login cannot impersonate non-seeded users.
- A newly created conversation with no messages is visible in inbox.
- Unread count is correct after mark-as-read and a new message.
- SignalR notification contains enough content to render immediately.
- Pagination cursor remains stable when new messages arrive.

---

### Task 1: Demo auth and current-user endpoints
**Files:** `Banter.API/Endpoints/Auth/Me.cs`, `DemoUsers.cs`, `DemoLogin.cs`, `Banter.API/DepedencyInjection.cs`, `Banter.API/appsettings*.json`
**Interfaces:** Produces `GET /auth/me`, `GET /auth/demo-users`, `POST /auth/demo-login` `{ userId }`; `Demo:Enabled` boolean.
- [ ] Add `Demo:Enabled=false` default, true in Development; demo user allowlist from seeded emails.
- [ ] Implement `/auth/me` from `UserContext`/Identity.
- [ ] Implement demo-users and demo-login using `SignInManager.SignInAsync`; refuse unknown IDs and when disabled.
- [ ] Build API and add endpoint tests where feasible.
- [ ] Commit.

### Task 2: Conversation detail and inbox contract
**Files:** `Banter.API/Endpoints/Conversations/GetById.cs`, `Banter.Application/Features/Conversations/GetConversationQuery.cs`, `GetInboxQuery.cs`
**Interfaces:** Produces `GET /conversations/{id}` with title/isGroup/participants; adds `unreadCount` to inbox; nullable last message.
- [ ] Add participants/online/access query.
- [ ] Change inbox join to include empty conversations; add unread count from `LastSeenMessageId`.
- [ ] Build/tests.
- [ ] Commit.

### Task 3: Realtime payload
**Files:** `Banter.Application/Abstractions/Realtime/MessageNotification.cs`, `SendMessageCommand.cs`
**Interfaces:** Adds `Content` to notification.
- [ ] Add content; build/test; commit.

### Task 4: SPA scaffold and auth
**Files:** `Banter.Web/` (Vite React TS), `vite.config.ts`, `src/lib/api.ts`, `src/auth/*`, `src/pages/Login.tsx`, `Register.tsx`, design tokens.
**Interfaces:** Consumes demo/auth endpoints; produces authenticated app shell.
- [ ] Scaffold Vite app, install deps, configure proxy `/api` and `/hubs`.
- [ ] Build one-click demo picker, standard login/register, `/auth/me` session handling.
- [ ] Style the cream/charcoal/lime/coral system.
- [ ] Build.
- [ ] Commit.

### Task 5: Inbox, new chat, chat, details
**Files:** `src/pages/Inbox.tsx`, `Chat.tsx`, `NewConversation.tsx`, `src/components/*`, `src/lib/signalr.ts`, query keys.
**Interfaces:** Consumes REST + SignalR; produces full demo flow.
- [ ] Conversation rail with unread/presence/empty.
- [ ] Chat history, older pages, send, mark read, optimistic cache update.
- [ ] SignalR connect/reconnect/heartbeat; private-window note.
- [ ] Search/new DM/group; details/leave; responsive.
- [ ] Build and browser-check.
- [ ] Commit.

### Task 6: Verification and handoff
- [ ] `dotnet build Banter.slnx`, unit tests.
- [ ] `npm run build` in `Banter.Web`, TypeScript clean.
- [ ] Smoke with mock/available API; document run commands and demo users.
- [ ] Commit final.
