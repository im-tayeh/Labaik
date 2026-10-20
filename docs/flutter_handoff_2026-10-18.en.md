# Labaik API handoff for the Flutter team (18 October 2026)

This document describes the `main` branch as currently deployed on Render, after Batch 2. Every route, field and value is taken from the code and from the OpenAPI document generated from it (`/openapi/v1.json`), not from memory. If anything disagrees, the reference is `https://labaik-api.onrender.com/openapi/v1.json`.

This is the English version of `docs/flutter_handoff_2026-10-18.md` (Arabic); both have the same structure and content.

---

## 1. Breaking changes since the last handoff (16 October)

Review these first: they change behavior the app relies on, or may rely on.

| # | Change | What the app needs to do |
| --- | --- | --- |
| 1 | `POST /admin/companies` now requires `commercialRegistrationNumber` and `contactFullName` (in addition to `name` and `ownerEmail`), and returns the full `AdminCompanyDto` with 201. | Admin dashboard only. |
| 2 | An unregistered email in `POST /admin/companies` and in `POST /company/groups/{groupId}/members` no longer returns 404. The server now creates the account and emails a temporary password. | Do not rely on 404 to detect an unknown email. |
| 3 | `PUT /company/groups/{groupId}/supervisor` only accepts a supervisor on the company's roster. Anyone else returns 404. | Add the supervisor with `POST /company/supervisors` first, then assign. |
| 4 | `POST /groups` and `POST /groups/{code}/join` are now Admin-only; any other role gets 403. | The app does not call them (the company adds members). |
| 5 | `AnnouncementDto.groupId` is now nullable: company announcements to all pilgrims (`audience = 2`) have no group. | Treat `groupId` as optional. |
| 6 | Company groups start without a supervisor, so these fields can be `null`: `HomeGroupDto.leaderName`, `CompanyGroupDto.supervisorId` and `supervisorName`, and `GroupDto.leaderId`. | Show a "no supervisor yet" state. |
| 7 | Group chat messages now create notifications (types 8 and 9), and types 10, 11 and 12 were added. `GET /notifications/unread-count` will go up because of this. | Handle the new types, and ignore any unknown type instead of crashing. |
| 8 | `ChatMessageDto.body` can be an empty string `""` for location messages (`kind = 2`). | Show the location card instead of text. |
| 9 | `supervisorsCount` in `GET /company/home` is now the number of supervisors on the company roster (assigned or not), not only group supervisors. | Nothing; only the number changes. |
| 10 | `PUT /auth/account-type` does not exist on the server and will not be built. Public signup is for pilgrims only. | Remove the account-type screen and its call. |
| 11 | The `errors` keys of 422 responses are now camelCase JSON paths matching the request body: `fullName`, `arrival.entryPoint`, `stays[0].placeName` (were `FullName`). | Map field errors with camelCase names. |

Important non-breaking addition: every business error now carries a stable machine-readable `code` field (for example `Group.AlreadyMember`) alongside `status` and `title`, which are unchanged. Details in section 2.

Other non-breaking changes (new fields only): `HomeGroupDto.unreadMessages`; chat fields `kind`, `latitude`, `longitude`, `senderPhotoUrl` and `isRead`; optional `pilgrimageType` in `PUT /journeys/{id}`; optional `fullName` and `phoneNumber` when adding a member; extra fields in `GET /company/me`.

---

## 2. Contract basics

### Address and version

| Item | Value |
| --- | --- |
| Base URL | `https://labaik-api.onrender.com/api/v1` |
| Version | In the path: `/api/v{version}`; the only current version is `1`. |
| Format | JSON with camelCase field names. |
| OpenAPI | `https://labaik-api.onrender.com/openapi/v1.json` |
| Scalar | `https://labaik-api.onrender.com/scalar/v1` (enabled on the deployed instance) |
| Dates | `DateTimeOffset` as ISO 8601 in UTC, e.g. `2026-12-10T03:00:00+00:00`. Date-only fields (`DateOnly`) use `yyyy-MM-dd`, e.g. `departureDate` and `contractEndDate`. Hijri formatting and Arabic numerals are the app's job. |
| Enums | Always sent and received as integers, never as strings. Tables in section 4. |
| Ids | All `Guid`, as strings. |

### Generating a Dart client from OpenAPI

- The live document `https://labaik-api.onrender.com/openapi/v1.json` is **OpenAPI 3.0.4**, with an identical copy in the repository at `docs/openapi/v1.json`. A CI test fails if the copy differs from the code.
- Every operation has a stable `operationId` (e.g. `Login`, `GetHome`, `SendChatMessage`), its real response type and status codes, and its request body where there is one.
- Every enum is an integer with `x-enum-varnames` (the names) and a description such as `1 = Umrah, 2 = Hajj`, so the generator produces usable Dart enums.
- Fields that accept `null` are marked `nullable: true`.
- Every operation has a `default` response of type `ProblemDetails` (with `code` and `traceId`), and 422 errors use `HttpValidationProblemDetails`.
- What OpenAPI cannot carry well (notification types and their meaning, SignalR events, error codes) is in `docs/contract-extras.json`.
- Tested command: `openapi-generator generate -i docs/openapi/v1.json -g dart -o labaik_api` (version v7.10.0).

### Authentication

- Every route requires `Authorization: Bearer <accessToken>` except the `/auth/*` routes listed as public in section 5.
- `accessToken` is a JWT valid for **15 minutes** (default setting). The actual value comes in `expiresIn` (seconds) in every token response.
- `refreshToken` is valid for **7 days** and is **single use**: every `POST /auth/refresh-token` returns a new pair and revokes the old one. Reusing a used or expired token returns 401 with title `The refresh token is invalid or expired.`; the user must then log in again.
- `POST /auth/reset-password` revokes all of the user's refresh tokens.
- JWT contents: `sub` (user id), `email` and `name`, plus role claims for accounts that have a role. The recommended way to read roles is `user.roles` in the token response, or `GET /auth/me`.
- Role changes (such as being granted the supervisor role) take effect from the **next token**, i.e. after `refresh-token`. On 403, refresh the token and retry the request once (as the app already does).
- Password rules: at least 8 characters, with an uppercase letter, a lowercase letter, a digit and a symbol. If the password breaks the Identity rules the error is 400 with the message in `title`.
- The OTP verification code has 6 digits, is valid for 10 minutes, allows 5 attempts, and cannot be resent within 55 seconds (otherwise 429).

### Error shape (ProblemDetails, RFC 9457)

