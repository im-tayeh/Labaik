# Labaik — Backend Engineering Guide (CLAUDE.md)

> Single source of truth for how the Labaik backend is built and how to work in it.
> Claude Code MUST follow every rule here. Read it fully before writing or changing code.
> This file is also the backlog: implement unchecked tasks top-down, mostly autonomously
> (see "Autonomous Working Protocol").

---

## 1. Project Overview

**Labaik** is a digital companion app for Umrah pilgrims. It guides the pilgrim
step by step: from trip preparation, through performing the rituals (Manasik),
to returning safely.

- This repository is the **backend only** (ASP.NET Core Web API).
- The frontend is a separate **Flutter** app (a teammate) consuming this API over
  HTTPS. **Maps and geolocation rendering are the Flutter side's job**, not the backend's.
- UI/UX is in Figma; screens drive the API contract.
- Repo: `github.com/im-tayeh/Labaik`, default branch `main`.
- Integration is **contract-first**: the OpenAPI document generated from this code
  is the contract Flutter codes against. Keep endpoints, field names, and status
  codes stable and meaningful.

---

## 2. Architecture

Clean Architecture, **4 layers**, strict inward dependency direction:

```
Api  ->  Infrastructure  ->  Application  ->  Domain
(every arrow points inward; inner layers never reference outer ones)
```

- **Domain** (`src/Labaik.Domain`): entities, aggregates, value objects, domain
  events, business rules. Depends on **nothing** external — not even MediatR.
- **Application** (`src/Labaik.Application`): use cases via CQRS. Depends only on
  Domain. Defines interfaces (`IAppDbContext`, `IEmailSender`, `ICurrentUser`,
  `IJwtTokenGenerator`, `IRefreshTokenService`, `IJoinCodeGenerator`,
  `IUserDirectory`, ...) implemented by Infrastructure. References EF Core only for
  `DbSet` in `IAppDbContext`.
- **Infrastructure** (`src/Labaik.Infrastructure`): EF Core + PostgreSQL, ASP.NET
  Identity, email, JWT, interceptors, seeders.
- **Api** (`src/Labaik.Api`): Minimal API endpoints, DI wiring, middleware,
  current-user resolution, JWT setup, OpenAPI.

No `Contracts` or `Client` project — backend only.

---

## 3. Patterns & Conventions

- **CQRS with MediatR**: each use case is a Command or Query + Handler in a
  vertical-slice folder.
- **Result pattern, not exceptions, for business outcomes.** Domain and Application
  return `Result` / `Result<T>` with typed `Error`
  (`Validation/NotFound/Conflict/Unauthorized/Forbidden/TooManyRequests/Failure`).
  Exceptions only for truly unexpected failures.
- **Rich DDD**: private constructors + static `Create(...)` factories returning
  `Result`; encapsulated collections (private `List<T>`, public `IReadOnlyList<T>`);
  invariants inside the entity; state machines with explicit guarded transitions.
- **Domain events**: our own `IDomainEvent` (Domain free of MediatR). Raised inside
  aggregates via `AggregateRoot.Raise(...)`. Dispatched after `SaveChanges` by
  `DomainEventDispatchInterceptor`, which wraps each event in
  `DomainEventNotification<T>` and publishes it through MediatR. Handlers implement
  `INotificationHandler<DomainEventNotification<TEvent>>`.
- **Validation**: `ValidationBehavior` runs FluentValidation before the handler.
  Input-shape failures -> `ValidationException` -> `ValidationExceptionHandler` ->
  **HTTP 422** with an `errors` dictionary.
- **Error handling**: `GlobalExceptionHandler` + `ValidationExceptionHandler` emit
  **RFC 9457 ProblemDetails** with a `traceId` correlating responses to Serilog
  logs. Business `Error`s map to HTTP via `error.ToProblem()` in endpoints.
- **Ownership authorization**: any handler on user-owned data loads the entity and
  verifies `entity.UserId == ICurrentUser.Id`; otherwise returns `NotFound`
  (never leak existence).

---

## 4. Tech Stack & Versions

