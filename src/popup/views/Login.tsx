import { useState } from 'react';
import { login } from '../../backend/auth';
import { describeError } from '../../backend/http';

export function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login(email, password);
      // App re-renders via the auth listener; nothing else to do here.
    } catch (err) {
      setError(describeError(err, 'Sign-in failed.'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <div className="header">
        <div>
          <h1>WatchParty Sync</h1>
          <div className="sub">Sign in to join or host a room</div>
        </div>
      </div>
      <form onSubmit={submit}>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        {error && <div className="alert error">{error}</div>}
        <button type="submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="muted spacer" style={{ marginTop: 12 }}>
        Accounts are created by an admin. Ask your admin if you don’t have one.
      </p>
    </div>
  );
}