A business error comes back in this shape (`Content-Type: application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The user is already a member of this group.",
  "status": 409,
  "instance": "POST /api/v1/company/groups/7e8f9a0b-1c2d-4e3f-8a5b-6c7d8e9f0a1b/members",
  "code": "Group.AlreadyMember",
  "traceId": "00-2895e5aa0d41af41437e965bf73a1eee-3a607953c8512a5f-00"
}
```

- **`code`** is stable and machine-readable, in the form `Area.Reason`; the app should rely on it to pick the localized message. The full list is below. Existing codes will not change; new ones are only added.
- `title` is an English message for logs, not for display. Historical exception: logging in with an unconfirmed account returns 403 with `code = Auth.EmailNotConfirmed` and `title = email_not_confirmed`.
- **Responses without `code`**: the 401 issued by the authentication layer itself (missing or expired token, `title = Unauthorized`), the 403 from a role policy (`title = Forbidden`), the 500 (`An unexpected error occurred.`), and 422 errors. Handle them by `status`.
- Request-shape errors (FluentValidation) return 422 with an `errors` dictionary and no `code`. **Keys are camelCase JSON paths matching the request body**: `fullName`, `arrival.entryPoint`, `stays[0].placeName`, `days[1].activities[0].title`. The only exception is `destination` in `GET /map/route` (when `destinationId` and `toLat`/`toLng` are all missing).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "instance": "POST /api/v1/auth/register",
  "errors": {
    "email": ["'Email' is not a valid email address."],
    "password": ["The length of 'Password' must be at least 8 characters. You entered 5 characters."]
  },
  "traceId": "00-..."
}
```

- `traceId` links the error to the server logs. Include it in any bug report.
- SignalR errors arrive as a `HubException` whose message starts with the same code, e.g. `Location.SharingDisabled: ...`.

### Error codes (`code`)

The number in parentheses is the `status`. Codes of type Validation and Failure are both 400.

| Family | Codes |
| --- | --- |
| `Auth` | `Auth.InvalidCredentials` (401), `Auth.EmailNotConfirmed` (403), `Auth.InvalidRefreshToken` (401), `Auth.InvalidGoogleToken` (401), `Auth.ExternalEmailNotVerified` (401), `Auth.GoogleNotConfigured` (400), `Auth.ExternalLinkFailed` (400), `Auth.ResetFailed` (400), `Auth.Required` (401) |
| `Otp` | `Otp.Invalid` (400), `Otp.Expired` (400), `Otp.NotFound` (400), `Otp.TooManyAttempts` (400), `Otp.Throttled` (429) |
| `User` | `User.EmailTaken` (409), `User.NotFound` (404), `User.CreationFailed` (400), `User.RoleChangeFailed` (400) |
| `Company` | `Company.NotFound` (404), `Company.SupervisorNotFound` (404), `Company.OwnerAlreadyHasCompany` (409), `Company.RegistrationNumberTaken` (409), `Company.SupervisorAlreadyAdded` (409), `Company.OwnerCannotBeSupervisor` (409), `Company.NameRequired`, `Company.NameTooLong`, `Company.RegistrationNumberRequired`, `Company.ContactNameRequired`, `Company.TooLong`, `Company.InvalidUrl`, `Company.NegativeNumber`, `Company.ContractEndBeforeStart`, `Company.OwnerRequired`, `Company.SupervisorRequired` (all 400) |
| `Trip` | `Trip.NotFound` (404), `Trip.CannotEditItinerary` (403), `Trip.NameRequired`, `Trip.DeparturePlaceRequired`, `Trip.DestinationsRequired`, `Trip.InvalidDestination`, `Trip.TooLong`, `Trip.ReturnBeforeDeparture`, `Trip.InvalidExpectedPilgrims`, `Trip.CompanyRequired`, `Trip.ItineraryTooLarge`, `Trip.DuplicateItineraryDay`, `Trip.ItineraryDayOutsideTrip`, `Trip.InvalidActivity` (all 400) |
| `Group` | `Group.NotFound` (404), `Group.MemberNotFound` (404), `Group.NotLeader` (403), `Group.NotPilgrim` (403), `Group.AlreadyMember` (409), `Group.CannotRemoveSupervisor` (409), `Group.NameRequired`, `Group.NameTooLong`, `Group.JoinCodeRequired`, `Group.LeaderRequired`, `Group.TripRequired`, `Group.AddressRequired`, `Group.AddressTooLong`, `Group.InvalidCoordinates`, `Group.InvalidAttendanceStatus`, `Group.NotesTooLong` (all 400) |
| `Announcement` | `Announcement.NotFound` (404), `Announcement.NotCompanyOwner` (403), `Announcement.TitleRequired`, `Announcement.TitleTooLong`, `Announcement.BodyRequired`, `Announcement.BodyTooLong`, `Announcement.InvalidImportance`, `Announcement.LocationTooLong`, `Announcement.InvalidImageUrl` (all 400) |
| `Chat` | `Chat.NotMember` (403), `Chat.BodyRequired`, `Chat.BodyTooLong`, `Chat.InvalidLocation`, `Chat.InvalidKind` (all 400) |
| `Notification` | `Notification.NotFound` (404), `Notification.UserRequired` (400) |
| `Location` | `Location.SharingDisabled` (409), `Location.InvalidLatitude`, `Location.InvalidLongitude`, `Location.InvalidAccuracy`, `Location.UserRequired` (all 400) |
| `Map` | `Map.PlaceNotFound` (404), `Map.EmergencyNotFound` (404), `Map.InvalidCoordinates`, `Map.NoteTooLong`, `Map.UserRequired`, `Map.InvalidKey`, `Map.InvalidContent`, `Map.FacilityKindMismatch` (all 400) |
| `LostChild` | `LostChild.NotFound` (404), `LostChild.NotReporter` (403), `LostChild.InvalidAge`, `LostChild.DescriptionRequired`, `LostChild.TooLong`, `LostChild.InvalidPhotoUrl`, `LostChild.ReporterRequired` (all 400) |
| `FamilyCircle` | `FamilyCircle.NotFound` (404), `FamilyCircle.MemberNotFound` (404), `FamilyCircle.NotOwner` (403), `FamilyCircle.AlreadyMember` (409), `FamilyCircle.Full` (409), `FamilyCircle.OwnerCannotLeave` (409), `FamilyCircle.NameRequired`, `FamilyCircle.NameTooLong`, `FamilyCircle.JoinCodeRequired`, `FamilyCircle.UserRequired` (all 400) |
| `Journey` | `Journey.NotFound` (404), `Journey.NotEditable` (409), `Journey.InvalidTransition` (409), `Journey.InvalidPilgrimageType`, `Journey.EntryPointRequired`, `Journey.PlaceNameRequired`, `Journey.CityRequired`, `Journey.ArrivalPlanRequired`, `Journey.StayRequired`, `Journey.UserRequired` (all 400) |
| `Ritual` | `Ritual.NoSteps` (404), `Ritual.StepNotFound` (404), `Ritual.StepOutOfOrder` (409), `Ritual.AlreadyCompleted` (409), `Ritual.TitleRequired`, `Ritual.BodyRequired`, `Ritual.InvalidOrder`, `Ritual.UserRequired` (all 400) |
| `Guide` | `Guide.NotFound` (404), `Guide.TitleRequired`, `Guide.BodyRequired`, `Guide.InvalidOrder` (all 400) |
| `Profile` | `Profile.InvalidGender`, `Profile.InvalidNationality`, `Profile.InvalidPhotoUrl`, `Profile.HealthNotesTooLong`, `Profile.UserRequired` (all 400) |

Most 400 codes are preceded by 422 checks on the request shape, so they rarely appear. The codes the app actually needs to handle are mostly the 401, 403, 404, 409 and 429 ones above.

### Status codes

| Code | Meaning in this API |
| --- | --- |
| 200 | Success with a body. |
| 201 | Created (e.g. trips, announcements, messages, reports). |
| 204 | Success without a body (mark as read, confirm arrival, send location), or "no value yet" in `GET .../gathering-point` and `GET /pilgrim/supervisor`. |
| 400 | A Validation-type business rule inside the domain (e.g. invalid coordinates), Identity rejecting the password, or `Google sign-in is not available.` |
| 401 | No token or invalid token, wrong credentials, or invalid refresh token. |
| 403 | Role not allowed (Admin, Company or Supervisor), you are not the group supervisor, or `email_not_confirmed`. |
| 404 | Not found, **or** exists but is not yours. We never reveal other people's resources. |
| 409 | Conflict: duplicate member, ritual step out of order, duplicate commercial registration number, journey not editable, location sharing off, and so on. |
| 422 | Invalid request shape (`errors` dictionary). |
| 429 | Verification code requested before the 55-second cooldown ended. |
| 500 | Unexpected error, title `An unexpected error occurred.` |

### Cursor pagination

Used in `GET /notifications` and `GET /groups/{groupId}/messages`:

- Order is newest to oldest.
- `take` defaults to 50, maximum 100 (more returns 422).
- Next page: send `before` = the oldest timestamp in the current page (`createdAtUtc` for notifications, `sentAtUtc` for messages), URL-encoded (`Uri.encodeQueryComponent`).
- A page shorter than `take` means the end.

---

## 3. Account model and top-down provisioning

| Account | How it is created | Role in `user.roles` |
| --- | --- | --- |
| Pilgrim | Registers in the app (`register` then `verify-email`), or Google, or is created by the company when added to a group with a new email. | none (empty list) |
| Company | Created by the admin from the web dashboard (`POST /admin/companies`). | `Company` |
| Supervisor | Created by the company (`POST /company/supervisors`) and added to its roster. | `Supervisor` |
| Admin | From server configuration (`Admin:Emails`): created at server startup with a temporary password sent by email; sign in with the normal `login`. | `Admin` |

### Temporary password flow

When an account is created top-down (company, supervisor, or pilgrim added by the company):

1. **New email**: the server creates the account already confirmed (`EmailConfirmed = true`, no OTP), with the right role, and sends an email containing the login email and a temporary password.
2. **Registered and confirmed email**: the account is linked as is (the role is added if needed) and a notice is emailed without a password. Its current password does not change. (A registered pilgrim added to a group by the company gets no email.)
3. **Registered but unconfirmed email**: treated as a new account: its password is replaced by a temporary one sent to the real address, the account is confirmed, and its old sessions are revoked.

The user then logs in normally with `POST /auth/login` and the temporary password. The response has `mustChangePassword: true`; only `/auth/change-password`, `/auth/logout`, `/auth/me`, and `/auth/refresh-token` are available until the user changes it. `POST /auth/change-password` takes `{ currentPassword, newPassword }` and returns fresh tokens with `mustChangePassword: false`. Reissuing a temporary password revokes the user's refresh tokens.

---

## 4. Enums

All are integers. A new value is always added with a new number; existing values are never renumbered.

### Notifications `NotificationType`

| Value | Name | Recipient | `referenceId` | `preview` |
| --- | --- | --- | --- | --- |
| 1 | `AnnouncementPosted` | Group members except the author, or all company pilgrims for a company announcement | announcement id | title |
| 2 | `MemberJoinedGroup` | Group supervisor | member id | member name |
| 3 | `GroupCreated` | Company account | group id | group name |
| 4 | `SupervisorActivated` | Company account | supervisor id | supervisor name |
| 5 | `GroupWithoutSupervisor` | Company account (first pilgrim in a group with no supervisor, once) | group id | group name |
| 6 | `GatheringPointChanged` | Group members except whoever changed it | group id | address |
| 7 | `PilgrimArrived` | Group supervisor | pilgrim id | pilgrim name |
| 8 | `GroupMessagePosted` | Group members except the sender, when a pilgrim writes in the group chat | message id | message text (or `null` for a location without a caption) |
| 9 | `SupervisorMessagePosted` | Group pilgrims, when the supervisor writes in the group chat | message id | message text |
| 10 | `EmergencyReported` | Supervisors of the pilgrim's groups | emergency alert id | pilgrim name |
| 11 | `LostChildReported` | The reporter's supervisors, and with `shareWithNearby` everyone sharing a fresh location within 1 km | report id | child description |
| 12 | `FamilyMemberJoined` | Family circle owner | circle id | new member name |

Note: value 12 was added with family circles in B6, so the list goes up to 12, not 11. Mapping for the pilgrim's four notification types: new announcement = 1, new message from the supervisor = 9, gathering point changed = 6, new group message = 8. Type 9 currently means the supervisor's message in the group chat, and will be reused for direct messages once they are built. `preview`, `groupId` and `referenceId` can be `null`. The app builds the displayed text from the type.

### Company and dashboard

| enum | Values |
| --- | --- |
| `CompanyCategory` | 1 `Umrah`, 2 `Hajj`, 3 `HajjAndUmrah` |
| `SubscriptionPeriod` | 1 `Month`, 2 `ThreeMonths`, 3 `SixMonths`, 4 `Year` |
| `CompanyStatus` (computed, not sent) | 1 `Active`, 2 `Stopped` (stopped by the admin), 3 `Expired` (`contractEndDate` passed) |
| `TripStatus` (computed) | 1 `Upcoming`, 2 `Departed` (today in UTC is on or after the departure date) |

### Groups and members

| enum | Values |
| --- | --- |
| `GroupMemberRole` | 1 `Leader` (the supervisor), 2 `Member` (pilgrim) |
| `AttendanceStatus` | 1 `Present`, 2 `Absent` (default) |
| `Gender` | 1 `Male`, 2 `Female` |

### Chat and announcements

| enum | Values |
| --- | --- |
| `ChatMessageKind` | 1 `Text`, 2 `Location` |
| `AnnouncementImportance` | 1 `Normal`, 2 `Important` |
| `AnnouncementAudience` | 1 `Group`, 2 `AllCompanyPilgrims` |

### Personal journey, rituals and guides

| enum | Values |
| --- | --- |
| `PilgrimageType` | 1 `Umrah`, 2 `Hajj` |
| `TravelParty` | 1 `Solo`, 2 `Family`, 3 `Group` |
| `TransportMode` | 1 `Flight`, 2 `Bus`, 3 `PrivateCar` |
| `AccommodationType` | 1 `Hotel`, 2 `Other` |
| `JourneyStatus` | 1 `Draft`, 2 `Ready`, 3 `Active`, 4 `Completed` |
| `RitualPhase` | 1 `Preparation`, 2 `Ihram`, 3 `Tawaf`, 4 `Sai`, 5 `HalqOrTaqsir`, 6 `Completion` |
| `RitualStepKind` | 1 `Pillar`, 2 `Obligation`, 3 `Sunnah`, 4 `Guidance` |
| `GuideCategory` | 1 `IhramPreparation` |

### Field mode, help center and family

| enum | Values |
| --- | --- |
| `MapPlaceType` | 1 `MainGate`, 2 `SubGate`, 3 `Facility` |
| `FacilityKind` (facilities only) | 1 `Restrooms`, 2 `Medical`, 3 `ZamzamWater`, 4 `LostAndFound`, 5 `WheelchairService` |
| `EmergencyStatus` | 1 `Open`, 2 `Resolved` |
| `LostChildReportStatus` | 1 `Open`, 2 `Found` |
| `FamilyCircleRole` | 1 `Owner`, 2 `Member` |

---

## 5. Routes

All routes are under `/api/v1`. Access:
- **Public**: no token.
- **Signed in**: any valid token.
- **Member**: a member of the group or circle. Non-members get 404.
- **Group supervisor**: the group's `LeaderId`. A regular member gets 403, a non-member 404.
- **Company / Supervisor / Admin**: role policy. The `Supervisor` policy also accepts the admin.

Optional fields are marked with `?`. The full DTO shapes are under each table.

### 5.1 Authentication `/auth`

| Method | Path | Access | Body | Response |
| --- | --- | --- | --- | --- |
| POST | `/auth/register` | Public | `{ fullName, email, password }` | 200 `{ message }`, and an OTP is sent. Re-registering an unconfirmed email sends a new code (not 409). A confirmed email gives 409. |
| POST | `/auth/verify-email` | Public | `{ email, code }` | 200 `AuthTokens` |
| POST | `/auth/login` | Public | `{ email, password }` | 200 `AuthTokens`. Unconfirmed: 403 `email_not_confirmed`. Wrong: 401. |
| POST | `/auth/google` | Public | `{ idToken }` | 200 `AuthTokens`. If Client IDs are not configured on the server: 400. |
| POST | `/auth/refresh-token` | Public | `{ refreshToken }` | 200 `AuthTokens` (new pair, the old one is revoked) |
| POST | `/auth/resend-code` | Public | `{ email }` | 200 `{ message }` always, or 429 within 55 seconds |
| POST | `/auth/forgot-password` | Public | `{ email }` | 200 `{ message }` always (does not reveal whether the email exists) |
| POST | `/auth/reset-password` | Public | `{ email, code, newPassword }` | 200 `{ message }` |
| GET | `/auth/me` | Signed in | | 200 `{ id, email, name, roles: string[] }` |

```
AuthTokens { accessToken, refreshToken, tokenType: "Bearer", expiresIn: int (seconds), user: AuthUserSummary }
AuthUserSummary { id, email, fullName, roles: string[] }   // roles: [] for a pilgrim, or "Admin" / "Supervisor" / "Company"
```

### 5.2 Admin dashboard `/admin` (Admin only)

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| GET | `/admin/overview` | | 200 `AdminOverviewDto` |
| GET | `/admin/companies` | | 200 `AdminCompanyListItemDto[]` (newest first) |
| POST | `/admin/companies` | `CreateCompanyCommand` | 201 `AdminCompanyDto`. 409 for a duplicate registration number or an account that already owns a company. |
| GET | `/admin/companies/{id}` | | 200 `AdminCompanyDto` |
| PUT | `/admin/companies/{id}` | `UpdateCompanyRequest` (same form without `ownerEmail`, full replace) | 200 `AdminCompanyDto` |
| POST | `/admin/companies/{id}/stop` | | 200 `AdminCompanyDto` (status 2) |
| POST | `/admin/companies/{id}/resume` | | 200 `AdminCompanyDto` |
| GET | `/admin/users?email=` | | 200 `UserAccountDto` |
| PUT | `/admin/users/{userId}/supervisor` | | 200 `UserAccountDto` (grants the Supervisor role) |
| DELETE | `/admin/users/{userId}/supervisor` | | 200 `UserAccountDto` |

```
CreateCompanyCommand {
  name, commercialRegistrationNumber, contactFullName, ownerEmail,        // required
  category?, officialEmail?, primaryPhone?, logoUrl? (https), country?, city?,
  vatId?, contractReferenceNumber?, subscriptionPrice? (>= 0), subscriptionPeriod?,
  contractStartDate? (yyyy-MM-dd), contractEndDate? (>= start), maxSupervisors?, maxPilgrims?,
  contractFileUrl? (https), contactJobTitle?, contactMobile?,
  enabledServices? { gpsTracking, duaAndRituals, instantNotifications, chatWithGuides, reportsAndStats }  // if omitted: all true
}
AdminCompanyDto { id, name, ownerUserId, ownerEmail, createdAtUtc, + every form field above (except ownerEmail),
  enabledServices, isStopped, status: CompanyStatus, daysRemaining?: int,
  stats { tripsCount, activeTripsCount, groupsCount, supervisorsCount, activePilgrims } }