- **.NET 10**, **C# 14**. `LangVersion 14`, nullable + implicit usings enabled.
- **EF Core 10** + **PostgreSQL** (Npgsql).
- **ASP.NET Core Identity** + **JWT Bearer** + rotated refresh tokens.
- **MediatR 14.2** (community license is fine here), **FluentValidation 12.1.1**.
- **Serilog**; **Scalar** OpenAPI UI (Code-First generation).
- **Asp.Versioning** — URL versioning: `/api/v{version:apiVersion}/...`.
- **Microsoft.OpenApi pinned 2.12.0** (transitive pin; ≥ 2.7.5 fixes CVE-2026-49451;
  2.12.0 is the minimum required by Microsoft.AspNetCore.OpenApi 10.0.12).
- **OpenAPI per version**: `AddApiVersioning().AddApiExplorer().AddOpenApi()`
  (Asp.Versioning.OpenApi) + `MapOpenApi().WithDocumentPerVersion()`; do not call
  `builder.Services.AddOpenApi()` directly (analyzer AV0029/AV0030). Document:
  `/openapi/v1.json`.
- **Package management**: Central Package Management. Versions in
  `Directory.Packages.props`; shared build settings in `Directory.Build.props`;
  `CentralPackageTransitivePinningEnabled` on. `.csproj` files carry **no** `Version`
  attributes and no duplicated TFM/nullable `PropertyGroup`.

---

## 5. Coding Standards (STRICT)

1. **All code is English only** — identifiers, comments, logs, summaries, exception
   text. **No Arabic anywhere in code.**
2. **Arabic user-facing content is DATA, not code.** It lives in JSON files (e.g.
   `src/Labaik.Infrastructure/Persistence/Seed/*.json`) loaded by a seeder, and is
   reviewed/approved separately (religious content needs approval by a trusted
   authority before production). Never hardcode Arabic strings in C#.
3. **Commands/Queries and any request-body type must be `public`** (Minimal API
   binding requires it).
4. **Vertical slices**: `Features/<Area>/Commands/<Name>/` and
   `Features/<Area>/Queries/<Name>/` — each with Command/Query + Validator + Handler
   (+ DTOs/Mappers per area).
5. **Thin handlers, rich domain.** Business rules live in the aggregate.
6. **Return `Result`**, not thrown exceptions, for expected outcomes.
7. **DTOs for all responses** — never return domain entities.
8. **One aggregate per transaction**; prefer a single `SaveChanges` per command.
9. Endpoints: `MapGroup`, carry an `ApiVersionSet`, `.RequireAuthorization()` where
   protected, translate failures with `result.Error!.ToProblem()`.

---

## 6. Authentication Design (built)

- `AddIdentityCore` (not `AddIdentity`; API is JWT, no cookies). `AddDataProtection()`
  required for token providers. User key is **`Guid`**. `ApplicationUser :
  IdentityUser<Guid>` adds `FullName`.
- Short-lived **JWT access tokens** + **rotated refresh tokens** (single-use, stored
  SHA-256 hashed, revoked on use).
- **Email verification** via **6-digit OTP**, stored hashed, with expiry, attempt
  limit, and resend cooldown.
- **Password reset** via emailed code.
- Security: `forgot-password` always 200 (no enumeration); `login` returns **403
  `email_not_confirmed`** when unverified; re-registering an unconfirmed email
  resends a fresh code (not 409).
- `ICurrentUser` reads `sub` from the JWT via `IHttpContextAccessor`.

---

## 7. Status

### Built, in repo, tested, deployed
- Walking skeleton: 4-layer solution, Serilog, ProblemDetails, OpenAPI/Scalar,
  Docker, GitHub Actions CI, Render deployment, auto-migrate on startup.
- CQRS foundation: MediatR, Result/Error, FluentValidation, ValidationBehavior.
- Persistence: EF Core 10 + PostgreSQL, migrations, domain-event dispatch interceptor.
- **Authentication slice (complete)**: register, verify-email (OTP), login,
  refresh-token, resend-code, forgot/reset-password, `GET /me`.
- **Journey aggregate**: single-step onboarding `POST /journeys` (pilgrimage type,
  first-time flag, travel party, arrival plan, stays) + `GET /journeys/me`. `City`
  is a free-text string (frontend supplies the list).
- **Group aggregate**: create (leader), preview by join code, join by code. Unique
  join code; no double-join.
- **Email via Brevo HTTP API** (T1): provider-switchable `Logging`/`Smtp`/`Brevo`.
- **Ritual Guide** (T2): `RitualStep` seeded from `umrah-steps.json` + `RitualProgress`
  per user; list/progress/complete/reset endpoints. Content is placeholder, pending
  religious approval.

---

## 8. Autonomous Working Protocol (IMPORTANT)

