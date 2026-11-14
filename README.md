# WatchParty Sync — browser extension

Turn any webpage's HTML5 `<video>` into a synchronized watch party. A room owner
picks a video by stepping through the page's players with the arrow keys; the page URL, a
robust CSS selector, and playback state are stored by the **WatchParty API**
(ASP.NET Core 10, Postgres on Neon through EF Core); other logged-in users join
and their player mirrors the owner's play / pause / seek in realtime over
SignalR. Ownership hands off automatically when the owner leaves.

This is the ASP.NET port of the original Firebase build. The extension and web
dashboard are the same; only the data layer (`src/backend`) and the server
(`server/`) are new.

## Project documents

- **[server/README.md](server/README.md)** — the API: running it locally, tests, deploying to Neon, changing the schema.
- **[server/ASPNET_GUIDELINES.md](server/ASPNET_GUIDELINES.md)** — the coding standard the server follows.
- **[DECISIONS.md](DECISIONS.md)** — why the architecture is shaped this way, one entry per major decision with the constraint that forced it and what it costs.
- **[HANDOVER.md](HANDOVER.md)** — start here if you are picking this up: current state, where things live, traps that have already bitten, and known gaps.
- **WatchParty-Test-Plan.xlsx** — 113 manual test cases plus 11 regression checks.

## How it maps to the acceptance criteria

| # | Requirement | Where |
|---|---|---|
| 1 | Account access: users sign up with name, e-mail and password, then log in on the widget | `backend/auth.ts`, `popup/views/Login.tsx`; server: `SignUpHandler`, `LoginHandler` |
| 2 | Logged-in user creates a room by choosing a video on the page | `content/picker.ts` (keyboard picker + `@medv/finder` selector), `content/videoScan.ts` (ranking) |
| 3 | On confirmation, the API stores URL + selector + currentTime; user names the room | `popup/views/CreateRoom.tsx`, `backend/rooms.ts`; server: `Room.Create` |
| 4 | A second user sees active rooms and opens one | `popup/views/RoomList.tsx`, background auto-navigates the tab |
| 5 | Owner controls playback; play/pause/seek mirror in realtime | `content/videoController.ts`, `backend/rooms.ts` (`writePlayback` / `watchRoom`); server: `Room.RecordPlayback`, SignalR `room` push |
| 6 | On refresh/exit, ownership hands to the next joiner; empty → inactive | server: `OwnershipSuccession` + `RoomSuccession`, run on every join, heartbeat and leave |
| 7 | Error handling for players that can't be manipulated | `content/videoController.ts` (layered autoplay workaround, DRM/seek errors), `content/picker.ts` (no-video / cross-origin) |

## Browser compatibility

| | Chrome | Edge | Firefox |
|---|---|---|---|
| Manifest | MV3 | MV3 | MV3 |
| Background | service worker | service worker | event page (`background.scripts`) |
| Build | `npm run build` → `dist/` | same `dist/` | `npm run build:firefox` → `dist-firefox/` |
| Host permissions | granted at install | granted at install | **user must opt in** |

The one API difference that matters is the namespace: Firefox's promise-based
API is `browser`, and its `chrome` alias is callback-style — so `await
chrome.tabs.query(...)` there resolves to `undefined` instead of throwing, which
would fail silently. `src/shared/ext.ts` resolves this once
(`globalThis.browser ?? globalThis.chrome`) and every runtime call goes through
it, so no polyfill dependency is needed. Type-only references still use the
`chrome` namespace from `@types/chrome`, which is erased at compile time.

## Architecture (why it's split this way)

The content script runs in the **page's** origin, so it cannot see the popup's
session. All API work therefore lives at the **extension** origin: the bearer
token is kept in `chrome.storage.local` (`src/backend/session.ts`), which the
popup, the dashboard and the background service worker share.

- **Popup** (`src/popup`) — interactive UI. Sign-up and login, room
  list, create/confirm, in-room controls.
- **Background service worker** (`src/background/service-worker.ts`) — owns all
  *realtime* work (the SignalR connection, playback writes, presence heartbeat)
  so it survives the popup closing. Talks to the content script over a
  long-lived port; the 3 s heartbeat over that port also keeps the MV3 worker
  alive while a room is active.
- **Content script** (`src/content`) — pure DOM: the picker overlay and the
  video controller. No network access.
- **Data layer** (`src/backend`) — one module per area (`auth`, `rooms`,
  `presence`, `chat`, `history`, `users`), the same function names the Firebase
  build had. `realtime.ts` turns SignalR pushes into `watchX(callback)`
  subscriptions shaped like Firestore's `onSnapshot`.
