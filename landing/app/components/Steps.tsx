import { Glyph, vars } from './shared';

// steps (sections-library.md): three real steps, each with its result shown. The first card's
// fragment loops; the others hold still. The card ground is the dealt grain-gradient.
const players: [string, string, string][] = [
  ['Main player', '1:52:10', 'Pick'],
  ['Sidebar clip', '2:31', 'Skip'],
  ['Embedded frame', '12:04', 'Skip'],
];

const settings: [string, string, boolean][] = [
  ['Start at 04:12', 'Where the video is now', true],
  ['Room chat', 'Encrypted before it is stored', true],
  ['Private room', 'Joiners need the passphrase', false],
];

export default function Steps() {
  return (
    <section className="section" id="steps" aria-labelledby="steps-h">
      <div className="wrap">
        <div className="section-head">
          <h2 id="steps-h">Start a room in 3 steps</h2>
        </div>
        <div className="grid">
          <article className="card card--row card--open span-4 m-nolift">
            <span className="step-num">1</span>
            <h3>Pick the video</h3>
            <p>The arrow keys step through every player on the page. Enter picks one.</p>
            <div className="panel" data-fx="grain-gradient" data-fx-seed="11" data-fx-params='{"drift":0}'>
              <div className="stage stage--fade-b">
                <div className="at-rise fit to-edge" style={vars({ '--fit-max': '380px', '--ax': 'max(24px, 50% - 190px)', '--ay': '56px', '--ay-sm': '24px' })} data-ui-loop="">
                  <div className="ui ui-panel ui-search" data-ui="search">
                    <div className="ui-search__field">
                      {Glyph.search}
                      <span className="ui-search__q"><span data-search-q="">videos on this page</span><i className="ui-search__caret" /></span>
                      <kbd className="ui-search__kbd">2 found</kbd>
                    </div>
                    <div className="ui-search__group">Players</div>
                    <ul className="ui-search__list" data-search-list="">
                      {players.map(([name, length, verb], i) => (
                        <li key={name} className={i === 0 ? 'ui-search__row is-sel' : 'ui-search__row'}>
                          <span className="ui-search__ico">{Glyph.play}</span>
                          <span className="ui-search__t"><b>{name}</b>, {length}</span>
                          <span className="ui-search__m">{verb}</span>
                        </li>
                      ))}
                    </ul>
                    <div className="ui-search__foot">
                      <span><kbd>&larr;</kbd><kbd>&rarr;</kbd> to move</span>
                      <span><kbd>Enter</kbd> to pick</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </article>

          <article className="card card--row card--open span-4 m-nolift">
            <span className="step-num">2</span>
            <h3>Name the room</h3>
            <p>The API stores the page, the player and the current second under that name.</p>
            <div className="panel" data-fx="grain-gradient" data-fx-seed="23" data-fx-params='{"drift":0}'>
              <div className="stage stage--fade-b">
                <div className="at-right to-edge" style={vars({ '--ax': '32px', '--ay': '56px' })}>
                  <div className="ui ui-panel ui-toggles" data-ui="toggles">
                    <div className="ui-head">
                      <span className="ui-toggles__ico">{Glyph.play}</span>
                      <div className="ui-title">Surf trip edit</div>
                      <span className="ui-spacer" />
                      <div className="ui-meta" data-tg-count="">2 of 3 on</div>
                    </div>
                    <ul className="ui-toggles__list">
                      {settings.map(([label, sub, on]) => (
                        <li key={label} className="ui-tg">
                          <span className="ui-tg__text"><span className="ui-tg__label">{label}</span><span className="ui-tg__sub">{sub}</span></span>
                          <button className={on ? 'ui-switch is-on' : 'ui-switch'} type="button" role="switch" aria-checked={on} aria-label={label}><i /></button>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              </div>
            </div>
          </article>

          <article className="card card--row card--open span-4 m-nolift">
            <span className="step-num">3</span>
            <h3>Press play together</h3>
            <p>Friends join from the popup. Their tab opens the page and follows your player.</p>
            <div className="panel">
              <img
                className="frame__img"
                src="/assets/img/photo-f195045529-960.jpg"
                srcSet="/assets/img/photo-f195045529-500.jpg 500w, /assets/img/photo-f195045529-960.jpg 960w"
                sizes="(max-width: 1023px) 100vw, 33vw"
                width={960}
                height={640}
                alt="A mountain valley with a lake, as a travel film shared in a room"
                loading="lazy"
                decoding="async"
                data-source="stand-in"
                data-subject="a travel film playing in a room"
              />
              <div className="note-over"><b>3 joined Surf trip edit</b>Playing at 04:12 on every screen</div>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
