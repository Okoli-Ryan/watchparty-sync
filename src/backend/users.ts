import { apiRequest } from './http';
import type { Role, UserProfile } from '../shared/types';

export interface CreateUserInput {
  email: string;
  password: string;
  displayName: string;
  role: Role;
}

/**
 * Admin-only: create a new account.
 *
 * The Firebase build had to spin up a throwaway second app so that creating an
 * account didn't sign the admin out. The API creates accounts server-side, so the
 * admin's own session is never involved, and it re-checks that the caller is
 * still an admin rather than trusting the token alone.
 */
export function createUser(input: CreateUserInput): Promise<UserProfile> {
  return apiRequest<UserProfile>('POST', '/users', {
    body: {
      email: input.email.trim(),
      password: input.password,
      displayName: input.displayName.trim(),
      role: input.role,
    },
  });
}