- **API** (`server/`) — the source of truth. It validates every write, checks
  that only the host drives playback, and decides host handoff itself.

## Setup

### 1. Run the API

Follow [server/README.md](server/README.md): start Postgres (`docker compose up -d`
in `server/`), set a JWT signing key with `dotnet user-secrets`, and
`dotnet run --project src/WatchParty.Api`. It listens on `http://localhost:5080`.
For production, the same file covers Neon, migrations and environment variables.

### 2. Create an account

There is nothing to seed. Open the extension popup (or the web dashboard) and
choose **Create an account** with your name, e-mail and a password of at least
8 characters. Sign-up signs you straight in. Everyone gets an ordinary user
account (`POST /api/v1/auth/register`).

### 3. Point the clients at the API

Copy `.env.example` to `.env` and set `VITE_API_URL` (the default is the local
API). The URL is not a secret: every request is authorised by the token the API
issues at sign-in.

For a deployed API, add the extension's origin (`chrome-extension://<id>`) and
the web dashboard's origin to the server's `Cors__AllowedOrigins`.

### 4. Build & load

**Chrome / Edge**
```bash
npm install        # Node 18+ required
npm run build
```
Then in Chrome: `chrome://extensions` → enable **Developer mode** →
**Load unpacked** → select the `dist/` folder. Edge is the same at
`edge://extensions`.

**Firefox**

```bash
npm run build:firefox
```

Then: `about:debugging` → **This Firefox** → **Load Temporary Add-on…** →
select `dist-firefox/manifest.json`.

Three things differ on Firefox, and the first one will bite you:

- **Host permissions are opt-in.** Under Firefox MV3, `*://*/*` is *requested*
  but not granted at install. Until you approve it (Add-ons Manager → the
  extension → **Permissions** → allow access to all sites), the content script
  never runs and the picker reports that it cannot reach the page.
- **The background is an event page**, not a service worker — Firefox MV3 has no
  `background.service_worker`. `npm run build:firefox` rewrites that key and
  adds the required `browser_specific_settings.gecko` id; the bundled code is
  otherwise identical.
- **Temporary add-ons vanish on restart.** For a permanent install the package
  has to be signed through addons.mozilla.org.

> Newly-opened pages get the content script automatically. Tabs that were
> already open **before** you loaded/reloaded the extension need a page refresh
> before "Create a room" can reach them.

### Packaging it to send to someone

```bash
npm run zip           # builds, then release/watchparty-sync-<version>-chrome.zip
npm run zip:firefox   # same for the Gecko build
```

The manifest sits at the root of the archive, so the recipient extracts it and
points **Load unpacked** at the extracted folder — and it is also the layout both
stores expect for an upload. The archives are gitignored.

> **Whoever receives the zip needs nothing but a browser.** It contains only
> built JS/CSS/HTML — no Node, no npm, no toolchain. Node is required to *make*
> the zip, not to run the extension.

