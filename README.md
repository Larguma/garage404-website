# garage404-website

Backend of the Garage404 website: an ASP.NET Core (.NET 10) REST API backed by MongoDB, with Firebase
Authentication, Google Calendar, email (SMTP) and Discord notifications.

## Architecture

```text
Website ──HTTPS──▶ OpenResty (reverse proxy) ──▶ garage404-backend (:8080) ──▶ MongoDB
                                                   ├── Firebase Auth   (login, JWT validation)
                                                   ├── Google Calendar (accepted reservations)
                                                   ├── SMTP            (confirmation emails)
                                                   └── Discord webhook (new reservations, admin changes)
```

- **Authentication**: `POST /v1/Auth` exchanges an email and a password for a Firebase ID token. Send it as
  `Authorization: Bearer <token>`. The signing keys are discovered from Firebase and refreshed automatically.
- **Authorization**: two levels, signed-in user and admin. The admin flag is stored on the user document in MongoDB
  and checked by the `Admin` policy.
- **Errors** follow RFC 9457 (`application/problem+json`) with a `traceId` to find the matching log entry.

## Endpoints

🌐 public · 🔑 signed-in user · 🛡️ admin

| Resource      | Endpoint                                                | Access |
| ------------- | ------------------------------------------------------- | ------ |
| Auth          | `POST /v1/Auth` (form: `username`, `password`)          | 🌐     |
| Bikes         | `GET /v1/Bike`, `GET /v1/Bike/{id}`                     | 🌐     |
|               | `POST /v1/Bike`, `PUT /v1/Bike/{id}`, `DELETE /v1/Bike/{id}` | 🛡️ |
|               | `POST /v1/Bike/{id}/images` (multipart, field `images`) | 🛡️     |
|               | `DELETE /v1/Bike/{id}/images/{fileName}`                | 🛡️     |
| Blog          | `GET /v1/Blog`, `GET /v1/Blog/{id}`, `GET /v1/Blog/slug/{slug}` | 🌐 (published only; admins also see drafts) |
|               | `POST /v1/Blog`, `PUT /v1/Blog/{id}`, `DELETE /v1/Blog/{id}` | 🛡️ |
| Reservations  | `POST /v1/Reservation`                                  | 🌐 (rate limited) |
|               | `GET /v1/Reservation`, `GET/PUT/DELETE /v1/Reservation/{id}` | 🛡️ |
|               | `PUT /v1/Reservation/{id}/accept` (body: Zurich time, e.g. `"2027-01-15T09:00:00"`) | 🛡️ |
| Service types | `GET /v1/ServiceType`                                   | 🌐     |
|               | `POST /v1/ServiceType` (array), `DELETE /v1/ServiceType/{type}` | 🛡️ |
| Users         | `GET /v1/User`                                          | 🔑     |
|               | `GET /v1/User/all`, `POST /v1/User/set-admin?userId=…&isAdmin=…` | 🛡️ |
| Health        | `GET /health`                                           | 🌐     |

The interactive documentation (Swagger UI at `/swagger`, spec at `/openapi/v1.json`) is only served when
`ASPNETCORE_ENVIRONMENT=Development`.

### Reservation flow

1. A customer sends `POST /v1/Reservation`. The service type must exist and the preferred date must not be in the past.
   A Discord message is sent; if Discord is down, the reservation is still saved.
2. An admin lists the requests and accepts one with `PUT /v1/Reservation/{id}/accept`: a Google Calendar event is
   created (or updated), then the customer receives a confirmation email. The response says whether the email was sent.
3. Deleting a reservation also deletes its calendar event.

## Configuration

Secrets are read from environment variables (or a `.env` file next to the app) and are never committed or baked
into the Docker image.

| Variable                                  | Description                                              |
| ----------------------------------------- | -------------------------------------------------------- |
| `Firebase__ApiKey`                        | Firebase Web API key, used by `POST /v1/Auth`            |
| `Firebase__ProjectId`                     | Firebase project id (default `garage404-68a8f`)          |
| `Firebase__CredentialsPath`               | Service account JSON for Google Calendar (default `garage404-68a8f.json`) |
| `GoogleCalendar__CalendarId`              | Calendar where accepted reservations are added           |
| `GoogleCalendar__DurationMinutes`         | Length of an appointment (default 120)                   |
| `Email__Username`, `Email__Password`, `Email__FromAddress` | SMTP account (Infomaniak by default)    |
| `Discord__WebhookUrl`                     | Discord webhook for notifications (optional)             |
| `BackendDatabase__ConnectionString`       | MongoDB connection string                                |
| `Cors__AllowedOrigins__0`, `__1`, …       | Origins allowed to call the API from a browser           |
| `RateLimiting__PublicWrite__PermitLimit` / `__WindowMinutes` | Reservations per IP (default 5 per 10 min) |
| `RateLimiting__Auth__PermitLimit` / `__WindowMinutes` | Login attempts per IP (default 10 per min)   |

## Running

```sh
docker compose up -d
```

`compose.yml` mounts `appsettings.json`, the service account JSON and the `.env` file from
`/home/larguma/docker_data/garage404-backend/`. MongoDB data and uploaded bike images are stored in named volumes.

The API trusts the `X-Forwarded-For` / `X-Forwarded-Proto` headers of the reverse proxy, so it must not be exposed
directly to the internet.

### First admin

Admins can only be created by another admin. For the very first one, sign in once (this creates the user document),
then set the flag in MongoDB:

```sh
docker exec garage404-backend-db mongosh Garage404Db --eval 'db.Users.updateOne({Email: "you@example.com"}, {$set: {IsAdmin: true}})'
```

Then create the service types: `GARAGE404_API_TOKEN=<admin token> garage404-backend/populateDB.sh`.

## Tests

`scripts/e2e.sh` runs about 90 end-to-end checks against a local, throwaway stack (see the header of the script).
Never run it against production: it promotes the test user to admin directly in MongoDB.
