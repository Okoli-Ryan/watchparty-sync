import { useState } from 'react';
import { createUser } from '../../backend/users';
import { describeError } from '../../backend/http';
import type { Role } from '../../shared/types';

export function AdminUsers() {
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<Role>('user');
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setOk(null);
    setBusy(true);
    try {
      const created = await createUser({ email, password, displayName, role });
      setOk(`Created ${created.displayName} (${created.role}).`);
      setEmail('');
      setDisplayName('');
      setPassword('');
      setRole('user');
    } catch (err) {
      setError(describeError(err, 'Could not create the user.'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h2>Create a user</h2>
      <form onSubmit={submit}>
        <label htmlFor="u-name">Display name</label>
        <input id="u-name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
        <label htmlFor="u-email">Email</label>
        <input id="u-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        <label htmlFor="u-pass">Temporary password</label>
        <input id="u-pass" type="text" value={password} onChange={(e) => setPassword(e.target.value)} minLength={8} required />
        <label htmlFor="u-role">Role</label>
        <select id="u-role" value={role} onChange={(e) => setRole(e.target.value as Role)}>
          <option value="user">User</option>
          <option value="admin">Admin</option>
        </select>
        {error && <div className="alert error">{error}</div>}
        {ok && <div className="alert ok">{ok}</div>}
        <button type="submit" disabled={busy}>
          {busy ? 'Creating…' : 'Create user'}
        </button>
      </form>
    </div>
  );
}

