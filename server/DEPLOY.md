# Running and deploying the WatchParty API

Two ways to run it locally, one way to run it in production.

```
push to main ──▶ test (dotnet test) ──▶ build image ──▶ GHCR  ghcr.io/<owner>/<repo>:<commit-sha>
                                                           │
                           SSH ◀── deploy job ◀────────────┘
                            │
                            └─▶ VPS: docker compose pull ─▶ up -d ─▶ wait healthy ─▶ roll back if not
                                                                       │
                                     deploy job curls /health/ready ◀─┘
```

The VPS never builds anything: it pulls the exact image CI tested. Postgres runs in a
container next to the API, on a named volume, and is not reachable from outside.

Files: [.github/workflows/deploy.yml](../.github/workflows/deploy.yml),
[.github/workflows/cleanup-images.yml](../.github/workflows/cleanup-images.yml),
[Dockerfile](Dockerfile), [deploy/docker-compose.yml](deploy/docker-compose.yml),
[deploy/remote-deploy.sh](deploy/remote-deploy.sh), [deploy/.env.example](deploy/.env.example).

---

## Part 1. Run it locally

### Option A: day-to-day development (no image)

Needs the .NET 10 SDK and Docker.

1. Start Postgres (from `server/`):

   ```bash
   cd server
   docker compose up -d
   ```

2. Set the JWT signing key once (32+ characters):

   ```bash
   dotnet user-secrets set "Jwt:SigningKey" "<32+ random characters>" --project src/WatchParty.Api
   ```

3. Run the API:

   ```bash
   dotnet run --project src/WatchParty.Api
   ```

   It listens on `http://localhost:5080` and applies migrations on startup (Development only).

4. Check it: open `http://localhost:5080/health/ready`. It should return `Healthy`.
5. Point the extension at it: in the repo root, `.env` has `VITE_API_URL=http://localhost:5080`,
   then `npm run build` and load `dist/` in Chrome (see the root README).
6. Tests: `dotnet test` (Docker must be running; the integration tests start their own Postgres).

### Option B: run the production image locally

This is what the VPS runs, so use it to check a Dockerfile or compose change before pushing.
It can run alongside Option A: it uses port 8081 and does not publish Postgres.

1. Build the image (from the repo root):

   ```bash
   docker build -t watchparty-api:local server
   ```

2. Make a scratch folder with the compose file and an `.env`:

   ```bash
   mkdir wp-local && cd wp-local
   cp ../server/deploy/docker-compose.yml .
   cp ../server/deploy/.env.example .env
   ```

   Fill in `.env`: any `POSTGRES_PASSWORD`, a `JWT_SIGNING_KEY` of 32+ characters, and
   `CORS_ORIGIN_0=chrome-extension://<your extension id>` (any value works for a smoke test).

3. Start it:

   ```bash
   # bash
   WATCHPARTY_IMAGE=watchparty-api:local docker compose up -d
   ```

   ```powershell
   # PowerShell
   $env:WATCHPARTY_IMAGE = "watchparty-api:local"; docker compose up -d
   ```

4. Check it: `curl http://127.0.0.1:8081/health/ready`, and `docker compose ps` shows `api` as `healthy`.
   Migrations are applied by the API on start, so the tables exist already.
5. Stop it: `docker compose down` (add `-v` to also delete the database).
   Delete `wp-local` afterwards; it holds secrets.

---

## Part 2. Deploy to the VPS

You need: a VPS (Ubuntu/Debian) you can SSH into, a domain or subdomain for the API, and the
repo on GitHub (this repo has no remote yet: create one and `git push -u origin main`).

### Step 1. Prepare the VPS (once)

