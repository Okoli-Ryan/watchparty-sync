import { useState } from 'react';
import { login, signUp } from '../../backend/auth';
import { describeError } from '../../backend/http';

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
      // App re-renders via the auth listener; nothing else to do here.
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
    <div>
      <div className="header">
        <div>
          <h1>WatchParty Sync</h1>
          <div className="sub">{creating ? 'Create an account' : 'Sign in to join or host a room'}</div>
        </div>
      </div>
      <form onSubmit={submit}>
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
        {creating && <div className="muted">At least 8 characters.</div>}
        {error && <div className="alert error">{error}</div>}
        <button type="submit" disabled={busy}>
          {creating ? (busy ? 'Creating account…' : 'Create account') : busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="muted spacer" style={{ marginTop: 12 }}>
        {creating ? 'Already have an account? ' : 'New here? '}
        <a href="#" onClick={(e) => { e.preventDefault(); switchMode(); }}>
          {creating ? 'Sign in' : 'Create an account'}
        </a>
      </p>
    </div>
  );
}