Work the Backlog top-down without waiting for per-task orders:

1. Pick the **topmost unchecked** Backlog item.
2. Implement it as a **full vertical slice**, copying the patterns of the
   **Journey / Group** slices exactly (Command/Query + Validator + Handler + DTO +
   endpoint + EF config + DI).
3. If the EF model changed, add a migration:
   `dotnet ef migrations add <Name> --project src/Labaik.Infrastructure --startup-project src/Labaik.Api --output-dir Persistence/Migrations`.
4. Ensure `dotnet build` is clean (no warnings) and the vulnerable-package audit
   passes.
5. Tick the item's checkbox in this file and add a one-line note of what changed.
6. Commit with a conventional message (`feat(area): ...`, `fix(area): ...`) and
   continue to the next item.
7. **Only stop to ask** when a genuine product decision is ambiguous (not for
   mechanical choices). Prefer the pilgrim-facing path when unsure.
8. Never introduce Arabic into code; put Arabic content in JSON seed files.
9. Keep the OpenAPI contract stable; if a change is breaking, note it clearly for
   the Flutter side in the commit body.

---

## 9. Backlog (ordered; pilgrim-first)

- [x] **T1 — Email via Brevo HTTP API.** Add `BrevoEmailSender : IEmailSender`
  (typed `HttpClient`, `POST https://api.brevo.com/v3/smtp/email`, `api-key` header,
  15s timeout, throws on non-success). Extend `EmailSettings` with `ApiKey`.
  Switch DI on `Email:Provider` = `Logging`/`Smtp`/`Brevo`. Config:
  `Email:Provider`, `SenderName`, `SenderEmail`; secret `Email:ApiKey`.
  Acceptance: with `Provider=Brevo` and a verified Brevo sender, register sends a
  real OTP email. On Render set `Email__*` env vars; remove any SMTP/personal email.
  - _Done:_ `BrevoEmailSender` via typed `HttpClient` (15s, `api-key` header); DI switch Logging/Smtp/Brevo; appsettings default `Logging`, personal email/SMTP removed; pinned patched `System.Security.Cryptography.Pkcs`/`Xml`._
- [x] **T2 — Ritual Guide.** `RitualStep` aggregate (PilgrimageType, Order, Phase,
  Kind, Title, Body, Reference, IsPublished; unique index on (PilgrimageType,Order))
  + `RitualProgress` aggregate (UserId, PilgrimageType, CurrentOrder, owned
  `CompletedRitualStep` collection; unique index on (UserId,PilgrimageType)).
  Endpoints under `/api/v1/rituals` (auth): `GET /steps?pilgrimageType=`,
  `GET /progress?pilgrimageType=` (auto-starts), `POST /progress/complete`,
  `POST /progress/reset`. Seed published Umrah steps from
  `Persistence/Seed/umrah-steps.json` (Arabic content; csproj `CopyToOutputDirectory`).
  Call the seeder in `Program.cs` after `MigrateAsync`. Acceptance: steps list
  returns the seeded Umrah steps; completing a step advances `CurrentOrder`;
  finishing all sets `isCompleted`.
  - _Done:_ `Rituals` domain + 4 endpoints (`pilgrimageType` defaults to Umrah); steps complete strictly in order (409 otherwise); idempotent `RitualStepSeeder` upserts by (type, order) and unpublishes removed steps; migration `AddRitualGuide`; 10 placeholder Umrah steps (`reviewStatus: PendingReligiousApproval`)._
- [x] **T3 — Home screen aggregation.** `GET /api/v1/home` returning the pilgrim's
  journey status, next ritual step, and group summary in one call.
  - _Done:_ read-only `GetHomeQuery` -> `HomeDto { journey?, ritual?, group? }` (null sections when not applicable); ritual type follows the journey (default Umrah), `isStarted=false` shows the first step without creating progress; group = most recently joined, with `myRole`._
- [x] **T4 — Trip details view/edit.** `GET /journeys/{id}` (owner only) + edit
  endpoints matching the Figma "تفاصيل الرحلة / تعديل تفاصيل الرحلة" screens.
  - _Done:_ `GET /journeys/{id}` + `PUT /journeys/{id}` (full replace of isFirstTime, travelParty, arrival, stays; `pilgrimageType` not editable); editable while Draft or Ready (409 after); non-owner -> 404; stays ordered by arrival; `JourneyStays` key `ValueGeneratedNever`._