AdminCompanyListItemDto { id, name, logoUrl?, activePilgrims, subscriptionStartUtc? (yyyy-MM-dd), subscriptionEndUtc? (yyyy-MM-dd), status, daysRemaining? }
AdminOverviewDto { pendingRequests (0 for now), activeSubscriptions, subscriptionsExpiringIn30Days, activePilgrims, activeCompanies, totalCompanies }
UserAccountDto { id, email, fullName, emailConfirmed, roles: string[] }
```

`daysRemaining` = days until `contractEndDate` (`null` without a date, 0 once it has passed). "Active pilgrim" = member of a company group whose trip has not ended. Billing, limits and service flags are stored only and not enforced.

### 5.3 Company operations `/company` (Company only)

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| GET | `/company/me` | | 200 `CompanyDto` |
| PUT | `/company/me` | `{ name, commercialRegistrationNumber, contactFullName, logoUrl?, country?, city?, officialEmail?, primaryPhone? }` | 200 `CompanyDto` (contract fields are admin-only) |
| GET | `/company/home` | | 200 `{ activeTrips, pilgrimsCount, supervisorsCount, groupsCount }` |
| POST | `/company/supervisors` | `{ fullName, email, phoneNumber? }` | 201 `CompanySupervisorDto`. 409 if already on the roster or if it is the company account itself. |
| GET | `/company/supervisors` | | 200 `CompanySupervisorDto[]` (by name) |
| POST | `/company/trips` | `CreateTripCommand` | 201 `TripDto` |
| GET | `/company/trips` | | 200 `TripSummaryDto[]` (soonest first) |
| GET | `/company/trips/{id}` | | 200 `TripDto` |
| PUT | `/company/trips/{id}` | same fields as create | 200 `TripDto` |
| POST | `/company/trips/{tripId}/groups` | `{ name }` | 201 `CompanyGroupDto` (no supervisor) |
| GET | `/company/trips/{tripId}/groups` | | 200 `CompanyGroupDto[]` |
| POST | `/company/groups/{groupId}/members` | `{ email, fullName?, phoneNumber? }` | 200 `CompanyGroupDto`. New email = new pilgrim account. 409 if already a member. |
| GET | `/company/groups/{groupId}/members` | | 200 `GroupMemberRowDto[]` (no health notes) |
| GET | `/company/groups/{groupId}/members/{userId}` | | 200 `GroupMemberDetailsDto` (includes health notes) |
| DELETE | `/company/groups/{groupId}/members/{userId}` | | 204. The supervisor cannot be removed (409). |
| PUT | `/company/groups/{groupId}/supervisor` | `{ email }` | 200 `CompanyGroupDto`. Roster supervisors only, otherwise 404. Replaces the previous supervisor. |
| POST | `/company/announcements` | `{ title, body, importance?, gatheringTimeUtc?, location?, imageUrl? }` | 201 `AnnouncementDto` (`audience = 2`, to every company pilgrim) |

```
CompanyDto { id, name, logoUrl?, commercialRegistrationNumber, country?, city?, officialEmail?, primaryPhone?, contactFullName }
CompanySupervisorDto { userId, fullName, email, phoneNumber?, addedAtUtc, groupsCount }
CreateTripCommand { name, pilgrimageType, destinations: string[], departureDate (yyyy-MM-dd), returnDate?, departurePlace, expectedPilgrims (>= 1), notes? }
TripDto { id, name, pilgrimageType, destinations, departureDate, returnDate?, departurePlace, expectedPilgrims, notes?, status: TripStatus, createdAtUtc }
TripSummaryDto { id, name, pilgrimageType, departureDate, returnDate?, expectedPilgrims, status }
CompanyGroupDto { id, tripId, name, joinCode, supervisorId?, supervisorName?, pilgrimCount }
```

### 5.4 Supervisor features

| Method | Path | Access | Body | Response |
| --- | --- | --- | --- | --- |
| GET | `/supervisor/groups` | Supervisor | | 200 `SupervisorGroupDto[]` (soonest first) |
| GET | `/home?groupId=` | Member | | Picks the group shown on home (see 5.5) |
| PUT | `/groups/{groupId}/gathering-point` | Group supervisor | `{ address, latitude, longitude, gatheringTimeUtc? }` | 200 `GatheringPointDto`, and sends notification 6 |
| GET | `/groups/{groupId}/gathering-point` | Member | | 200 `GatheringPointDto`, or 204 if not set |
| GET | `/groups/{groupId}/attendance` | Group supervisor | | 200 `GroupAttendanceDto` |
| PUT | `/groups/{groupId}/attendance/{userId}` | Group supervisor | `{ status }` (1 or 2) | 200 `GroupAttendanceDto` |
| POST | `/groups/{groupId}/attendance/reset` | Group supervisor | | 200 `GroupAttendanceDto` (everyone absent) |
| POST | `/groups/{groupId}/attendance/confirm` | Pilgrim in the group | | 204, once; sends notification 7 to the supervisor |
| GET | `/groups/{groupId}/members` | Group supervisor | | 200 `GroupMemberRowDto[]` |
| GET | `/groups/{groupId}/members/{userId}` | Group supervisor | | 200 `GroupMemberDetailsDto` |
| PUT | `/groups/{groupId}/members/{userId}/notes` | Group supervisor | `{ notes? }` (`null` clears) | 204 |
| POST | `/groups/{groupId}/announcements` | Group supervisor | `{ title, body, importance?, gatheringTimeUtc?, location?, imageUrl? }` | 201 `AnnouncementDto` |
| GET | `/trips/{id}/itinerary` | The company, and supervisors and pilgrims of the trip's groups | | 200 `TripItineraryDto` |
| PUT | `/trips/{id}/itinerary` | The company or a supervisor on the trip (pilgrim 403) | `{ days: ItineraryDayDto[] }` | 200 `TripItineraryDto` (full replace) |

```
SupervisorGroupDto { id, name, tripId?, tripName?, departureDate?, pilgrimCount, presentCount, absentCount }
GatheringPointDto { address, latitude, longitude, gatheringTimeUtc?, updatedAtUtc }
GroupAttendanceDto { presentCount, absentCount, members: [ { userId, fullName?, status: AttendanceStatus, confirmedAtUtc? } ] }
GroupMemberRowDto { userId, fullName?, email?, role, memberNumberInGroup?, gender?, nationality?, photoUrl?, attendanceStatus, joinedAtUtc }
GroupMemberDetailsDto { userId, fullName?, email?, role, memberNumberInGroup?, gender?, nationality?, photoUrl?, healthNotes?, notes?, attendanceStatus, joinedAtUtc }
TripItineraryDto { tripId, tripName, days: ItineraryDayDto[], updatedAtUtc? }
ItineraryDayDto { date (yyyy-MM-dd), title?, activities: [ { title, timeUtc?, place? } ] }
```

`healthNotes` is sensitive data: it only appears in `GroupMemberDetailsDto`, for the group supervisor and the owning company. Itinerary days must fall between the departure and return dates, without duplicates, with at most 60 days and 30 activities per day.

### 5.5 Pilgrim: home, personal journey, rituals, guides and profile

| Method | Path | Body / Query | Response |
| --- | --- | --- | --- |
| GET | `/home` | `?groupId=` optional (member only, otherwise 404) | 200 `HomeDto` |
| POST | `/journeys` | `CreateJourneyCommand` | 201 `{ id }` |
| GET | `/journeys/me` | | 200 `JourneyDto`, or 404 if none |
| GET | `/journeys/{id}` | | 200 `JourneyDto` (owner only) |
| PUT | `/journeys/{id}` | `UpdateJourneyRequest` | 200 `JourneyDto`. Allowed only in Draft and Ready, 409 afterwards. |
| GET | `/rituals/steps?pilgrimageType=` | default 1 | 200 `RitualStepDto[]` |
| GET | `/rituals/progress?pilgrimageType=` | progress starts on the first call | 200 `RitualProgressDto` |
| POST | `/rituals/progress/complete` | `{ pilgrimageType, stepOrder }` | 200 `RitualProgressDto`. In order only, otherwise 409. |
| POST | `/rituals/progress/reset` | `{ pilgrimageType }` | 200 `RitualProgressDto` |
| GET | `/guides?category=` | default 1 | 200 `GuideArticleDto[]` |
| GET | `/guides/{id}` | | 200 `GuideArticleDto` |
| GET | `/me/profile` | | 200 `MyProfileDto` |
| PUT | `/me/profile` | `{ gender?, nationality? (two-letter country code), photoUrl? (https), healthNotes? }` | 200 `MyProfileDto` (full replace, `null` clears) |

```
HomeDto { journey?: { id, pilgrimageType, status, isFirstTime },
          ritual?: { pilgrimageType, isStarted, isCompleted, completedCount, totalSteps, nextStep?: RitualStepDto },
          group?: { id, name, leaderName?, memberCount, myRole, unreadAnnouncements, tripId?, unreadMessages },
          unreadNotifications }
