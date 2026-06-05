# Google Login & Email Verification

## Prerequisites

- **.NET 10 SDK** (see `global.json`). The solution targets `net10.0`.
- **PostgreSQL** running locally (e.g. `docker compose up -d` in the repo root).
- **EF Core tools** (optional): `dotnet tool update --global dotnet-ef`

## Overview

1. Frontend obtains a Google **ID token** and sends it to `POST /api/auth/google`.
2. Backend validates the token with Google, creates or loads the user, and returns a JWT.
3. New users are `Pending` until they click the verification link in email (valid for **5 minutes**).
4. Protected APIs require JWT + `account_status` claim = `Active`.

## Configuration

Set values in `appsettings.json` or user secrets:

| Section | Keys |
|---------|------|
| `Google` | `ClientId` — Web client ID from Google Cloud Console |
| `Jwt` | `SecretKey` (32+ chars), `Issuer`, `Audience`, `ExpirationInMinutes` |
| `Smtp` | `Host`, `Port`, `Username`, `Password`, `FromEmail`, `FromName`, `UseSsl` |
| `App` | `FrontendBaseUrl`, `VerificationPath` |

### Gmail SMTP (development)

1. Enable 2FA on the Google account.
2. Create an [App Password](https://myaccount.google.com/apppasswords).
3. Set `Smtp:Username` to your Gmail address and `Smtp:Password` to the app password.

## API Endpoints

### `POST /api/auth/google`

```json
{ "idToken": "<google-id-token>" }
```

Response:

```json
{
  "accessToken": "...",
  "expiresAt": "2026-06-05T12:00:00Z",
  "user": {
    "id": "...",
    "email": "user@gmail.com",
    "fullName": "...",
    "avatarUrl": "...",
    "accountStatus": "Pending",
    "role": "Owner"
  },
  "requiresEmailVerification": true
}
```

### `GET /api/auth/verify-email?token={token}`

Activates the account and returns a new JWT with `Active` status.

### `POST /api/auth/resend-verification`

Requires `Authorization: Bearer <jwt>`. For `Pending` users only.

### `GET /api/auth/me`

Requires active account (`Active` policy).

## Frontend Integration

1. Add [Google Identity Services](https://developers.google.com/identity/gsi/web) with the same `ClientId` as the backend.
2. On credential callback, POST `credential` (ID token) to `/api/auth/google`.
3. Store `accessToken`; send `Authorization: Bearer {accessToken}` on API calls.
4. If `requiresEmailVerification` is true, show “check your email” and allow resend via `/api/auth/resend-verification`.
5. On route `{VerificationPath}?token=...`, call `GET /api/auth/verify-email?token=...` then store the new token.

## Database Migration

```bash
dotnet ef database update --project src/TaxMate.Model --startup-project src/TaxMate.API
```

Migration `AddGoogleAuthAndEmailVerification` replaces `Users.IsActive` with `AccountStatus` and adds verification columns.

## Authorization

- **401** — missing or invalid JWT.
- **403** — JWT valid but account is `Pending` on routes with `ActiveAccountOnly` policy.
