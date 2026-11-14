import { useState } from 'react';
import { logout } from '../backend/auth';
import { useDashboardData } from './useDashboardData';
import { RoomsTab } from './tabs/RoomsTab';
import { FavouritesTab } from './tabs/FavouritesTab';
import { HistoryTab } from './tabs/HistoryTab';
import { SettingsTab } from './tabs/SettingsTab';

// Shell for the full-page dashboard: the auth gate, the shared subscriptions
// (via useDashboardData) and the tab bar. Each tab owns its own local state —
// pagination, join progress, preferences — so none of it lands here.

type Tab = 'rooms' | 'favourites' | 'history' | 'settings';

export function Dashboard() {
  const { profile, live, liveIds, history, favourites } = useDashboardData();
  const [tab, setTab] = useState<Tab>('rooms');

  if (profile === undefined) return <div className="dash-empty">Loading…</div>;
  if (profile === null) {
    return (
      <div className="dash-empty">
        You're signed out. Open the extension popup to sign in, then reload this page.
      </div>
    );
  }

  return (
    <div>
      <div className="dash-head">
        <div>
          <h1>WatchParty Sync</h1>
          <div className="sub">
            {profile.displayName} · {profile.role} · {history.length} room
            {history.length === 1 ? '' : 's'} watched
          </div>
        </div>
        <button className="btn-signout" onClick={() => logout()}>
          Sign out
        </button>
      </div>

      <div className="dash-tabs">
        <button className={tab === 'rooms' ? 'active' : ''} onClick={() => setTab('rooms')}>
          Active ({live.length})
        </button>
        <button
          className={tab === 'favourites' ? 'active' : ''}
          onClick={() => setTab('favourites')}
        >
          ★ Favourites ({favourites.length})
        </button>
        <button className={tab === 'history' ? 'active' : ''} onClick={() => setTab('history')}>
          History ({history.length})
        </button>
        <button className={tab === 'settings' ? 'active' : ''} onClick={() => setTab('settings')}>
          ⚙ Settings
        </button>
      </div>

      {tab === 'rooms' && <RoomsTab live={live} />}
      {tab === 'favourites' && (
        <FavouritesTab favourites={favourites} liveIds={liveIds} />
      )}
      {tab === 'history' && (
        <HistoryTab history={history} liveIds={liveIds} />
      )}
      {tab === 'settings' && <SettingsTab />}
    </div>
  );
}
