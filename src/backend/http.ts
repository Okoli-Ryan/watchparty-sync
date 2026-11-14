import { API_ROOT } from './config';
import { readSession, writeSession } from './session';

/**
 * A failed API call. `message` is the server's own explanation (the Problem
 * Details `detail`), which is written to be shown to a person as-is.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    /** Stable machine code from the server, e.g. `Room.NotOwner`. */
    readonly code: string | null,
    /** Per-field validation messages, when the server sent them. */
    readonly fields: Record<string, string[]> | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * A message fit to show the user for any failure. The server's own wording is
 * used as-is; only rate limiting needs translating, since a 429 carries no body.
 */
export function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.status === 429) {
    return 'Too many attempts. Try again in a minute.';
  }

  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface RequestOptions {
  body?: unknown;
  /** Sign-in itself is the one call made without a token. */
  anonymous?: boolean;
  /** Treat 404 as "nothing there" and resolve to null instead of throwing. */
  allowNotFound?: boolean;
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Not JSON (a proxy error page, say): fall back to the status below.
  }

  const firstFieldMessage = problem.errors ? Object.values(problem.errors).flat()[0] : undefined;
  const message = firstFieldMessage ?? problem.detail ?? problem.title ?? `Request failed (${response.status}).`;
  return new ApiError(message, response.status, problem.code ?? null, problem.errors ?? null);
}

/**
 * One call to the WatchParty API. Adds the bearer token, sends and reads JSON,
 * and turns any failure into an {@link ApiError}.
 *
 * A 401 means the token is no longer accepted (expired, or the account was
 * removed), so the session is cleared and every context signs out together.
 */
export async function apiRequest<T>(method: Method, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };

  if (!options.anonymous) {
    const session = await readSession();
    if (!session) throw new ApiError('Not signed in.', 401, 'Auth.SignedOut');
    headers.Authorization = `Bearer ${session.accessToken}`;
  }

  if (options.body !== undefined) headers['Content-Type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(`${API_ROOT}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError('Network error. Check your connection.', 0, 'Network.Unreachable');
  }

  if (options.allowNotFound && response.status === 404) return null as T;

  if (!response.ok) {
    if (response.status === 401 && !options.anonymous) await writeSession(null);
    throw await toApiError(response);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}