- [x] **T5 — Ihram preparation content.** Educational content module (content as
  JSON seed, same pattern as Ritual Guide).
  - _Done:_ generic `GuideArticle` aggregate keyed by (`GuideCategory`, Order); `GET /guides?category=` (default IhramPreparation) + `GET /guides/{id}`; idempotent `GuideArticleSeeder` from `ihram-preparation.json` (8 placeholder articles, pending religious approval); migration `AddGuideArticles`. New content screens = new category + JSON file._
- [x] **T6 — Announcements.** Group leader broadcasts to members; pilgrim lists/reads.
  `Announcement` under the Group aggregate or its own aggregate referencing GroupId.
  - _Done:_ own `Announcement` aggregate (GroupId) created only via `Group.PostAnnouncement` (leader-only, 403 otherwise); owned `AnnouncementRead` receipts (author auto-read); `POST/GET /groups/{groupId}/announcements`, `POST /announcements/{id}/read` (idempotent, 204); non-members -> 404; `home.group.unreadAnnouncements`; `AnnouncementPosted` event ready for T8 notifications; migration `AddAnnouncements`._
- [x] **T7 — Family circle / live location.** Store+relay member lat/lng for
  "أين أفراد مجموعتي"; realtime via SignalR. Backend stores/relays only; map is Flutter.
  - _Done:_ `LocationShare` aggregate (one per user, last position only, opt-in, stop = erase, stale after 15 min); `GET /location/me`, `PUT /location/sharing`, `POST /location`, `GET /groups/{groupId}/locations` (members only); SignalR hub `/hubs/location` (JWT via `?access_token=`; client `UpdateLocation`, server `LocationUpdated`/`StoppedSharing`), pushed via domain events + `ILocationBroadcaster` so REST and hub updates both broadcast; migration `AddLocationSharing`. Hub subscribes groups at connect time (reconnect after joining a group)._
- [x] **T8 — Chat + Notifications.** Group/direct messaging + notifications (SignalR).
  - _Done (scope agreed: group chat only, in-app notifications only; direct messages and FCM push deferred):_ `ChatMessage` aggregate via `Group.PostMessage` (members only); `POST/GET /groups/{groupId}/messages` (cursor `before`, `take` ≤ 100); `Notification` aggregate stores type + ids + preview (no sentences; Flutter localizes), created from `AnnouncementPosted` (members) and `MemberJoinedGroup` (leader); `GET /notifications`, `GET /notifications/unread-count`, `POST /notifications/{id}/read`, `POST /notifications/read-all`; `home.unreadNotifications`; hub `/hubs/events` (`MessageReceived` to group, `NotificationReceived` to user via `sub` user-id provider); shared `GroupSubscribedHub` base; migration `AddChatAndNotifications`._
- [x] **T9 — Google sign-in.** Flutter sends a Google ID token; backend validates it
  (Google token validation) and issues our JWT (link/create `ApplicationUser`).
  - _Done:_ `POST /auth/google { idToken }` -> same `AuthTokens` as login. `Google.Apis.Auth` validates signature/expiry/audience against `Authentication:Google:ClientIds` (env `Authentication__Google__ClientIds__0..n`; empty -> 400 `Auth.GoogleNotConfigured`). Identity external logins (`AspNetUserLogins`): linked login -> sign in; else link by email only if Google `email_verified` (unconfirmed existing account: password removed before confirming, anti-squatting); else create password-less confirmed user. **Pending:** real Client IDs from Google Cloud._
- [x] **T10 — Role-based access.** Supervisor vs Pilgrim authorization policies and
  screen gating.
  - _Done:_ roles `Admin`/`Supervisor` (pilgrim = no role), seeded by `RoleSeeder`; `Admin:Emails` (env `Admin__Emails__0..n`) bootstraps Admin on any token issue; policies `Admin`, `Supervisor` (Supervisor or Admin); `POST /groups` now Supervisor-only (**breaking**: pilgrims get 403); `GET /admin/users?email=`, `PUT|DELETE /admin/users/{id}/supervisor` (applies from next token/refresh); roles exposed in login `user.roles` and `/auth/me`. Group leadership stays per-group (announcements remain leader-only)._