CreateJourneyCommand { pilgrimageType, isFirstTime, travelParty, arrival: { mode, entryPoint }, stays: [ { cityName, accommodationType, placeName, arrivalAtUtc } ] }
UpdateJourneyRequest { isFirstTime, travelParty, arrival, stays, pilgrimageType? }   // pilgrimageType: if omitted, unchanged
JourneyDto { id, pilgrimageType, isFirstTime, travelParty, status, arrivalPlan?: { mode, entryPoint }, stays: [ { cityName, accommodationType, placeName, arrivalAtUtc } ] }
RitualStepDto { id, pilgrimageType, order, phase, kind, title, body, reference? }
RitualProgressDto { id, pilgrimageType, currentOrder, totalSteps, completedCount, isCompleted, startedAtUtc, completedAtUtc?, currentStep?, completedSteps: [ { stepOrder, completedAtUtc } ] }
GuideArticleDto { id, category, order, title, body, reference? }
MyProfileDto { gender?, nationality?, photoUrl?, healthNotes?, updatedAtUtc? }
```

Any section of `HomeDto` is `null` when it does not apply (no journey, no group, no published steps). The default group is the most recently joined one. The ritual type on home follows the personal journey's type.

### 5.6 Announcements

| Method | Path | Access | Response |
| --- | --- | --- | --- |
| GET | `/groups/{groupId}/announcements` | Member | 200 `AnnouncementDto[]` (newest first, includes company announcements to all pilgrims) |
| POST | `/groups/{groupId}/announcements` | Group supervisor | 201 `AnnouncementDto` |
| POST | `/company/announcements` | Company | 201 `AnnouncementDto` |
| POST | `/announcements/{id}/read` | Whoever can see it | 204 (repeating has no effect) |

```
AnnouncementDto { id, groupId?, title, body, authorId, authorName?, createdAtUtc, isRead,
                  importance: AnnouncementImportance, audience: AnnouncementAudience, gatheringTimeUtc?, location?, imageUrl? }
