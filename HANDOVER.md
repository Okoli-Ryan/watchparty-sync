# Handover

For whoever picks this up next. Read `DECISIONS.md` alongside this — it explains
*why* the odd-looking parts are that way, and most of them were paid for with a
bug.

---

## What this is

A Chrome/Edge/Firefox MV3 extension for synchronised video watching. A host
picks a `<video>` on any page; viewers join and their player mirrors the host's
play, pause and seek in realtime through the WatchParty API (SignalR push over
a Postgres-backed ASP.NET Core service). It also has
encrypted room chat, reactions, per-user history and favourites, and a full-page
dashboard.

All seven original acceptance criteria are implemented (access control,
keyboard-driven room creation, persisting the room's URL/selector/position,
room discovery, realtime playback control, ownership handoff on exit, and error
handling for uncontrollable players).

---

## Current state

**This is the ASP.NET port.** Firebase is gone. A backend in `server/` (ASP.NET
Core 10, domain-driven layers, Postgres on Neon through EF Core, SignalR for
realtime) replaces Firestore, Firebase Auth and `firestore.rules`. The extension,
popup, dashboard and web app are unchanged apart from their data layer, now
`src/backend/`, which keeps the old function names over REST and SignalR.

**Verified by automated tests:**

| Suite | Count | What it exercises |
|---|---|---|
| `server/` `dotnet test` | 103 | domain rules, use cases, the full HTTP pipeline against a real Postgres 17 container, layer rules |
| `npm test` | 17 | the client data layer, with the network and SignalR faked |
| `npm run test:e2e` | 7 | the real client modules, real SignalR and real AES-GCM against a running API |

`npm run build`, `npm run build:firefox` and `npm run web:build` all pass with
TypeScript strict mode. The server builds with zero warnings, analyzers on.

**Still never verified in a browser.** No test loads the extension. The
pickers, the video controller, the widget and the MV3 service worker's SignalR
connection have only been type-checked since the port. `WatchParty-Test-Plan.xlsx`
(113 cases, 11 regression checks) has not been run. Test in this order:

1. Sign in from the popup, then confirm the service worker sees the session
   (it reads `chrome.storage.local`, not IndexedDB, now).
2. The SignalR connection from the MV3 service worker, especially after Chrome
   suspends and restarts the worker.
3. Host handoff on tab close, and the original host reclaiming on return.
4. The viewer's stale-playback guard in `videoController.apply()`. It was dead
   code in the Firebase build, because Firestore `Timestamp`s lost `toMillis()`
   when posted across the extension port. With plain epoch numbers it now runs.

**Not yet updated for the port:** `PRIVACY.md` and `STORE_LISTING.md` (both
gitignored) still say data is stored in Firebase. Rewrite them, naming whoever
hosts the API and the Neon project, before submitting to a store.

### Fixed in the pre-publication code review (unverified at runtime)

A static review before the first git commit found eight defects. All were fixed;
none has been exercised in a browser, so re-test these paths first.

| Area | What was wrong |
|---|---|
| Widget panel | The "Watching" count and the host's roster both used `data-f="members"`, so `patch()` overwrote the count with the roster markup and the real roster never updated. The roster is now `data-f="roster"`. |
| Join / room lifecycle | `startSession()` always left the old room first, so pressing "Join" on the room you were already alone in ran `deactivateIfEmpty()` and killed it. Re-entry now skips the leave path. |
| Popup ↔ background | An unexpected rejection in a popup handler left the message channel unanswered, freezing the popup's buttons. `sendResponse` now has a `.catch`. |
| Playback sync | The 150 ms throttle *dropped* the final `seeked` of a scrub. It is now deferred and flushed instead. |
| Picker | Frames that armed an overlay but weren't the one picked kept a full-viewport click-swallowing overlay. `onPickResult` now cancels every frame. |
| Presence | `joinRoom()` wrote the requested role, not `effectiveRole`, so a reclaiming primary owner was stored as `viewer`. |
| Chat | The async `onSnapshot` handler could deliver decrypted batches out of order; results are now sequenced. |
| Cross-browser | `isFirefox` could never be true, because Firefox defines `chrome` as well as `browser`. |

---

