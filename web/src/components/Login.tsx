import { useState } from 'react';
import { login, signUp } from '../../../src/backend/auth';
import { describeError } from '../../../src/backend/http';

export function Login() {
  const [mode, setMode] = useState<'signin' | 'signup'>('signin');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const creating = mode === 'signup';

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (creating) await signUp(email, name, password);
      else await login(email, password);
      // The auth listener in App re-renders; nothing else to do here.
    } catch (err) {
      setError(describeError(err, creating ? 'Sign-up failed.' : 'Sign-in failed.'));
    } finally {
      setBusy(false);
    }
  }

  function switchMode() {
    setMode(creating ? 'signin' : 'signup');
    setError(null);
  }

  return (
    <div className="login-shell">
      <form className="card login" onSubmit={submit}>
        <h1>WatchParty Sync</h1>
        <p className="muted">
          {creating ? 'Create an account to follow your rooms and chat.' : 'Sign in to follow your rooms and chat.'}
        </p>

        {creating && (
          <>
            <label htmlFor="name">Name</label>
            <input
              id="name"
              type="text"
              autoComplete="name"
              maxLength={80}
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              autoFocus
            />
          </>
        )}

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          autoFocus={!creating}
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete={creating ? 'new-password' : 'current-password'}
          minLength={creating ? 8 : undefined}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        {creating && <p className="muted small">At least 8 characters.</p>}

        {error && <div className="alert error">{error}</div>}

        <button type="submit" disabled={busy}>
          {creating ? (busy ? 'Creating account…' : 'Create account') : busy ? 'Signing in…' : 'Sign in'}
        </button>
        <p className="muted small">
          {creating ? 'Already have an account? ' : 'New here? '}
          <a href="#" onClick={(e) => { e.preventDefault(); switchMode(); }}>
            {creating ? 'Sign in' : 'Create an account'}
          </a>
        </p>
      </form>
    </div>
  );
}