For iterative development use `npm run watch` (rebuilds `dist/` on save; click the
extension's reload icon to pick up changes).

> Avoid `npm run dev`. The CRXJS dev server serves the popup through a redirect
> stub, and because a popup is destroyed every time it loses focus the HMR
> client reconnects endlessly and flashes "Cannot connect to localhost:5173".

## Tests

```bash
npm test            # client data layer (src/backend), network and SignalR faked
npm run build       # type-checks everything, then builds dist/
```

```bash
cd server && dotnet test   # 103 server tests; integration tests need Docker
```

The end-to-end suite runs the real client modules, real SignalR and real
AES-GCM against a **running** API. Start the API, sign up two accounts, then:

```bash
E2E_HOST_EMAIL=host@example.com E2E_VIEWER_EMAIL=viewer@example.com E2E_PASSWORD=... npm run test:e2e
```

It covers sign-in, room creation, a playback push, an encrypted chat round trip,
the members list, history, the host handoff on leave, and private-room key
handling. It does **not** drive a browser: the extension itself is covered by the
manual walkthrough below and `WatchParty-Test-Plan.xlsx`.

## Manual walkthrough

1. Sign up two accounts, `user1` and `user2`, from the popup's **Create an account** link.
2. In one Chrome profile, sign in as **user1**. Open a page with a plain HTML5
   video (e.g. any `.mp4` or an `<video>` test page). **Start the video playing
   first** — many players don't create the `<video>` element until you hit play,
   and a playing one ranks above a stopped one. Then click **Create a room**: a
   banner appears at the top of the page with the first video already
   highlighted. Press **↑ ↓** (or **← →**) to step through the page's videos —
   each is scrolled into view as you land on it — **Enter** to choose one,
   **Esc** to back out. Name the room, **Create & host**. Confirm it appears in
   `GET /api/v1/rooms`.
3. In a second profile (or another machine), sign in as **user2**. The room
   shows under **Active rooms** → **Join** → the tab navigates to the URL and a
   "Click to start watching together" button appears; click it.
4. As user1, play / pause / seek → user2's player follows within ~1 s. The
   room's `playback` in `GET /api/v1/rooms/{id}` changes with it.
5. As user1, refresh or close the tab → ownership moves to user2 at once (their
   badge flips to **owner** and their controls now drive playback). Only a hard
   crash waits out the 10 s heartbeat grace. Close everyone → the room flips
   `isActive: false` and drops off the list.
6. **Error paths:** try a DRM/iframe player (YouTube, Netflix) → you get a clear
   "can't be controlled" message instead of a silent failure; press **Create a
   room** on a page with no video → "No playable video found on this page."
   rather than a picker that hangs; open the picker and press **Esc** with focus
   inside an embedded player's iframe → the banner and the highlight clear in
   *every* frame, not just the one that had focus.

## Ownership model

Two separate fields, because "who is host" has two meanings:

| Field | Meaning |
|---|---|
| `ownerUid` | Who drives playback **right now**. Moves automatically when the host leaves. |
| `primaryOwnerUid` | Who holds the **persistent claim**. Set to the creator; only a deliberate transfer moves it. |

That split gives the behaviour you'd expect:

- **Automatic handoff** (leave, refresh, crash) moves `ownerUid` only. The
  server decides it (`OwnershipSuccession`) from the complete member list and its
  own clock, on every heartbeat and leave. If the original host returns, joining
  hands the room straight back (`Room.Reclaim`), and a present primary owner
  outranks join order when promoting.
- **Manual handoff** — the host picks a name from the **Hand host to…** roster
  on the floating widget (`membersHtml` in `src/content/widget.ts`; the popup is
  for discovery only — DECISIONS.md #17). This moves *both* fields, so the claim
  genuinely changes hands and the previous host will not take it back on their
  next visit.

The roster lists the room's other present members; nothing in the UI currently
distinguishes the one holding `primaryOwnerUid`.

## Dashboard, history and favourites

History, favourites and settings all live on the dashboard rather than the
popup, which has no room for them: the popup lists active rooms and offers
**⧉ Open dashboard — history & favourites**, which opens a full-page dashboard
in a new tab (declared as `options_ui` with `open_in_tab`, so it's also
reachable from the extension's Options entry).

The dashboard has four tabs — **Active**, **★ Favourites**, **History**
(paginated, 20 per page) and **⚙ Settings** (currently just the chat sound) —
and shows two dates per room: when the room was created, and when *you* last
watched it.

History is per user (`/api/v1/me/history`), recorded by the server whenever you
join a room. It belongs to the user rather than the room because "when did I
last watch this" and "is this one of my favourites" are personal, and because
memberships are pruned when someone leaves. Starring a room keeps it in
Favourites so you can come back to a show you're following. Every history route
is under `/me`, so there is no way to address anyone else's.

## Encrypted room chat

Each room has a live chat in the floating widget. Messages are **AES-GCM
encrypted client-side before they reach the API**, so the server stores only
`{ iv, ct, senderUid, senderName, createdAt }`: the body is ciphertext. Reading
the database or its backups shows nothing readable. The server fills in the
sender from the token, so nobody can post as someone else.

Encryption runs in the **background service worker**, not the page: the content
script never sees the room key, only plaintext for rendering. Messages are
immutable once sent; the API has no route to edit or delete one.

### Public vs private rooms

Rooms are created as one or the other, and it decides where the key lives:

| | **Public** | **Private** |
|---|---|---|
| Joining | One click, any signed-in user | Passphrase required |
| Key | Random, **stored** on the room doc | **Derived** from the passphrase (PBKDF2, 250k iters) |
| Stored by the API | `chatKey` | `chatSalt` + `chatCheck` verifier — never the key |
| Database shows | Ciphertext | Ciphertext |
| Can the operator decrypt? | Yes, by taking the key | **No** |

Both store ciphertext, so chat is never readable by browsing the database. The
difference is whether a determined person *with database access* could decrypt:
for a public room they could lift `chatKey` and do it offline; for a private
room the key exists only in the heads of people who know the passphrase.

The passphrase is validated against `chatCheck` — a fixed sentinel encrypted at
creation. A wrong passphrase fails AES-GCM's auth tag, so it's rejected without
the passphrase ever being stored or transmitted anywhere but the client. Once
accepted it's cached in `chrome.storage.session` so a rejoin doesn't re-prompt;
it's gone when the browser closes.

**If a private room's passphrase is lost, its chat history is unrecoverable** —
by you, by admins, by anyone. That's the point, but it is worth knowing.

## Room lifecycle

A room's liveness is **derived**, not just a flag — otherwise a crashed or
closed browser would leave a room "active" forever.

- While attached, the **owner** stamps `rooms/{id}.lastActiveAt` every
  `ROOM_TOUCH_MS` (9 s).
- A room counts as live when `isActive !== false` **and** its heartbeat is
  newer than `ROOM_STALE_MS` (30 s). See `isRoomLive` in `src/backend/rooms.ts`.
- When the last member leaves, or the last remaining members are pruned for
  silence, the server sets `isActive: false`, so the room drops out at once
  rather than after the 30 s window.

The popup lists live rooms under **Active rooms** and nothing else; ended rooms
are reachable only through the dashboard's **History** tab, which lists the ones
*you* attended. Because liveness is computed from a timestamp, rooms created
before this behaviour existed (no `lastActiveAt`) fall back to `createdAt` and
count as ended automatically.

## The on-page widget

Once you're in a room, a floating pill appears bottom-right of the **top-level
page**, floating over everything — even when the video itself lives in a nested
player iframe. The collapsed pill carries a quick-reaction button; click the
pill to expand: room name, your role, host, live viewer count, play state,
chat, **⧉ Dashboard** and **Leave room**, plus **⟲ Sync with host** for a viewer
or **⇄ Change video** and the **Hand host to…** roster for the host. Player
errors raised down in the video frame are relayed up and shown here too.

It renders in a shadow root, so the host page's CSS can't restyle it and its own
styles can't leak out. The split is deliberate: the **video controller** runs in
whichever frame holds the `<video>`, while the **widget** always runs in the top
frame — the background routes `ROOM_INFO` / `WIDGET_OFF` to the latter and
`ATTACH` / `APPLY` to the former.

## Debugging playback sync

Diagnostic logging ships **off** (`DEBUG` in `src/shared/log.ts` — flip it to
`true` while you work; genuine errors log either way). Logs are timestamped
`HH:MM:SS.mmm` so you can line up the owner's action with the viewer's reaction
across two browsers and read the true latency.

Two consoles, two sources:

| Prefix | Where to look |
|---|---|
| `[WP:owner]`, `[WP:viewer]`, `[WP:content]` | the **page's** devtools console (F12 on the video tab) |
| `[WP:bg]` | the **service-worker** console — `chrome://extensions` → the extension → **service worker** |

A healthy pause propagation looks like this:

```
owner page      [WP:owner]  emit pause t=132.44s playing=false
owner SW        [WP:bg]     write playback pause t=132.44 playing=false
viewer SW       [WP:bg]     room snapshot → APPLY t=132.44 playing=false
viewer page     [WP:viewer] apply remote(t=132.44 playing=false) drift=0.31s → pause
```

Reading failures:

- **No `[WP:owner] emit`** → the owner isn't attached, or the role is `viewer`.
- **`emit` but no `[WP:bg] write playback`** → the port or session is broken;
  look for `port closed` / `attachTab skipped`.
- **`writePlayback FAILED`** → the API rejected the write: usually a 403 because
  this client is no longer the host. The error carries the server's message.
- **`⚠ NO CONTENT PORT`** → the background has no content script for that tab;
  the page likely needs a refresh after loading the extension.
- **`apply ignored`** → arrived while the viewer had no video/role.
- **`play() blocked by autoplay policy`** → expected before the join gate is
  clicked; the muted fallback should follow.

## Known trade-offs

- **No refresh tokens yet.** An access token lasts 7 days
  (`Jwt:AccessTokenLifetime`), long enough to outlast any watch party. Sensitive
  actions re-check the account rather than trusting the token alone, so a deleted
  account's token cannot be used for them, but ordinary calls trust the token until it
  expires.
- **Presence uses a heartbeat.** Clean exits (**Leave**, tab close, refresh)
  hand off ownership at once; only a hard crash falls back to the 10 s staleness
  window (`Membership.StaleAfter`). The server measures staleness on its own clock, and
  every members push carries `serverTime` so clients age the watcher count the
  same way (DECISIONS.md #6).
- **Pruning needs a heartbeat to trigger it.** The server reconciles a room when
  someone joins, heartbeats or leaves. A room orphaned by a crash, with nobody
  left to heartbeat, stays `isActive` in the database, though clients still show
  it as ended once its `lastActiveAt` is 30 s old.
- **One API instance.** SignalR groups live in that process's memory. Running
  several instances needs a backplane (Redis or Azure SignalR Service).