1. Install Docker with the compose plugin: <https://docs.docker.com/engine/install/>.
2. Make sure your SSH user can run Docker: `sudo usermod -aG docker $USER`, then log out and in.
3. Install Caddy (<https://caddyserver.com/docs/install>) if it is not already there. If this
   VPS already hosts another app behind Caddy, reuse it.

### Step 2. Domain and HTTPS (once)

1. Create a DNS `A` record for your API hostname (for example `api.example.com`) pointing at the VPS.
2. Add a block to `/etc/caddy/Caddyfile`:

   ```
   api.example.com {
       reverse_proxy 127.0.0.1:8081
   }
   ```

   Caddy gets the certificate itself, and WebSockets (SignalR) work with no extra settings.
   `8081` is `WATCHPARTY_PORT`. Then `sudo systemctl reload caddy`.

### Step 3. Create the app folder and its secrets on the VPS (once)

The `.env` lives only on the VPS. Deployments never overwrite it.

```bash
mkdir -p ~/watchparty && cd ~/watchparty
```

Create `~/watchparty/.env` using [deploy/.env.example](deploy/.env.example) as the template:

```bash
POSTGRES_PASSWORD=$(openssl rand -hex 24)
JWT_SIGNING_KEY=$(openssl rand -base64 48)
CORS_ORIGIN_0=chrome-extension://<your extension id>
CORS_ORIGIN_1=https://<your web dashboard>
WATCHPARTY_PORT=8081
```

(Write the values out literally; `.env` files do not run `$(...)`. Generate them first with
`openssl`, then paste them in.) `chmod 600 .env`.

`CORS_ORIGIN_1` is optional. Never change `POSTGRES_PASSWORD` after the first start: the
database was created with it. Changing `JWT_SIGNING_KEY` signs everyone out.

### Step 4. A deploy key for GitHub Actions (once)

On your own machine, with no passphrase (this key is only for CI):

```bash
ssh-keygen -t ed25519 -f watchparty-ci -C "watchparty-ci" -N ""
ssh-copy-id -i watchparty-ci.pub <user>@<vps-ip>
ssh-keyscan -t ed25519 <vps-ip>        # copy this output for VPS_KNOWN_HOSTS
```

Keep `watchparty-ci` until Step 5 is done, then delete it locally.

### Step 5. GitHub secrets and settings (once)

Repo → Settings → Secrets and variables → Actions.

| Kind | Name | Value |
| --- | --- | --- |
| Secret | `VPS_HOST` | the VPS IP or hostname |
| Secret | `VPS_USER` | the SSH user on the VPS |
| Secret | `VPS_SSH_KEY` | the full contents of the private key file `watchparty-ci` |
| Secret | `VPS_KNOWN_HOSTS` | the `ssh-keyscan` output from Step 4 (pins the server's identity) |
| Variable | `APP_URL` | `https://api.example.com` (used for the post-deploy check; if unset, the check is skipped with a warning) |

Also create an environment named `production` (Settings → Environments). Optionally add a
required reviewer there to approve each deploy by hand.

### Step 6. Let the VPS pull the image (once)

The image is private by default, so the VPS needs its own read-only credential, separate from CI's.

1. GitHub → Settings → Developer settings → Personal access tokens → **Tokens (classic)**.
   Scope: only `read:packages`. Name it `watchparty-vps-pull`.
2. On the VPS:

   ```bash
   echo '<the token>' | docker login ghcr.io -u <your-github-username> --password-stdin
   ```

(If you would rather make the package public, do that after the first build in the package's
settings and skip this step.)

### Step 7. Deploy

Push to `main`. The workflow runs when files under `server/` (or the workflow itself) change;
to deploy without a server change, use Actions → **deploy** → **Run workflow**.

Watch the run in the Actions tab. When it is green:

```bash
curl https://api.example.com/health/ready         # Healthy
ssh <user>@<vps-ip> 'docker compose -f ~/watchparty/docker-compose.yml ps'
```

Then set `VITE_API_URL=https://api.example.com` in the extension's `.env`, rebuild, and reload it.
The first person to open the extension creates their account with **Create an account**.

If the new release is unhealthy, the script prints the container's last log lines, puts the
previous image back and fails the job.

---

## Day-two operations

All on the VPS, in `~/watchparty`:

| Task | Command |
| --- | --- |
| Logs | `docker compose logs -f api` |
| Status | `docker compose ps` |
| Roll back by hand | `WATCHPARTY_IMAGE=ghcr.io/<owner>/<repo>:<older-sha> docker compose up -d api` |
| Back up the database | `docker compose exec -T postgres pg_dump -U watchparty watchparty > backup-$(date +%F).sql` |
| Restore | `docker compose exec -T postgres psql -U watchparty watchparty < backup.sql` |

Every commit has its own image tag, so a rollback is one command. A rollback restores code,
not data: the new release may already have applied a migration.

## Things to know

- **Migrations run when the API starts** here (`Database__ApplyMigrationsOnStartup=true` in the
  compose file). That is a deliberate shortcut for one VPS and one instance, and differs from
  the Neon route in [README.md](README.md), where migrations are a separate step. If you later
  run several instances, move migrations into a deploy step.
- **Single instance only.** Realtime fan-out is in memory (see README → Single instance).
- **Forwarded headers are on** (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`). The API port is bound to
  `127.0.0.1`, so only Caddy can set them. Without this, the login rate limit would count every
  user as one address.
- **SSH on port 22 must be reachable from GitHub's runners.** Use key-only logins at minimum.
- **Storage.** Private GHCR packages count against the plan's quota. **cleanup-images** runs
  weekly and keeps the newest 10 versions.
- **No automatic backups.** CI does not back up Postgres; schedule the `pg_dump` above with cron.
- **Using Neon instead of the Postgres container:** delete the `postgres` service from the
  compose file and set `ConnectionStrings__WatchParty` to the pooled Neon string.
