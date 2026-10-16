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
- **Microsoft.OpenApi pinned 2.7.5** (transitive pin; fixes CVE-2026-49451).
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
- [ ] **T8 — Chat + Notifications.** Group/direct messaging + notifications (SignalR).
- [ ] **T9 — Google sign-in.** Flutter sends a Google ID token; backend validates it
  (Google token validation) and issues our JWT (link/create `ApplicationUser`).
- [ ] **T10 — Role-based access.** Supervisor vs Pilgrim authorization policies and
  screen gating.
- [ ] **T11 — Tests.** xUnit. Start with Domain unit tests (aggregates/state
  machines), then integration tests (Testcontainers) for key endpoints. Wire
  `dotnet test` into CI.

---

## 10. DevOps & Deployment

- **Docker**: multi-stage, non-root (`USER $APP_UID`), listens on **8080**, honors
  Render's dynamic `PORT`.
- **CI** `.github/workflows/ci.yml`: on push/PR to `main` — restore,
  vulnerable-package audit (fails on any known vuln), build Release. (Add
  `dotnet test` once T11 lands.)
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
