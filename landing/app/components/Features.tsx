import { Glyph, vars } from './shared';

// features-2x2 as two screens (.g2): row 1 the h2 and two cards, row 2 the next two.
export default function Features() {
  return (
    <section className="section g2" id="features" aria-labelledby="features-h">
      <div className="wrap">
        <div className="group">
          <div className="section-head">
            <h2 id="features-h">
              What a room handles for you. <span className="muted">Hosts leave, players misbehave, chats stay private.</span>
            </h2>
          </div>
          <div className="grid">
            <article className="card card--row card--open span-6 m-nolift">
              <h3>Hands the room to the next viewer</h3>
              <p>When the host leaves, the next viewer takes the remote. The first host gets it back on return.</p>
              <div className="panel" data-fx="grain-gradient" data-fx-seed="37" data-fx-params='{"drift":0}'>
                <div className="stage stage--fade-b">
                  <div className="at-rise fit" style={vars({ '--fit-max': '380px', '--ax': '32px', '--ay': 'calc(100% - 180px)', '--ay-sm': 'calc(100% - 180px)' })} data-ui-loop="">
                    <div className="ui ui-notify" data-ui="notify">
                      <ul className="ui-notify__list" data-notify-list="">
                        <li className="ui-note">
                          <span className="ui-note__ico">{Glyph.play}</span>
                          <span className="ui-note__text"><span className="ui-note__title">You hold the remote<time>now</time></span><span className="ui-note__body">The host closed their tab</span></span>
                        </li>
                        <li className="ui-note">
                          <span className="ui-note__ico">{Glyph.person}</span>
                          <span className="ui-note__text"><span className="ui-note__title">2 viewers joined<time>3m</time></span><span className="ui-note__body">Lined up at 58:40</span></span>
                        </li>
                        <li className="ui-note">
                          <span className="ui-note__ico">{Glyph.check}</span>
                          <span className="ui-note__text"><span className="ui-note__title">Room is in step<time>5m</time></span><span className="ui-note__body">5 viewers, one position</span></span>
                        </li>
                      </ul>
                    </div>
                  </div>
                </div>
              </div>
            </article>

            <article className="card card--row card--open span-6 m-nolift">
              <h3>Runs in Chrome, Edge and Firefox</h3>
              <p>One Manifest V3 build for Chrome and Edge, one for Firefox, from the same code.</p>
              <div className="panel" data-fx="grain-gradient" data-fx-seed="41" data-fx-params='{"drift":0}'>
                <div className="browsers">
                  <img className="ic ic-40" src="/assets/icons/chrome.svg" alt="Chrome" width={40} height={40} />
                  <img className="ic ic-40" src="/assets/icons/microsoft-edge.svg" alt="Microsoft Edge" width={40} height={40} />
                  <img className="ic ic-40" src="/assets/icons/firefox.svg" alt="Firefox" width={39} height={40} />
                </div>
              </div>
            </article>
          </div>
        </div>

        <div className="grid">
          <article className="card card--row card--open span-6 m-nolift">
            <h3>Locks private rooms with a passphrase</h3>
            <p>The chat key comes from the passphrase, and the passphrase is never stored.</p>
            <div className="panel">
              <img
                className="frame__img"
                src="/assets/img/photo-f117648128-960.jpg"
                srcSet="/assets/img/photo-f117648128-500.jpg 500w, /assets/img/photo-f117648128-960.jpg 960w, /assets/img/photo-f117648128.jpg 1280w"
                sizes="(max-width: 1023px) 100vw, 50vw"
                width={1280}
                height={853}
                alt="A mountain reflected in a still lake, as a nature film in a private room"
                loading="lazy"
                decoding="async"
                data-source="stand-in"
                data-subject="a nature film in a private room"
              />
              <div className="note-over"><b>Friday film club is private</b>Type the passphrase to join</div>
            </div>
          </article>
          <article className="card card--row card--open span-6 m-nolift">
            <h3>Flags video players that block seeking</h3>
            <p>Some players refuse seeking or autoplay. The room says which, and keeps what still works in step.</p>
            <div className="panel">
              <img
                className="frame__img"
                src="/assets/img/photo-f62171462-960.jpg"
                srcSet="/assets/img/photo-f62171462-500.jpg 500w, /assets/img/photo-f62171462-960.jpg 960w"
                sizes="(max-width: 1023px) 100vw, 50vw"
                width={960}
                height={722}
                alt="A surfer inside a breaking wave, as a surf film playing on a page"
                loading="lazy"
                decoding="async"
                data-source="stand-in"
                data-subject="a surf film on a player that blocks seeking"
                style={{ objectPosition: '30% 50%' }}
              />
              <div className="note-over"><b>This player blocks seeking</b>Play and pause still follow the host</div>
            </div>
          </article>
        </div>
      </div>
    </section>
  );
}
