// ---------------------------------------------------------------------------
// Where the WatchParty API lives. Set VITE_API_URL in .env (see .env.example);
// the default is the API's development profile (`dotnet run` in server/).
//
// This value is not a secret: every request is authorised by the bearer token
// the API issues at sign-in, and the server re-checks ownership on every write.
// ---------------------------------------------------------------------------
const configuredUrl: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

/** Base URL without a trailing slash, e.g. `https://api.example.com`. */
export const API_BASE_URL = configuredUrl.replace(/\/+$/, '');

/** REST routes are versioned in the path. */
export const API_ROOT = `${API_BASE_URL}/api/v1`;

/** The SignalR hub that replaces Firestore's live listeners. */
export const REALTIME_HUB_URL = `${API_BASE_URL}/hubs/realtime`;
