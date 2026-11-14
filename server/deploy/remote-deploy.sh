#!/usr/bin/env bash
#
# Runs ON THE VPS. Pulls a prebuilt image and starts it, rolling back to whatever was
# running before if the new one doesn't come up healthy.
#
#   remote-deploy.sh <image:tag> [app-dir]
#
# Called by .github/workflows/deploy.yml. Needs a one-time `docker login ghcr.io` on this
# machine and a filled-in .env in the app dir. The database volume and .env are never touched.

set -euo pipefail

IMAGE_REF="${1:?usage: remote-deploy.sh <image:tag> [app-dir]}"
APP_DIR="${2:-$HOME/watchparty}"
CONTAINER="${WATCHPARTY_CONTAINER:-watchparty-api}"
WAIT_SECONDS="${WATCHPARTY_WAIT_SECONDS:-120}"

say() { printf '\n==> %s\n' "$*"; }
die() { printf '\nERROR: %s\n' "$*" >&2; exit 1; }

command -v docker >/dev/null || die "docker is not installed on this machine"
docker compose version >/dev/null 2>&1 || die "the docker compose plugin is missing"
[ -f "$APP_DIR/docker-compose.yml" ] || die "$APP_DIR/docker-compose.yml is missing"
[ -f "$APP_DIR/.env" ] || die "$APP_DIR/.env is missing. Copy .env.example to .env and fill it in"

cd "$APP_DIR"
compose() { docker compose "$@"; }

# What is running now, so a bad release can be undone. Empty on the very first deploy.
PREVIOUS="$(docker inspect --format '{{.Config.Image}}' "$CONTAINER" 2>/dev/null || true)"
[ -n "$PREVIOUS" ] && say "currently running: $PREVIOUS"

# ---- pull first: if this fails, the running containers are untouched -------------

say "pulling $IMAGE_REF"
export WATCHPARTY_IMAGE="$IMAGE_REF"
compose pull api || die "could not pull $IMAGE_REF. Is this machine logged in to ghcr.io? (docker login ghcr.io)"

# ---- start ---------------------------------------------------------------------

say "starting"
if ! compose up -d --remove-orphans; then
  STATUS="could not start"
else
  say "waiting for $CONTAINER to report healthy (up to ${WAIT_SECONDS}s)"
  STATUS="timed out"
  for _ in $(seq 1 "$WAIT_SECONDS"); do
    HEALTH="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$CONTAINER" 2>/dev/null || echo missing)"
    RUNNING="$(docker inspect --format '{{.State.Running}}' "$CONTAINER" 2>/dev/null || echo false)"
    if [ "$HEALTH" = "healthy" ]; then STATUS="healthy"; break; fi
    if [ "$RUNNING" != "true" ]; then STATUS="container exited"; break; fi
    sleep 1
  done
fi

if [ "$STATUS" = "healthy" ]; then
  say "healthy. Removing old local images (keeping this one and the previous one)"
  docker image ls "${IMAGE_REF%:*}" --format '{{.Repository}}:{{.Tag}}' \
    | grep -vxF -e "$IMAGE_REF" -e "${PREVIOUS:-none}" \
    | xargs -r docker rmi >/dev/null 2>&1 || true
  docker image prune -f >/dev/null
  compose ps
  printf '\nDeployed %s\n' "$IMAGE_REF"
  exit 0
fi

# ---- it didn't come up: say why, then go back ----------------------------------

printf '\nThe new release is not healthy (%s). Last log lines:\n\n' "$STATUS" >&2
compose logs --tail 40 api 2>&1 | sed 's/^/    /' >&2 || true

if [ -n "$PREVIOUS" ] && [ "$PREVIOUS" != "$IMAGE_REF" ]; then
  say "rolling back to $PREVIOUS"
  WATCHPARTY_IMAGE="$PREVIOUS" compose up -d --force-recreate --pull never api || true
  die "rolled back to $PREVIOUS. The new release failed: $STATUS"
fi
die "the deploy failed ($STATUS) and there is no previous release to go back to"
