# Pacer

Pacer is a small, mobile-friendly web app for tracking progress towards a goal by counting. 
Create a counter with a number to count up to and a date to reach it by, then tap a big button each time you make progress. 
Pacer tells you how many clicks per week you need to stay on track.

> **This project was written by [Claude](https://www.anthropic.com/claude), an AI assistant made by Anthropic.**

## Features

- Sign in with **Authentik** over OpenID Connect (OIDC)
- **Per-user data**: every counter belongs to the signed-in user and is never visible to anyone else
- Counters with a **name**, a **count to** number, a **target date**, and a count that starts at 0
- A large tap button that increments the counter instantly, plus an undo
- **Average clicks per week required** to reach the target by the target date (also shown per day)
- A **default counter** shown on the home page
- A small menu: **Add new counter**, **List counters**, **Change default counter** (plus Sign out)
- Edit and delete counters
- Light and dark mode, safe-area aware, 16px form inputs for iOS
- Built for **Docker Swarm** behind **Traefik**: data in **PostgreSQL**, OIDC settings from **Docker secret files**, safe to run several replicas
- **Unit tests** with xunit, plus optional PostgreSQL integration tests

## How the pace is calculated

```
remaining  = count_to - current
days_left  = target_date - today          (in APP_TIMEZONE)
per_day    = remaining / days_left
per_week   = per_day * 7
```

On the target date itself, or once it has passed, Pacer shows how many clicks remain instead of a weekly rate. When `current >= count_to` the counter shows "Target reached".

## Tech stack

| Part | Choice |
| --- | --- |
| Language / framework | C#, ASP.NET Core **Blazor Server** (.NET 10) |
| UI | Server-rendered Razor components; the browser talks to the server over SignalR (WebSockets) |
| OIDC | `Microsoft.AspNetCore.Authentication.OpenIdConnect` (authorization code flow with PKCE) |
| Data access | **Entity Framework Core 10** with the Npgsql PostgreSQL provider and code-first migrations (no hand-written SQL) |
| Login cookies | Cookie auth; data protection keys stored in the database so all replicas share them |
| Tests | xunit, EF Core SQLite in-memory for store tests, optional PostgreSQL integration tests |

All business rules live in plain C# classes (`CounterService`, `Pacing`, `CounterValidator`, `AppSettings`, and so on), kept out of the Razor components so they can be unit tested. The components only display state and call the service.

## Setup

### 1. Create the application in Authentik

1. In the Authentik admin interface go to **Applications > Providers > Create** and choose **OAuth2/OpenID Provider**.
2. Add a **strict redirect URI**: `https://pacer.example.com/auth/oidc/callback` (use your own host).
3. Make sure the scopes `openid`, `profile` and `email` are enabled, and select a signing key.
4. Create an **Application** that uses this provider and note its **slug** (for example `pacer`).
5. Copy the **Client ID** and **Client Secret** from the provider.

Your issuer URL is `https://<authentik-host>/application/o/<slug>/` and must be HTTPS.

### 2. Prepare PostgreSQL

Pacer needs a PostgreSQL database it can reach from the swarm. Any PostgreSQL 12 or newer works (an existing server, a managed service, or a container). Create a database and user:

```sql
CREATE USER pacer WITH PASSWORD 'choose-a-strong-password';
CREATE DATABASE pacer OWNER pacer;
```

Pacer creates and updates its own tables on startup using EF Core migrations, and waits for the database if it is not up yet. The database user therefore needs permission to create tables (owning the database, as above, is enough). It uses these tables: `Users`, `Counters` and `DataProtectionKeys`.

### 3. Create the Docker secrets

Each secret holds only the value.

```bash
printf '%s' 'YOUR_CLIENT_ID'     | docker secret create pacer_oidc_client_id -
printf '%s' 'YOUR_CLIENT_SECRET' | docker secret create pacer_oidc_client_secret -
printf '%s' 'THE_DB_PASSWORD'    | docker secret create pacer_db_password -
```

Swarm mounts them in the container at `/run/secrets/<name>`, and the compose file points the `*_FILE` settings at those paths. There is no session secret to manage: cookie signing keys are generated automatically and stored in PostgreSQL.

### 4. Build and push the image

Swarm ignores `build:`, so push the image to a registry your nodes can pull from:

```bash
docker build -t registry.example.com/pacer:1.0.0 .
docker push registry.example.com/pacer:1.0.0
```

### 5. Configure and deploy the stack

Edit `docker-compose.yml`:

- `image`: the image you pushed
- `APP_BASE_URL`: the public URL users visit
- `OIDC_ISSUER`: the issuer URL from step 1
- `APP_TIMEZONE`: your IANA timezone, such as `Europe/London`
- `DB_HOST`, `DB_NAME`, `DB_USER`: how to reach the database from step 2
- The Traefik `Host(...)` rule, and the `certresolver` label if you use one
- The `traefik` and `database` network names, so they match your existing overlay networks

Then deploy:

```bash
docker stack deploy -c docker-compose.yml pacer
```

Visit your URL, sign in through Authentik, and add your first counter.

### Don't have PostgreSQL yet?

If you need a database in the same stack, a minimal service looks like this. Postgres still needs somewhere to keep its files, so pin it to one node with a local volume, or point the volume at storage you trust (for example NFS or a cloud volume driver):

```yaml
  postgres:
    image: postgres:17
    environment:
      POSTGRES_DB: pacer
      POSTGRES_USER: pacer
      POSTGRES_PASSWORD_FILE: /run/secrets/pacer_db_password
    secrets:
      - pacer_db_password
    volumes:
      - pgdata:/var/lib/postgresql/data
    networks:
      - database
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.postgres == true   # docker node update --label-add postgres=true <node>
```

Add `pgdata:` under a top-level `volumes:` key. A managed PostgreSQL service avoids this question entirely.

## Database and migrations

All data access goes through Entity Framework Core. The model is plain C# classes (`Data/Entities.cs`) configured in `PacerDbContext`; `EfCounterStore` uses LINQ queries, and the schema is managed by migrations in `src/Pacer/Data/Migrations`.

- **Migrations run automatically on startup.** When several replicas start together, the Npgsql provider takes a database lock so only one of them applies them.
- **Changing the model**: edit the entities, then add a migration and commit the generated files:

  ```bash
  dotnet tool install --global dotnet-ef        # once
  dotnet ef migrations add AddSomething --project src/Pacer --output-dir Data/Migrations
  ```

  `dotnet ef` uses `DesignTimePacerDbContextFactory`, so it needs none of the app's settings. By default it points at a host that can never resolve, so it can never touch a real database by accident. What that means per command:

  | Command | Connects to a database? |
  | --- | --- |
  | `migrations add`, `migrations script` | No |
  | `migrations remove --force` | No |
  | `migrations remove` (without `--force`) | Yes, to check the migration has not been applied |
  | `database update`, `migrations list` | Yes |

  For the commands that connect, set `PACER_DESIGN_CONNECTION` to a real connection string, for example `export PACER_DESIGN_CONNECTION="Host=localhost;Database=pacer_dev;Username=pacer;Password=dev"`. Use `--force` with `migrations remove` when you know the migration was never applied to the database.
- **If a test or the app reports that the model has pending changes**, the model and the migrations have drifted apart. Add a migration as above. `ModelAndMigrationTests` catches this during `dotnet test`.
- **About the initial migration**: it was written by hand, because `dotnet ef` could not be run when the project was generated. If you would rather have EF generate it, run `dotnet ef migrations remove --force --project src/Pacer` (or delete the `Data/Migrations` folder) and then `dotnet ef migrations add InitialCreate --project src/Pacer --output-dir Data/Migrations`. The tests do not depend on the migration's timestamp.
- Counting uses single `UPDATE` statements generated by `ExecuteUpdateAsync`, so concurrent taps are never lost.
- Each store call uses its own short-lived `DbContext` from a `IDbContextFactory`, which is the recommended pattern for Blazor Server.

## Configuration reference

Every setting can be given directly (`NAME=value`) or as a file (`NAME_FILE=/path`). If both are set, the file wins, and an empty file counts as "not set". Use the `_FILE` form for anything sensitive.

| Setting | Required | Default | Description |
| --- | --- | --- | --- |
| `OIDC_ISSUER` | yes | | Authentik issuer URL, ending in the application slug |
| `OIDC_CLIENT_ID` | yes | | OIDC client ID |
| `OIDC_CLIENT_SECRET` | yes | | OIDC client secret |
| `POSTGRES_HOSTNAME` | yes | | PostgreSQL host |
| `POSTGRES_DB` | yes | | PostgreSQL password |
| `POSTGRES_PORT` | no | `5432` | PostgreSQL port |
| `PGPORT` | no | `5432` | PostgreSQL port |
| `POSTGRES_DB` | no | `pacer` | Database name |
| `POSTGRES_USER` | no | `pacer` | Database user |
| `DB_SSL_MODE` | no | `Prefer` | `Disable`, `Prefer`, `Require`, `VerifyCA` or `VerifyFull` |
| `APP_BASE_URL` | recommended | derived from request | Public URL, used to build the OIDC redirect URI |
| `APP_TIMEZONE` | no | `UTC` | Timezone used to decide what "today" is |
| `OIDC_SCOPES` | no | `openid profile email` | Space-separated scopes to request |
| `COOKIE_SECURE` | no | `true` | Set to `false` only for local HTTP development |

## Traefik and Blazor Server notes

- The app listens on port `8080` inside the container. The stack labels point Traefik's service at that port.
- Blazor Server holds each user's live session in memory on one replica and talks to the browser over a WebSocket. Traefik proxies WebSockets without extra configuration. The example labels turn on a **sticky session cookie** so a reconnecting browser returns to the same replica. If a replica restarts, open pages show Blazor's "reconnecting" banner and then offer a reload; no data is lost.
- In swarm mode the Traefik labels must be under `deploy.labels`, as in the example file. If you use Traefik's dedicated Swarm provider, change `traefik.docker.network` to `traefik.swarm.network`.
- TLS is terminated at Traefik. The container trusts `X-Forwarded-*` headers from any source, so make sure only Traefik can reach it (do not publish port 8080).
- `GET /healthz` is an unauthenticated health endpoint. The image's `HEALTHCHECK` calls it with `dotnet Pacer.dll --healthcheck`.

## Tests

The solution has an xunit project in `tests/Pacer.Tests`.

```bash
dotnet test
```

What is covered:

| Area | Tests |
| --- | --- |
| `Pacing` | weekly and daily rate, due today, overdue, complete, counting past the target, day counting across months and years |
| `CounterValidator` | name, target and date rules, boundaries, create versus edit, multiple errors at once |
| `Fmt` | number rounding, singular and plural days, date and percent formatting |
| `CounterService` | create, default handling, counting, undo, edit, delete, default promotion, and isolation between users, using an in-memory store |
| `EfCounterStore` | every query against an in-memory SQLite database: round-tripping, ordering, counting and clamping, update, default promotion on delete, foreign key enforcement, and isolation between users |
| `EfXmlRepository` | data protection keys are stored, ordered and shared between instances |
| EF model and migrations | migrations match the model (no drift), table names, name length matches the validator, cascade delete, primary key |
| Connection string | settings carried over, special characters in passwords, SSL mode spellings and errors |
| Design-time factory | the default connection can never reach a real database, and `PACER_DESIGN_CONNECTION` overrides it |
| `AppSettings` | required settings, defaults, `_FILE` secrets (precedence, trimming, unreadable and empty files), ports, scopes, timezone |
| `Clock` | timezone conversion either side of midnight, unknown-timezone fallback |
| `IdentityMapper`, `CurrentUser`, `UserExtensions`, `UrlSafety` | claim mapping, name fallbacks, missing `sub`, open-redirect protection |
| `FakeStore` | the in-memory store follows the same rules as the real one |

### PostgreSQL integration tests (optional)

`PostgresIntegrationTests` cover what SQLite cannot: the real migrations, several replicas migrating at once, concurrent increments, case-insensitive ordering, the 80-character column limit, cascade delete and the data protection key store. They are **skipped** unless you point them at a throw-away database:

```bash
docker run -d --name pacer-test-pg -p 5433:5432 \
  -e POSTGRES_DB=pacer_test -e POSTGRES_USER=pacer -e POSTGRES_PASSWORD=dev postgres:17

export PACER_TEST_DB="Host=localhost;Port=5433;Database=pacer_test;Username=pacer;Password=dev"
dotnet test
```

Each test creates its own user, so they do not interfere with each other. Do not point them at a production database.

The Blazor components themselves are not covered by automated UI tests; they contain no business logic beyond calling `CounterService`.

## Backups

All state is in PostgreSQL, so back it up the usual way:

```bash
pg_dump -h <host> -U pacer pacer > pacer-backup.sql
```

Users are keyed by the OIDC `sub` claim, so a user keeps their counters as long as Authentik keeps issuing the same subject for them.

## Local development

You need the .NET 10 SDK and a PostgreSQL database. The OIDC issuer must be an HTTPS URL.

```bash
docker run -d --name pacer-pg -p 5432:5432 \
  -e POSTGRES_DB=pacer -e POSTGRES_USER=pacer -e POSTGRES_PASSWORD=dev postgres:17

export OIDC_ISSUER=https://auth.example.com/application/o/pacer/
export OIDC_CLIENT_ID=...
export OIDC_CLIENT_SECRET=...
export POSTGRES_DB=localhost DB_PASSWORD=dev
export APP_BASE_URL=http://localhost:8080
export COOKIE_SECURE=false
export ASPNETCORE_URLS=http://localhost:8080

dotnet run --project src/Pacer
```

Add `http://localhost:8080/auth/callback` as an additional redirect URI in Authentik while developing.

## Security notes

- Authorization code flow with PKCE, state and nonce, handled by ASP.NET Core
- Every query is scoped to the signed-in user's `sub`; other users' counters are never returned
- Every page requires sign-in by default; only the sign-out, not-found and error pages are public
- Login cookie is `HttpOnly`, `SameSite=Lax` and `Secure` by default
- Sign-out is a simple `GET /logout` that only ends the Pacer session. Authentik's own session may remain active, so signing in again can be instant
- The `returnUrl` for sign-in only accepts local paths
- Data protection keys are stored unencrypted in the `DataProtectionKeys` table, so protect database access accordingly
- Container runs as a non-root user

## Troubleshooting

- **Redirect URI mismatch**: the URI in Authentik must exactly match `APP_BASE_URL` + `/auth/callback`.
- **`..._FILE points to ... which cannot be read`**: check the secret is attached to the service and the path matches.
- **`Database not ready` in the logs**: the app retries for about a minute. If it never connects, check `DB_HOST`, the shared network and the database password.
- **Startup fails with a pending model changes error**: the entities changed without a migration. See [Database and migrations](#database-and-migrations).
- **`password authentication failed` or a connection error from `dotnet ef`**: that command is connecting to a database. Use `--force` with `migrations remove`, or set `PACER_DESIGN_CONNECTION` to the right connection string (see [Database and migrations](#database-and-migrations)).
- **`permission denied` while migrating**: the database user needs to be able to create tables, for example by owning the database.
- **Login loops back to sign-in**: the cookie is `Secure` but the browser is on plain HTTP. Use HTTPS, or set `COOKIE_SECURE=false` for local development only.
- **Page loads but never becomes interactive, or keeps reconnecting**: check that WebSockets reach the container through your proxy chain, and that the sticky cookie label is present when running more than one replica.
- **Wrong "today" or off-by-one days**: set `APP_TIMEZONE`.
- **OIDC discovery errors**: open `<OIDC_ISSUER>.well-known/openid-configuration` from a node and check it responds.

## Credits

Designed and written by **Claude** (Anthropic).
