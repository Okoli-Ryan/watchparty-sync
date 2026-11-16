# Deploying the landing page and the web dashboard to the VPS

Both sites ship as **one image** (`ghcr.io/<owner>/<repo>-web`): nginx serving the landing page
(the Next.js static export from `landing/`) on port 8080 and the web dashboard (the Vite build of
`web/`) on port 8081. The VPS runs it next to the API, behind the same Caddy, and never builds
anything: it pulls the exact image CI built.

```
push to main ──▶ build image (landing + dashboard) ──▶ GHCR  ghcr.io/<owner>/<repo>-web:<sha>
                                                         │
                           SSH ◀── deploy job ◀──────────┘
                            │
                            └─▶ VPS ~/watchparty-web: docker compose pull ─▶ up -d ─▶ wait healthy ─▶ roll back if not
                                                                                          │
                              deploy job curls LANDING_URL and DASHBOARD_URL ◀────────────┘

Caddy ── watchparty.example.com ──▶ 127.0.0.1:8082 (landing)
      ── app.example.com        ──▶ 127.0.0.1:8083 (dashboard)
      ── api.example.com        ──▶ 127.0.0.1:8081 (API, server/DEPLOY.md)
```

Files: [Dockerfile](Dockerfile), [nginx.conf](nginx.conf), [docker-compose.yml](docker-compose.yml),
[.env.example](.env.example), [../../.github/workflows/deploy-web.yml](../../.github/workflows/deploy-web.yml),
and the shared [../../server/deploy/remote-deploy.sh](../../server/deploy/remote-deploy.sh).

The landing and the dashboard deploy separately from the API: their own compose project
(`~/watchparty-web`), their own container, their own rollback. A web deploy never restarts the API.

---

## Before you start

The API deployment in [server/DEPLOY.md](../../server/DEPLOY.md) is done, so these already exist
and are reused as they are:

- Docker and Caddy on the VPS, and the VPS logged in to `ghcr.io` (its Step 6).
- The secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`, `VPS_KNOWN_HOSTS` and the `production` environment.
- The variable `APP_URL` (the API's public address). The dashboard bakes it into its bundle at
  build time, so the web build stops with an error while it is unset.

## Step 1. Two hostnames (once)

Create DNS `A` records pointing at the VPS, for example:

| Site | Hostname |
| --- | --- |
| Landing page | `watchparty.example.com` |
| Web dashboard | `app.example.com` |

## Step 2. Caddy (once)

Add two blocks to `/etc/caddy/Caddyfile`, next to the API's:

```
watchparty.example.com {
    encode zstd gzip
    reverse_proxy 127.0.0.1:8082
}

app.example.com {
    encode zstd gzip
    reverse_proxy 127.0.0.1:8083
}
```

Then `sudo systemctl reload caddy`. Caddy fetches the certificates itself.

## Step 3. The app folder on the VPS (once)

```bash
mkdir -p ~/watchparty-web && cd ~/watchparty-web
```

Create `~/watchparty-web/.env` from [.env.example](.env.example) (only the two ports; the deploy
script refuses to run without the file):

```
LANDING_PORT=8082
DASHBOARD_PORT=8083
```

## Step 4. Let the dashboard call the API (once)

The browser calls the API from the dashboard's origin, so the API must allow it. In
`~/watchparty/.env` (the API's folder) set:

```
CORS_ORIGIN_1=https://app.example.com
```

and restart the API: `cd ~/watchparty && docker compose up -d`.

## Step 5. GitHub variables (once)

Repo → Settings → Secrets and variables → Actions → **Variables**:

| Name | Value | Used for |
| --- | --- | --- |
| `APP_URL` | `https://api.example.com` (already set for the API) | the dashboard's API address, baked in at build |
| `LANDING_URL` | `https://watchparty.example.com` | the landing's share tags and canonical link; the post-deploy check |
| `DASHBOARD_URL` | `https://app.example.com` | the post-deploy check |

`LANDING_URL` and `DASHBOARD_URL` are optional; without them the checks are skipped with a warning
and the landing's share image falls back to a relative address.

## Step 6. Deploy

Push to `main`. The workflow runs when the landing, the dashboard, the extension code they share,
or these deploy files change; to deploy without a change, use Actions → **deploy-web** → **Run workflow**.

When it is green:

```bash
curl -I https://watchparty.example.com
curl -I https://app.example.com
ssh <user>@<vps-ip> 'docker compose -f ~/watchparty-web/docker-compose.yml ps'
```

If the new release is unhealthy, the script prints its last log lines, puts the previous image
back and fails the job.

The first run creates the package `<repo>-web` on GHCR. It is private like the API's, and the VPS
can already pull it with its `read:packages` login.

---

## Run the image locally

From the repo root, with Docker running:

```bash
docker build -f deploy/web/Dockerfile --build-arg VITE_API_URL=http://localhost:5080 -t watchparty-web:local .
```

```bash
docker run --rm -p 8082:8080 -p 8083:8081 watchparty-web:local
```

The landing is on <http://localhost:8082>, the dashboard on <http://localhost:8083> (it talks to
the API at `VITE_API_URL`; for a local API, add `http://localhost:8083` to its allowed origins).

Day to day you don't need the image: `cd landing && npm run dev` (port 3100) and `npm run web:dev`
(port 5174).

## Day-two operations

On the VPS, in `~/watchparty-web`:

| Task | Command |
| --- | --- |
| Logs | `docker compose logs -f web` |
| Status | `docker compose ps` |
| Roll back by hand | `WATCHPARTY_IMAGE=ghcr.io/<owner>/<repo>-web:<older-sha> docker compose up -d web` |

## Things to know

- **The API address is fixed at build time.** Changing `APP_URL` needs a new web deploy (Run
  workflow), not just a restart.
- **The dashboard is `noindex`** (an `X-Robots-Tag` header, as the old `vercel.json` had); the
  landing is indexable.
- **Vercel.** `vercel.json` still describes the dashboard's old Vercel setup. It is harmless; delete
  it once the VPS serves the dashboard so there are not two live copies.
- **Storage.** `cleanup-images` keeps the newest 10 versions of both packages.