```

### 5.7 Group chat (group members)

| Method | Path | Body / Query | Response |
| --- | --- | --- | --- |
| POST | `/groups/{groupId}/messages` | `{ body?, kind?, latitude?, longitude? }` | 201 `ChatMessageDto` |
| GET | `/groups/{groupId}/messages` | `?before=&take=` | 200 `ChatMessageDto[]` (newest first) |
| POST | `/groups/{groupId}/messages/read` | | 204: everything up to now is read for me |
| GET | `/groups/{groupId}/messages/unread-count` | | 200 `{ count }` |

- `kind` omitted = 1 text, and `body` is required (up to 2000 characters).
- `kind = 2` location: `latitude` and `longitude` are required, and `body` is an optional caption (comes back as `""` when not sent).
- `isRead` is for the current user: their own messages are always read, and other people's messages are read if they were sent before (or at) the user's last `messages/read`.
- The read marker never moves back. The unread count also appears in `home.group.unreadMessages`.
- Sending is over REST only; real-time delivery is via `/hubs/events` (`MessageReceived` event).

```
ChatMessageDto { id, groupId, senderId, senderName?, body, sentAtUtc, kind: ChatMessageKind, latitude?, longitude?, senderPhotoUrl?, isRead }
```

### 5.8 Notifications (signed in)

| Method | Path | Response |
| --- | --- | --- |
| GET | `/notifications?before=&take=` | 200 `NotificationDto[]` (newest first) |
| GET | `/notifications/unread-count` | 200 `{ count }` |
| POST | `/notifications/{id}/read` | 204 |
| POST | `/notifications/read-all` | 204 |

```
NotificationDto { id, type: NotificationType, groupId?, referenceId?, preview?, createdAtUtc, isRead }
```

Notifications are in-app only and arrive in real time via `NotificationReceived` on `/hubs/events`. There is no push notification while the app is closed (see section 8).

### 5.9 Location (signed in)

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| GET | `/location/me` | | 200 `MyLocationSharingDto` |
| PUT | `/location/sharing` | `{ isSharing }` | 200 `MyLocationSharingDto`. Turning it off erases the last position. |
| POST | `/location` | `{ latitude, longitude, accuracyMeters? }` | 204. If sharing is off: 409. |
| POST | `/map/location` | same body | exactly the same behavior (alias) |
| GET | `/groups/{groupId}/locations` | | 200 `MemberLocationDto[]` (members only) |

```
MyLocationSharingDto { isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
MemberLocationDto { userId, fullName?, role, isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
```

Only the last position is kept (no history). A position older than 15 minutes is marked `isStale = true`.

### 5.10 Field mode and map `/map` (signed in)

| Method | Path | Body / Query | Response |
| --- | --- | --- | --- |
| GET | `/map/destinations?type=` | `type` optional (1, 2 or 3) | 200 `MapPlaceDto[]` (by type, then seed order) |
| GET | `/map/route` | `fromLat, fromLng` and (`destinationId` or `toLat` + `toLng`) | 200 `MapRouteDto`. Unknown id 404, missing destination 422. |
| POST | `/map/emergency` | `{ latitude, longitude, accuracyMeters?, note? (up to 500) }` | 201 `EmergencyAlertDto`, and sends notification 10 to the pilgrim's supervisors |
| GET | `/map/emergency/{id}` | | 200 `EmergencyAlertDto` (the reporter or one of their supervisors only) |
| POST | `/map/emergency/{id}/resolve` | | 200 `EmergencyAlertDto` (repeating has no effect) |
| POST | `/map/location` | see 5.9 | 204 |

```
MapPlaceDto { id, key, type: MapPlaceType, facilityKind?, name (Arabic), number?, description? (Arabic), latitude, longitude, imageUrl? }
MapRouteDto { from: { latitude, longitude }, to: { latitude, longitude }, destination?: MapPlaceDto, distanceMeters, bearingDegrees (0 north, 90 east), walkingMinutes }
EmergencyAlertDto { id, userId, fullName?, latitude, longitude, accuracyMeters?, note?, status: EmergencyStatus, createdAtUtc, resolvedAtUtc? }
```

`/map/route` gives straight-line guidance only (distance, bearing and walking time at 1.2 m/s). Drawing the path and turn-by-turn navigation are the job of the app's map library. `imageUrl` is currently `null` for every place, so use the app's images. `key` is stable (e.g. `gate-king-abdulaziz`) and can be used to map images locally.

### 5.11 Help center (signed in)

| Method | Path | Body / Query | Response |
| --- | --- | --- | --- |
| GET | `/pilgrim/supervisor` | `?groupId=` optional | 200 `SupervisorContactDto`, or 204 if there is no supervisor, or 404 for a group you are not in |
| POST | `/lost-child-reports` | `ReportLostChildCommand` | 201 `LostChildReportDto`, and sends notification 11 |
| GET | `/lost-child-reports/mine` | | 200 `LostChildReportDto[]` (newest first) |
| GET | `/lost-child-reports/{id}` | | 200 for the reporter or anyone who received notification 11, otherwise 404 |
| POST | `/lost-child-reports/{id}/found` | | 200 `LostChildReportDto` (reporter only; other notified users get 403) |

```
SupervisorContactDto { groupId, groupName, supervisorId, fullName?, email?, phoneNumber?, photoUrl? }
ReportLostChildCommand { approxAge (0 to 17), description (up to 1000), photoUrl? (https), latitude, longitude, locationDescription? (up to 300), shareWithNearby (default false) }
LostChildReportDto { id, reporterId, reporterName?, approxAge, description, photoUrl?, latitude, longitude, locationDescription?, shareWithNearby, status: LostChildReportStatus, createdAtUtc, foundAtUtc? }
```

"Nearby" means users sharing a fresh location (less than 15 minutes old) within 1 km of the report.

### 5.12 Family circle `/family-circles` (signed in, independent of company groups)

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| POST | `/family-circles` | `{ name }` (up to 100) | 201 `FamilyCircleDto` (you are the owner; share `joinCode`) |
| GET | `/family-circles/mine` | | 200 `FamilyCircleSummaryDto[]` |
| GET | `/family-circles/preview/{code}` | | 200 `FamilyCirclePreviewDto` (confirm screen) |
| POST | `/family-circles/join` | `{ code }` | 200 `FamilyCircleDto`. Already a member or circle full (20): 409. Wrong code: 404. |
| GET | `/family-circles/{id}` | | 200 `FamilyCircleDto` (members only) |
| GET | `/family-circles/{id}/locations` | | 200 `FamilyMemberLocationDto[]` |
| PUT | `/family-circles/{id}/location-sharing` | `{ isSharing }` | 200 `{ circleId, isSharingLocation }` |
| DELETE | `/family-circles/{id}/members/{userId}` | | 204. The owner removes others, a member leaves with their own id, the owner cannot leave (409). |

```
FamilyCircleSummaryDto { id, name, joinCode, myRole: FamilyCircleRole, memberCount }
FamilyCircleDto { id, name, joinCode, ownerId, myRole, members: [ { userId, fullName?, photoUrl?, role, isSharingLocation, joinedAtUtc } ] }
FamilyCirclePreviewDto { id, name, ownerName?, memberCount }
FamilyMemberLocationDto { userId, fullName?, isSharing, latitude?, longitude?, accuracyMeters?, updatedAtUtc?, isStale }
```

A member's location shows in the circle only if they turned on sharing **in this circle** (`location-sharing`) **and** their general sharing is on (`PUT /location/sharing`) and they have sent a position. Codes are uppercase (6 characters) and the lookup is case-insensitive. The "view location" button = `GET /family-circles/{id}/locations` with `/hubs/location`. The "start chat" button has no backend counterpart yet (direct chat is deferred).

### 5.13 Legacy admin group routes (not called by the app)

| Method | Path | Access | Response |
| --- | --- | --- | --- |
| POST | `/groups` | Admin | 200 `GroupDto { id, name, joinCode, leaderId?, members: [ { userId, role, joinedAtUtc } ] }` |
| GET | `/groups/{code}` | Signed in | 200 `GroupSummaryDto { id, name, leaderName?, memberCount }` |
| POST | `/groups/{code}/join` | Admin | 200 `GroupSummaryDto` |

---

## 6. SignalR

| Item | Value |
| --- | --- |
| Paths | `wss://labaik-api.onrender.com/hubs/location` and `wss://labaik-api.onrender.com/hubs/events` |
| Authentication | The token in the query string: `?access_token=<accessToken>` (WebSockets cannot send a header). |
| Transport | WebSocket directly, without negotiate, as the app already does. WebSockets must stay enabled on Render. |
| Subscription | On connect, the server adds the connection to every group you are a member of, and `/hubs/location` also adds it to every family circle you are in. Subscriptions are computed **at connect time only**: after joining a group or circle, or after refreshing the token, reconnect. |
| Delivery | Real-time pushes are best effort. Data is saved before the push, so re-fetch over REST after reconnecting. |

### `/hubs/location`

| Direction | Name | Payload |
| --- | --- | --- |
| Client to server | `UpdateLocation` | `(latitude: double, longitude: double, accuracyMeters: double?)`. Same as `POST /location`: sharing must be on, otherwise a `HubException` whose message starts with `Location.SharingDisabled`. Shape errors start with `Validation:`. |
| Server to client | `LocationUpdated` | `LiveLocationDto { userId, latitude, longitude, accuracyMeters?, updatedAtUtc }`. Reaches all your groups, and every circle in which the sender turned sharing on. |
| Server to client | `StoppedSharing` | `{ userId }`. When general sharing is turned off (all groups and circles), when it is turned off in a circle, or when leaving a circle. |

The same `LocationUpdated` may arrive twice if the person shares both a group and a circle with you. De-duplicate on `userId` and `updatedAtUtc`.

### `/hubs/events` (receive only)

| Direction | Name | Payload | Who receives |
| --- | --- | --- | --- |
| Server to client | `MessageReceived` | `ChatMessageDto` (always with `isRead = false`) | Every connected group member, including the sender. De-duplicate on `id`. |
| Server to client | `NotificationReceived` | `NotificationDto` | The recipient only (identified by `sub` in the token) |

There are no client-to-server methods on `/hubs/events`. Messages are sent over REST.

---

## 7. What Flutter must do now

- [ ] Remove the account-type screen and the `PUT /auth/account-type` call. Public signup is pilgrim only.
- [ ] Read roles from `user.roles` (or `GET /auth/me`): `Company` opens the company UI, `Supervisor` opens the supervisor UI, `Admin` opens the admin UI, and an empty list is a pilgrim. Accounts created top-down log in directly with the temporary password, without OTP.
- [ ] When `mustChangePassword` is true, show the required password-change screen and keep other API requests blocked until `/auth/change-password` succeeds.
- [ ] Serialize refresh-token requests with a single-flight lock. A refresh token can be used only once; concurrent refresh calls can invalidate the second request.
- [ ] After any role change or unexpected 403: `refresh-token`, retry the request once, then reconnect SignalR with the new token.
- [ ] Reconnect `/hubs/location` and `/hubs/events` when the group in `/home` changes or after joining a family circle.
- [ ] Handle notification types 8 to 12, and display unknown types generically instead of crashing. Open the right screen from `referenceId`: message (8 and 9), emergency (10, for the supervisor via `GET /map/emergency/{id}`), lost child (11, via `GET /lost-child-reports/{id}`), circle (12).
- [ ] Chat: add location messages (`kind = 2`), the sender photo, and `isRead`. Call `POST /groups/{groupId}/messages/read` when the chat is opened or scrolled to the bottom, and show `home.group.unreadMessages` or `messages/unread-count`.
- [ ] Show a "no supervisor yet" state when `leaderName`, `supervisorId` or `supervisorName` is empty.
- [ ] Handle `AnnouncementDto.groupId = null` (company announcement), and enable the "important announcement" badge (`importance = 2`), `unreadAnnouncements`, and `POST /announcements/{id}/read`.
- [ ] Company UI: wire `/company/*`. Add the supervisor with `POST /company/supervisors` **before** assigning them to a group. Adding a member with a new email creates their account, so do not treat it as an error.
- [ ] Supervisor UI: gathering point, attendance, member list and member card, trip program, and the group list (`/supervisor/groups` with `?groupId=` on home).
- [ ] Field mode: replace the default gate data with `GET /map/destinations`, and wire the emergency button to `POST /map/emergency`.
- [ ] Help center: `GET /pilgrim/supervisor` and the lost-child report.
- [ ] Family circle: replace the on-device demo data with the `/family-circles` routes.
- [ ] Personal journey: `pilgrimageType` can now be sent in `PUT /journeys/{id}`.
- [ ] Errors: pick the localized message by `code` (and by `status` when there is no `code`), and map 422 errors to fields through the camelCase `errors` keys.

### Fields that stay in Flutter (no place on the server)

- Route city order, traveler name, and the "not decided yet" choices.
- `travelParty` is multi-select in the app: send `Group` if selected, then `Family`, otherwise `Solo`.
- An unanswered `isFirstTime` is sent as `false`.
- Departure time: the app asks for one date and sends it as `arrivalAtUtc` of every stay (project owner's decision; the server still requires it per stay). Do not send the journey before it has the arrival mode and place and at least one stay.

---

## 8. Deferred or pending

| Item | Status |
| --- | --- |
| Direct chat (pilgrim with their supervisor, member with member) | Deferred by the project owner. Type 9 will be reused for direct supervisor messages. |
| FCM push while the app is closed | Deferred. Notifications are in-app only for now (REST + `NotificationReceived`). |
| Religious content review | Ritual (`/rituals`) and guide (`/guides`) texts are placeholders awaiting approval by a trusted religious authority before production. |
| Google sign-in | The code is ready (`POST /auth/google`). Waiting for the Android, iOS and Web Client IDs in the Render settings (`Authentication__Google__ClientIds__0..n`). Without them it returns 400. |
| Haram map data | Marked `PendingReview`: gate numbers and coordinates are approximate and will be checked against an official map. No images on the server yet (`imageUrl = null`). |
| Temporary password | Sent in plain text by email; the first sign-in now requires a password change. |
| File upload | There is no upload service. Images and files are `https` links only (`photoUrl`, `logoUrl`, `imageUrl`, `contractFileUrl`). |

### Operational notes

- **Render's free tier sleeps** after about 15 minutes without requests. The first request after that takes about 50 seconds, which is normal. The app's 60-second timeout is appropriate.
- **OpenAPI and Scalar are enabled on the deployed instance**: `https://labaik-api.onrender.com/openapi/v1.json` and `https://labaik-api.onrender.com/scalar/v1`. The `/admin/*` and `/company/*` routes include full request and response examples.
- Real email is sent through Brevo only when `Email:Provider = Brevo` with the key on Render. Otherwise the email (including the temporary password or verification code) is written to the server logs.
