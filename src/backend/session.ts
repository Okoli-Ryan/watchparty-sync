import type { UserProfile } from '../shared/types';

// ---------------------------------------------------------------------------
// The signed-in session: the API's bearer token plus the profile it belongs to.
//
// In the extension it lives in `chrome.storage.local`, which the popup, the
// dashboard and the background service worker all share. That is what the
// Firebase build got from `indexedDBLocalPersistence`: the popup signs in, and
// the service worker uses that same session for the realtime work. In the web
// dashboard (an ordinary page with no extension APIs) it lives in localStorage.
// ---------------------------------------------------------------------------

export interface StoredSession {
  accessToken: string;
  /** Epoch ms. A session past this is treated as signed out. */
  expiresAt: number;
  profile: UserProfile;
}

const STORAGE_KEY = 'watchparty:auth';

type SessionListener = (session: StoredSession | null) => void;

interface ExtensionStorageArea {
  get(key: string): Promise<Record<string, unknown>>;
  set(items: Record<string, unknown>): Promise<void>;
  remove(key: string): Promise<void>;
}

interface ExtensionStorage {
  local: ExtensionStorageArea;
  onChanged: {
    addListener(listener: (changes: Record<string, { newValue?: unknown }>, areaName: string) => void): void;
  };
}

/** The extension's storage, or null when running as an ordinary web page. */
function extensionStorage(): ExtensionStorage | null {
  const globals = globalThis as unknown as {
    browser?: { storage?: ExtensionStorage };
    chrome?: { storage?: ExtensionStorage };
  };
  return globals.browser?.storage ?? globals.chrome?.storage ?? null;
}

function isUsable(session: StoredSession | null | undefined): session is StoredSession {
  return !!session && typeof session.accessToken === 'string' && session.expiresAt > Date.now();
}

let cached: StoredSession | null | undefined;
const listeners = new Set<SessionListener>();

function notify(session: StoredSession | null) {
  cached = session;
  for (const listener of listeners) listener(session);
}

/** The current session, or null when signed out or expired. */
export async function readSession(): Promise<StoredSession | null> {
  if (cached !== undefined) {
    return isUsable(cached) ? cached : null;
  }

  const storage = extensionStorage();
  let stored: StoredSession | null = null;
  if (storage) {
    const items = await storage.local.get(STORAGE_KEY);
    stored = (items[STORAGE_KEY] as StoredSession | undefined) ?? null;
  } else {
    const raw = globalThis.localStorage?.getItem(STORAGE_KEY);
    stored = raw ? (JSON.parse(raw) as StoredSession) : null;
  }

  cached = stored;
  return isUsable(stored) ? stored : null;
}

export async function writeSession(session: StoredSession | null): Promise<void> {
  const storage = extensionStorage();
  if (storage) {
    if (session) {
      await storage.local.set({ [STORAGE_KEY]: session });
    } else {
      await storage.local.remove(STORAGE_KEY);
    }
  } else if (session) {
    globalThis.localStorage?.setItem(STORAGE_KEY, JSON.stringify(session));
  } else {
    globalThis.localStorage?.removeItem(STORAGE_KEY);
  }

  notify(session);
}

/**
 * Called whenever the session changes, in THIS context or another one: signing in
 * from the popup reaches the service worker through the storage change event.
 */
export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function listenAcrossContexts() {
  const storage = extensionStorage();
  if (storage) {
    storage.onChanged.addListener((changes, areaName) => {
      if (areaName !== 'local' || !(STORAGE_KEY in changes)) return;
      const next = (changes[STORAGE_KEY].newValue as StoredSession | undefined) ?? null;
      notify(next);
    });
    return;
  }

  globalThis.addEventListener?.('storage', (event: StorageEvent) => {
    if (event.key !== STORAGE_KEY) return;
    notify(event.newValue ? (JSON.parse(event.newValue) as StoredSession) : null);
  });
}

listenAcrossContexts();
