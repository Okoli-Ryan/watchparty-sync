import { Arrow, MAIN_ACTION, REPO } from './shared';

// footer.closing: row 1 the dealt close (close-word), row 2 the dealt footer (footer-columns),
// in one last screen. Columns follow the nav's words.
const columns: [string, [string, string][]][] = [
  ['Rooms', [['Room list', '#rooms'], ['Sync', '#sync']]],
  ['Setup', [['Three steps', '#steps'], ['Build guide', `${REPO}#setup`]]],
  ['Features', [['Host handoff', '#features'], ['Source code', REPO]]],
  ['Questions', [['Answers', '#faq'], ['Issues', `${REPO}/issues`]]],
];

export default function Closing() {
  return (
    <footer className="footer closing" id="contact" aria-labelledby="close-h">
      <div className="wrap">
        <div className="closing__cta close--word">
          <div className="section-head--split">
            <div>
              <h2 id="close-h">Host your first watch party</h2>
              <p className="t-lead">Build it from source, load it in your browser and open a room.</p>
            </div>
            <a className="btn btn--primary" href={REPO}>{MAIN_ACTION}<Arrow /></a>
          </div>
          <p className="close__word" aria-hidden="true">WatchParty Sync</p>
        </div>

        <div className="closing__links">
          <div className="grid">
            <div className="span-4 stack-4">
              <a className="nav__logo" href="#top">WatchParty Sync</a>
              <p className="footer__lede">Synced video rooms for private groups.</p>
            </div>
            {columns.map(([head, links]) => (
              <div key={head} className="span-2 footer__col">
                <h3>{head}</h3>
                <ul>
                  {links.map(([label, href]) => (
                    <li key={label}><a href={href}>{label}</a></li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
          <div className="footer__legal">
            <span>&copy; 2026 WatchParty Sync</span>
            <a href={`${REPO}/blob/main/LICENSE`}>MIT licence</a>
            <div className="socials" />
            <a className="credits" href="/credits.html">Photographs via Wikimedia Commons</a>
          </div>
        </div>
      </div>
    </footer>
  );
}

// The phone's action bar (system.md, Nav): the main action at the foot once the hero's button has
// scrolled away; with it the nav's button moves into the menu sheet. system.js shows and hides it.
export function ActionBar() {
  return (
    <nav className="actionbar" aria-label="Quick actions">
      <a className="btn btn--primary" href={REPO}>{MAIN_ACTION}</a>
    </nav>
  );
}
