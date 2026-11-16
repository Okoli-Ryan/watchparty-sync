# WatchParty web: the landing page (Next.js static export) and the web dashboard (Vite SPA),
# served by one small nginx image on two ports. Nothing runs Node in production.
#
#   docker build -f deploy/web/Dockerfile \
#     --build-arg VITE_API_URL=https://api.example.com \
#     --build-arg SITE_URL=https://watchparty.example.com \
#     -t watchparty-web .                                   (from the repo root)
#
# The build context is the repo root: the dashboard imports the extension's own src/ (the chat
# crypto must be the same code), and the landing lives in landing/ with its own package.json.

# ---- 1. the dashboard -------------------------------------------------------------
FROM node:22-alpine AS dashboard
WORKDIR /repo
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY tsconfig.json vite.config.ts manifest.config.ts ./
COPY src ./src
COPY web ./web
COPY public ./public
# Baked into the bundle at build time; not a secret (see .env.example).
ARG VITE_API_URL
RUN test -n "$VITE_API_URL" || (echo "set --build-arg VITE_API_URL=https://<your api>" >&2; exit 1)
ENV VITE_API_URL=$VITE_API_URL
RUN npm run web:build

# ---- 2. the landing page ----------------------------------------------------------
FROM node:22-alpine AS landing
WORKDIR /landing
COPY landing/package.json landing/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY landing ./
# The landing's public address, for its share tags and canonical link. Optional.
ARG SITE_URL=""
ENV SITE_URL=$SITE_URL NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# ---- 3. the image that runs ---------------------------------------------------------
# nginx as a non-root user, listening on 8080 (landing) and 8081 (dashboard).
FROM nginxinc/nginx-unprivileged:1.27-alpine
COPY deploy/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=landing /landing/out /usr/share/nginx/landing
COPY --from=dashboard /repo/web/dist /usr/share/nginx/dashboard
EXPOSE 8080 8081
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/ && wget -q -O /dev/null http://127.0.0.1:8081/ || exit 1
