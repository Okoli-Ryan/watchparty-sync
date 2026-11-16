import { MAIN_ACTION, REPO } from './shared';

// nav-centre (sections-library.md): the words on the page axis, the name on the left line, the
// main action on the right. system.js turns it into the phone sheet and runs the Rooms panel.
export default function Nav() {
  return (
    <header className="nav nav--centre">
      <div className="wrap nav__bar">
        <a className="skip-link" href="#top">Skip to content</a>
        <a className="nav__logo" href="#top">WatchParty Sync</a>
        <nav className="nav__links" aria-label="Main">
          <div className="nav__item">
            <a className="nav__link" href="#rooms">
              Rooms{' '}
              <svg className="chev" viewBox="0 0 12 12" aria-hidden="true">
                <path d="M3 4.5l3 3 3-3" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </a>
            <div className="nav__menu">
              <div className="wrap grid">
                <div className="menu-col span-3">
                  <p className="t-label">Rooms</p>
                  <a href="#rooms">Room list<span>Open rooms, one click away</span></a>
                  <a href="#steps">Setup<span>Pick, name, press play</span></a>
                </div>
                <div className="menu-col span-3">
                  <p className="t-label">In a room</p>
                  <a href="#sync">Sync<span>Play, pause and seek together</span></a>
                  <a href="#features">Features<span>Handoff, chat, private rooms</span></a>
                </div>
              </div>
            </div>
          </div>
          <div className="nav__item"><a className="nav__link" href="#steps">Setup</a></div>
          <div className="nav__item"><a className="nav__link" href="#features">Features</a></div>
          <div className="nav__item"><a className="nav__link" href="#faq">Questions</a></div>
        </nav>
        <div className="nav__actions">
          <a className="btn btn--primary" href={REPO}>{MAIN_ACTION}</a>
        </div>
        <button className="nav__toggle" type="button" aria-expanded="false" aria-label="Menu">
          <svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true">
            <path d="M2 5h14M2 13h14" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
          </svg>
        </button>
      </div>
    </header>
  );
}