- [x] **T11 — Tests.** xUnit. Start with Domain unit tests (aggregates/state
  machines), then integration tests (Testcontainers) for key endpoints. Wire
  `dotnet test` into CI.
  - _Done:_ `tests/Labaik.Domain.Tests` (36 tests: Journey, Group, RitualProgress, LocationShare, content, announcements, notifications) and `tests/Labaik.Api.IntegrationTests` (21 tests over HTTP: auth, journeys, rituals, guides, home, groups/roles, announcements, notifications, chat, locations) on one shared `WebApplicationFactory` + `postgres:17` Testcontainer (`ICollectionFixture`; Serilog's bootstrap logger allows one host per process) with a capturing `IEmailSender` for OTPs; `dotnet test` step in CI; `EntityFrameworkCore.Relational` pinned to 10.0.12. New slices must add tests here._

### Backlog v2 — Remaining scope (post Flutter sync, 17 Oct 2026)

#### Surfaces (4 clients)

1. **Pilgrim app** (mobile) — the معتمر experience.
2. **Supervisor view** — same mobile app, role-gated by `Supervisor` in `user.roles`.
3. **Company view** — the شركة experience (manages its trips/groups/members).
4. **Admin Dashboard** — a **separate web client** for platform operators/developers. This is where **company accounts are created** and the platform is managed (backend = the `/admin/*` area).

The mobile app **does keep an account-type selection screen** (`PUT /auth/account-type` stays). What it does NOT do is self-create company accounts — those come only from the admin dashboard.

#### Architectural shift (read first)

The app moved from a **pilgrim-self-service** model to an **admin → company → group** managed model:

```
Admin (dashboard) --creates--> Company --creates--> Trip --has--> Group(s) --assigns--> Supervisor
                                                                    \--adds--> Members (Pilgrims)
```

Consequences for existing code:
- `Group` becomes owned by `Company` (gains `CompanyId`, and a `TripId`).
- `POST /groups` and `POST /groups/{code}/join` are no longer called by the app.
  **Decision: keep them Admin-only** (do not delete yet).
- A third role `Company` is added to `user.roles`.
- `Trip` (company product) is distinct from `Journey` (pilgrim's personal onboarding). Do not merge.

#### Build order

| Phase | Why now | Tasks |
| --- | --- | --- |
| A — Company foundation | Fills the data every screen reads from | T13–T17 |
| B — Supervisor operations | Closes the supervisor↔pilgrim loop | T18–T23 |
| C — Pilgrim & shared | Completes pilgrim experience | T24–T25 |
| D — Feature-complete | Sections with no endpoint at all | T26–T28 |
| E — Journey reconciliation | Small shape mismatches | T29 |
| F — Admin dashboard | Root of the chain; small (mostly admin endpoints) | T30–T31 |
| Deferred | By project-owner decision | 1:1 chat, FCM push, religious-content review |

Note on order: Phase F is the logical top of the chain (admin creates companies),
but the backend work is small and can be built alongside Phase A — `POST /admin/companies`
in T13 is the core of it. Build the rest of F after A if time is tight.

---

#### Phase A — Company foundation

**T13 — `Company` role + aggregate**
- Domain: `Company` aggregate (`Id`, `Name`, `CreatedAtUtc`); link user → company.
- Identity: add `Company` role.
- Auth: include `Company` in `user.roles` across login / verify-email / google / refresh-token and `GET /auth/me`.
- `POST /admin/companies` (name + owner email) — Admin only.
- `GET /company/me` -> `{ id, name }` (welcome header).
- _Done:_ `Company` aggregate (`Name`, `OwnerUserId` unique, `CreatedAtUtc`, `CompanyCreated` event); role `Company` + policy `Company`; `POST /admin/companies` links an **existing** account by email (404 if not registered, 409 if it already owns one) and grants the role (effective after refresh); `GET /company/me`; `CompanyDataAccess.GetMyCompanyIdAsync` resolves "my company" for all company endpoints; migration `AddCompanies`.

**T14 — Company trips**
- Domain: `Trip` aggregate owned by `Company`: `Name`, `PilgrimageType`, `Destinations`, `DepartureDate`, `ReturnDate?`, `DeparturePlace`, `ExpectedPilgrims`, `Notes?`; computed `TripStatus` (1 Upcoming / 2 Departed).
- `POST /company/trips`, `GET /company/trips` (list), `GET /company/trips/{id}` (details), `PUT /company/trips/{id}`.
- _Done:_ `Trip` aggregate (separate from `Journey`; `CompanyId` FK, `Destinations` as `text[]`, `DepartureDate`/`ReturnDate` as `DateOnly` "yyyy-MM-dd", atomic `Update`, `TripCreated` event); status computed via `StatusOn(todayUtc)` (Departed once today ≥ departure); shared `ITripFields` + `TripFieldsValidator<T>` for create/update; other companies' trips -> 404; migration `AddTrips`.

**T15 — Company-managed groups**
- Domain: `Group` gains `CompanyId` + `TripId`.
- `POST /company/trips/{tripId}/groups`
- `POST /company/groups/{groupId}/members`, `DELETE /company/groups/{groupId}/members/{userId}`
- `PUT /company/groups/{groupId}/supervisor` (grants `Supervisor` role).
- _Done:_ `Group` gains nullable `CompanyId`/`TripId` (FKs) and `LeaderId` is now **nullable** (company groups start with no supervisor); `Group.CreateForTrip`, `AssignSupervisor` (replaces + removes the previous supervisor, promotes an existing member, idempotent, `SupervisorAssigned` event), `AddMember`, `RemoveMember` (supervisor can't be removed -> 409); members/supervisor are added **by email of an existing account**; extra `GET /company/trips/{tripId}/groups` (company needs to list group ids); `POST /groups` and `POST /groups/{code}/join` are now **Admin-only** (locked decision 2); `GenerateUniqueJoinCodeAsync` shared; migration `AddCompanyManagedGroups`.

**T16 — Company dashboard numbers**
- `GET /company/home` -> `{ activeTrips, pilgrimsCount, supervisorsCount, groupsCount }`.
- _Done:_ read-only `GetCompanyHomeQuery` (no migration). Definitions: `activeTrips` = trips with no return date or returning today or later (UTC); `pilgrimsCount`/`supervisorsCount` = **distinct** people across all the company's groups; `groupsCount` = all its groups.

**T17 — Company notifications (new enum values)**
- Add to `NotificationType` (next numbers, do not renumber): `3 GroupCreated`, `4 SupervisorActivated`, `5 GroupWithoutSupervisor`. Recipient: company account; broadcast via `/hubs/events` `NotificationReceived`.

---

#### Phase B — Supervisor operations

**T18 — Gathering point**
- Domain: `GatheringPoint` VO on `Group` (`Address`, `Latitude`, `Longitude`, `GatheringTimeUtc?`).
- `PUT /groups/{groupId}/gathering-point` (group leader only), `GET /groups/{groupId}/gathering-point` (members).
- Fires new `NotificationType 6 GatheringPointChanged` to members.

**T19 — Attendance / check-in**
- Domain: `Attendance` per member (`Status` 1 Present / 2 Absent, `ConfirmedAtUtc?`).
- `POST /groups/{groupId}/attendance/confirm` (pilgrim "I arrived"), `GET /groups/{groupId}/attendance` (supervisor list + counts), optional `PUT .../attendance/{userId}` (manual). Fires notification to supervisor on confirm.

**T20 — Member profile fields**
- Add: `MemberNumberInGroup`, `Gender`, `Nationality`, `HealthNotes`, `Notes`, `PhotoUrl`, `Email`.
- `HealthNotes` is sensitive PII — restrict to supervisor/company, never in public DTOs.
- `GET /groups/{groupId}/members/{userId}` (supervisor).

**T21 — Announcement enrichment**
- Add fields (never rename): `Importance` (1 Normal / 2 Important), `GatheringTimeUtc?`, `Location?`, `ImageUrl?`, `Audience` (1 Group / 2 AllCompanyPilgrims).
- Then Flutter re-enables `unreadAnnouncements` and `POST /announcements/{id}/read`.

**T22 — Multiple groups per supervisor**
- `GET /supervisor/groups` -> list (name + counts); home supports selection.

**T23 — Trip itinerary**
- Domain: `ItineraryDay` + `ItineraryActivity` (`Title`, `TimeUtc?`, `Place?`) on `Trip`/`Group`.
- `GET /trips/{id}/itinerary`, `PUT` (company/supervisor). Fills "trip program" + "upcoming trip" card.

---

#### Phase C — Pilgrim & shared

**T24 — Chat enhancements**
- `unreadCount` + read state for group chat; location messages (`Latitude`/`Longitude` + message type) in `ChatMessageDto`; `SenderPhotoUrl`.

**T25 — Missing pilgrim notification types**
- Optional `NotificationType 7 GroupMessagePosted`. Cover the 4 types in the pilgrim notifications screen (announcement / supervisor message [deferred, needs 1:1] / gathering-point change [T18] / group message).

---

#### Phase D — No-endpoint sections

**T26 — Field mode (map)**
- `GET /map/destinations?type=` (Haram gates + images, seed data), `GET /map/route`, `POST /map/emergency`, unify `POST /map/location` with `/location`.

**T27 — Help center**
- `GET /pilgrim/supervisor` (supervisor contact for pilgrim), `POST /lost-child-reports`.

**T28 — Family circle**
- Full section (invite/accept + location sharing within the circle), independent of the company group.

---

#### Phase E — Journey reconciliation

**T29**
- Departure time: app sends one date; server wants `arrivalAtUtc` per stay. Current owner decision: same date for all stays. (Alt: make `arrivalAtUtc` optional, or add trip-level `departureAtUtc`.)
- `pilgrimageType` in `PUT`: **Decision: allow editing it.**
- Frontend-only fields with no server home: route city order, traveler name, "not decided yet" — keep in Flutter.

---

#### Admin dashboard endpoints (backend for the dev/ops web panel)

**T30 — Admin area (`/admin/*`, Admin role only)**
- `POST /admin/companies` (name + owner email) — create a company account (already in T13).
- `GET /admin/companies` (list), `GET /admin/companies/{id}` (details + its trips/groups/supervisors counts).
- `PUT /admin/companies/{id}` (edit), optional `POST /admin/companies/{id}/deactivate`.
- `GET /admin/overview` — platform-wide numbers (companies, trips, pilgrims, supervisors) for the dashboard home.
- Reuse the existing `GET/PUT/DELETE /admin/users/{id}/supervisor` endpoints.
- The dashboard itself is a separate web frontend; the backend only exposes this area.

---

#### Decisions locked in (flip if you disagree)

1. "Account type" screen + `PUT /auth/account-type`: **kept**. Company accounts are created only from the admin dashboard, never self-registered.
2. `POST /groups` + join: **kept Admin-only**, not deleted.
3. Multiple groups per supervisor: **yes** (T22).
4. `pilgrimageType` editable in `PUT`: **yes**.
5. 1:1 chat + FCM push: **deferred**.

---

## 10. DevOps & Deployment

- **Docker**: multi-stage, non-root (`USER $APP_UID`), listens on **8080**, honors
  Render's dynamic `PORT`.
- **CI** `.github/workflows/ci.yml`: on push/PR to `main` — restore,
  vulnerable-package audit (fails on any known vuln), build Release, `dotnet test`
  (integration tests need Docker; run `docker compose up -d` is NOT required —
  Testcontainers starts its own PostgreSQL).
- **Hosting**: Render free tier; auto-deploy on push to `main`; instances sleep
  after ~15 min idle (first request ~50s cold start — normal).
- **DB on Render**: managed PostgreSQL. **Migrations auto-apply on startup**
  (`MigrateAsync` in `Program.cs`), then the content seeder runs.
- **Secrets**: `dotnet user-secrets` locally; environment variables on Render
  (`ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`, `Email__*`). Never
  commit secrets. `__` denotes nesting in env var names.

### Local development
- PostgreSQL in Docker via `docker-compose.yml`, host port **5433** (`"5433:5432"`).
- Connection string (user-secret):
  `Host=localhost;Port=5433;Database=labaik;Username=postgres;Password=postgres`.
- Commands:
  - `docker compose up -d`
  - `dotnet ef migrations add <Name> --project src/Labaik.Infrastructure --startup-project src/Labaik.Api --output-dir Persistence/Migrations`
  - `dotnet ef database update --project src/Labaik.Infrastructure --startup-project src/Labaik.Api`
  - `dotnet run --project src/Labaik.Api` (Scalar: `https://localhost:7000/scalar/v1`)
  - Inspect DB: `docker exec -it labaik-postgres psql -U postgres -d labaik -c '\dt'`
    (PowerShell: single-quote the whole `-c` SQL; quote identifiers with plain `"..."`).

---

## 11. Hard-Won Gotchas (do not repeat)

- **`OwnsMany` + clear-and-re-add across requests -> `DbUpdateConcurrencyException`.**
  Root cause: see the `ValueGeneratedNever()` gotcha below (now applied to
  `JourneyStays`, `GroupMembers`, `CompletedRitualSteps`). Still prefer building the
  aggregate and saving once per command.
- **`PendingModelChangesWarning` on startup** = model changed without a matching
  migration. Add one. In early dev, resetting migrations (drop DB + delete
  `Persistence/Migrations/*` + one fresh `InitialCreate`) is acceptable; never in prod.
- **Entity base key is `Guid`.** Don't configure a shadow `int` key on owned types.
- **Minimal API bodies must be `public`** (else `CS0122 inaccessible`).
- **Every handler must exist** — a missing `...Handler` is a runtime "no handler
  registered", not a compile error.
- **Package names**: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and
  `Microsoft.AspNetCore.Identity` (both with `Core`). Avoid legacy
  `Microsoft.AspNet.Identity.*`.
- **Options binding**: `AddOptions<T>().Bind(section)` (needs
  `using Microsoft.Extensions.Options;`).
- **Render blocks outbound SMTP** — use the Brevo HTTP API for production email.
- Value objects -> `OwnsOne`; child collections -> `OwnsMany` with
  `UsePropertyAccessMode(PropertyAccessMode.Field)`.
- **Appending to an `OwnsMany` of an already-saved aggregate**: the client-generated
  Guid key makes EF treat the new child as existing (UPDATE of 0 rows ->
  `DbUpdateConcurrencyException`). Configure the owned key `ValueGeneratedNever()`
  (see `RitualProgressConfiguration`).

---

## 12. Security Note

A Brevo API key was shared earlier in a chat (now exposed). Before relying on it in
production, **regenerate it in Brevo**, store it only in user-secrets / Render env
vars, and never commit it.

---

## 13. Figma Designs → API (Figma MCP rules)

This repo has **no UI layer**: no design tokens, components, styles, icons, images or
front-end build. Those live in the Flutter repo. Never add UI code or design assets
here. In this repo a Figma screen is an **input to the API contract**, not something to
implement visually.

### What maps where

| Figma artifact | Backend counterpart | Location |
| --- | --- | --- |
| A screen / flow | One or more endpoints in a vertical slice | `src/Labaik.Api/Endpoints/<Area>Endpoints.cs` + `src/Labaik.Application/Features/<Area>/` |
| Fields shown on a screen | Response DTO properties (camelCase in JSON) | `Features/<Area>/Dtos/*.cs` |
| A form | Command/Query + FluentValidation rules | `Features/<Area>/Commands/<Name>/` |
| A dropdown / segmented choice | C# enum, sent as an **integer** | `src/Labaik.Domain/<Area>/Enums/*.cs` |
| Static Arabic copy (guides, ritual steps) | JSON seed data, never C# | `src/Labaik.Infrastructure/Persistence/Seed/*.json` |
| Labels, buttons, error and notification wording | **Flutter** localization: the API sends codes/types, not sentences | n/a here |
| Images, icons, colors, spacing, fonts | **Flutter** assets/theme | n/a here |

### Workflow when given a Figma link

1. Read the frame with `get_metadata` / `get_screenshot` (use `get_design_context` only
   when the structure is needed; follow the `/figma-design-to-code` skill if it applies).
   Treat the design as a requirements source, not as code to generate.
2. List every data field, action and state the screen needs. Loading and offline
   states are Flutter's concern; empty **data** states are ours (e.g. `null` sections
   in `HomeDto`).
3. Check the existing contract first (`/openapi/v1.json` locally, the DTOs, §9 notes).
   Prefer extending an existing DTO with **additive** fields over a new endpoint.
4. Implement any gap as a normal vertical slice (§8), with tests (§9 T11).
5. Report field → DTO mapping and anything the design implies that the domain does not
   support yet (e.g. a field with no data source) as an open question, not a guess.

### Rules

- **No Arabic in C#** (§5). Arabic text seen in a design either becomes seed JSON
  (content) or stays in Flutter (UI copy).
- **Stable contract** (§1, §8.9): renaming or removing a field to match a design is a
  breaking change. Add the new field, keep the old one, and flag it for Flutter in the
  commit body.
- **Server owns rules, client owns presentation.** Visibility of a control (e.g.
  "create group") comes from data the API returns (`user.roles`,
  `home.group.myRole`). The API never returns layout, colors or display strings.
- **Enum options in a design must match the enum.** A new option = a new enum value
  appended with the next integer, never a renumbering.
- **Dates** are UTC ISO 8601 in the API. Formatting (Hijri/Gregorian, Arabic numerals)
  is Flutter's job.