## Get it running

1. Start the API: `server/README.md` (Postgres via `docker compose up -d`, a JWT
   signing key in user-secrets, `dotnet run --project src/WatchParty.Api`).
2. There is no seeding step: accounts are created by self-service sign-up
   (`POST /api/v1/auth/register`, or **Create an account** in the popup).
3. `cp .env.example .env`; the default `VITE_API_URL` is the local API.
4. `npm run build`, then load `dist/` unpacked. Firefox: `npm run build:firefox`
   and load `dist-firefox/manifest.json` via `about:debugging`, **and grant host
   permissions manually** — Firefox MV3 makes them opt-in, and without them the
   content script never runs.

Two browser profiles are needed for any multi-user testing.

---

## Where things live

```
src/shared/messages.ts   ← read this first: the contract between contexts
src/background/          ← service worker: all API + realtime + routing
src/content/             ← pure DOM: picker, video controller, floating widget
src/popup/               ← discovery UI (login, room list, create, status)
src/dashboard/           ← full page: history, favourites, settings
src/backend/             ← data layer: REST client, SignalR live queries, session
tests/e2e/               ← client against a running API
server/                  ← the API: domain, use cases, EF Core, endpoints, hub
scripts/build-firefox.mjs← rewrites the built manifest for Gecko
```

On the server, business rules live in `server/src/WatchParty.Domain` (start with
`Rooms/Room.cs` and `Rooms/Services/OwnershipSuccession.cs`). Who may do what is
decided there and in the Application handlers. There is no rules file any more.

The service worker is the hub. If behaviour is wrong, its console
(`chrome://extensions` → the extension → "service worker") is almost always
where the answer is.

---

## Debugging

Set `DEBUG = true` in `src/shared/log.ts` (it ships `false`). Logs are split:

- `[WP:bg]` → service-worker console
- `[WP:owner]` / `[WP:viewer]` / `[WP:content]` → the **page's** console

Everything is timestamped to the millisecond so you can line up a host action
with the viewer's reaction across two browsers. A healthy pause looks like:

```
owner page   [WP:owner]  emit pause t=132.44s playing=false
owner SW     [WP:bg]     write playback pause t=132.44 playing=false
viewer SW    [WP:bg]     room snapshot → APPLY t=132.44 playing=false
viewer page  [WP:viewer] apply remote(...) drift=0.31s → pause
```

Whichever line is missing tells you which hop broke. The README has a table
mapping each failure to its cause.

---

## Traps that have already bitten, in order of how much time they cost

1. **Conditional rendering in the widget's `build()`.** `patch()` silently
   no-ops on missing elements, so anything built conditionally never appears.
   Caused three separate bugs. Always emit the element hidden, or put the
   changing thing in the render signature.
2. **Firestore's first snapshot is from cache.** *(Firebase build; designed out
   in the port.)* For a joining user it contained only their own pending write,
   so the reconciler concluded the host was gone and promoted the joiner. The
   server now decides handoff from the full member list, so no client reasons
   about ownership from a partial view. Don't reintroduce client-side handoff.
3. **Server timestamps vs `Date.now()`.** *(Still applies to the client's
   watcher count and room liveness.)* Mixing them killed rooms after ten
   seconds. Use `serverNow()` from `presence.ts`. **This one recurs** — it came
   back in `roomsRef()`, which used `Math.max(Date.now(), freshestHeartbeat)`
   under a comment claiming to guard skew. It guarded only one direction: a
   local clock running more than `ROOM_STALE_MS` *ahead* of the server made every
   room age out at once, so the active list came up empty while the user was
   sitting in a room. Room liveness is now measured against the freshest
   heartbeat alone. If you write another freshness check, the rule is absolute:
   compare server time to server time, never to the local clock.
4. **Port disconnects during intentional navigation.** Joining navigates the
   tab, which closes the old page's port; that used to read as "left the room"
   and tore down the session. `pendingNav` guards it.
5. **Content-script timers throttle in hidden tabs.** Never drive presence or
   anything time-critical from one.
6. **The dev server.** `npm run dev` serves the popup via a redirect stub and
   flashes "Cannot connect to localhost:5173" endlessly, because a popup is
   destroyed on every focus loss. Use `npm run watch` (build --watch) instead.
