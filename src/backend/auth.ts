import { apiRequest } from './http';
import { onSessionChange, readSession, writeSession } from './session';
import type { UserProfile } from '../shared/types';

interface LoginResponse {
  accessToken: string;
  expiresAt: number;
  user: UserProfile;
}

async function startSession(response: LoginResponse): Promise<UserProfile> {
  await writeSession({
    accessToken: response.accessToken,
    expiresAt: response.expiresAt,
    profile: response.user,
  });
  return response.user;
}

/** Sign in an existing account. */
export async function login(email: string, password: string): Promise<UserProfile> {
  return startSession(
    await apiRequest<LoginResponse>('POST', '/auth/login', {
      body: { email: email.trim(), password },
      anonymous: true,
    }),
  );
}

/** Create an account and sign straight in. The server always makes an ordinary user. */
export async function signUp(email: string, displayName: string, password: string): Promise<UserProfile> {
  return startSession(
    await apiRequest<LoginResponse>('POST', '/auth/register', {
      body: { email: email.trim(), displayName: displayName.trim(), password },
      anonymous: true,
    }),
  );
}

/** Forget the token everywhere: the popup, the dashboard and the service worker share it. */
export function logout(): Promise<void> {
  return writeSession(null);
}

/** The signed-in user's profile, as the token's owner. Null when signed out. */
export async function getMyProfile(): Promise<UserProfile | null> {
  const session = await readSession();
  return session?.profile ?? null;
}

/**
 * Subscribe to sign-in state; the callback receives the profile, or null.
 * Fires once straight away with the current state, like onAuthStateChanged did.
 */
export function watchAuth(callback: (profile: UserProfile | null) => void): () => void {
  let stopped = false;
  void readSession().then((session) => {
    if (!stopped) callback(session?.profile ?? null);
  });

  const stopListening = onSessionChange((session) => callback(session?.profile ?? null));
  return () => {
    stopped = true;
    stopListening();
  };
}