7. **Consuming state on *send* rather than on *delivery*.** `broadcastToTab()`
   posts to `session.frameKey`, which is null until a frame attaches — and a
   joining viewer's first room snapshot almost always arrives before its page has
   finished loading. The viewer's APPLY marked `lastAppliedSig` as applied
   regardless, so every later snapshot matched the signature and returned early:
   the viewer played from 0:00 and stayed there until the host next did
   something. `broadcastToTab` now reports whether it delivered, and the
   background re-aligns on `ATTACHED`. If you add another "only send when
   changed" path, check delivery before you consume the change.

---

## Known gaps

- **Sign-out doesn't leave the room.** `logout()` only clears the stored token;
  the background session keeps going, so its heartbeats fail with 401 and the
  server prunes the user after the 10 s grace instead of at once. Small fix:
  send `LEAVE_ROOM` before signing out, and tear down on observing an
  unauthenticated user. Still not actioned.
- **No refresh tokens.** Access tokens last 7 days. A user signed in longer than
  that is signed out mid-session by the first 401.
- **One API instance.** SignalR groups are in-process. Scaling out needs a
  backplane.
- **MV3 worker suspension** can still stall presence if a tab is hidden and idle.
  Fully solving it needs `chrome.alarms` (30s minimum), which would mean
  loosening `STALE_MS` and slowing handoff — a real trade-off, not an oversight.
- **Broad host permissions** are still the biggest obstacle to store approval.
  `<all_urls>` has been narrowed to `*://*/*` (equivalent for http/https, drops
  the file:// and ftp:// claim) and the unused `scripting` permission removed,
  but the breadth itself is load-bearing: the background NAVIGATES a viewer's tab
  to the room's page and then waits for that page's content script to send
  `HELLO`. There is no user gesture there, so `activeTab` cannot substitute.

  Two ways out, neither taken yet:
  1. **Narrow to a site list** in `manifest.config.ts`. Best review outcome, ends
     the "any webpage" premise. Right answer if a deployment only uses a few
     sites.
  2. **Optional host permissions** — ship with none, call
     `chrome.permissions.request()` when the user creates or joins a room, then
     `chrome.scripting.registerContentScripts()` for the granted origin (this is
     what would re-earn the `scripting` permission). Correct long-term, but
     `permissions.request()` needs a user gesture on an extension page and Chrome
     often closes the **popup** when the permission dialog takes focus — so the
     grant flow has to move to the dashboard or an onboarding page first.

  Until then: publish **Unlisted (Chrome) / Hidden (Edge)**. For an
  admin-provisioned tool that is the intended distribution anyway, and it draws
  far less review scrutiny than a public listing asking for every host.
- **Icons are generated placeholders** (a purple play triangle), fine to ship
  but not designed.
- **No browser-level tests.** The server and the client data layer are covered;
  the content scripts, widget and popup are not. `crypto.ts` and `dates.ts` are
  pure and would be cheap to add.
- **No data migration from Firebase.** The port starts with an empty database.
  Rooms, history and accounts in an existing Firestore project are not carried
  over, and users need to sign up again.

---

## Publishing

Two documents cover this, and **both are gitignored** — they carry submission
copy and a personal contact address, so they stay out of the public repo. Ask
the maintainer for them:

- `STORE_LISTING.md` — finished copy: descriptions, category, data disclosures
  and written permission justifications.
- `PRIVACY.md` — the privacy policy. Written and complete, but it still **must
  be hosted at a public URL** (a Gist or GitHub Pages will do) before either
  store will accept a submission.

Screenshots still need capturing. Unlisted (Chrome) / Hidden (Edge) suits an
admin-provisioned tool and draws far less review scrutiny.

---

## Suggested next steps

1. Load the extension in two browser profiles against a local API and run the
   test plan, starting with the four checks under "Current state". Nothing above
   the data layer has run since the port.
2. Fix the sign-out gap; it is small and user-visible.
3. Rewrite `PRIVACY.md` and `STORE_LISTING.md` for the new hosting.
4. Decide how to handle the broad host permission before attempting store
   submission — the options are laid out under "Known gaps".
