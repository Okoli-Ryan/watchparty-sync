import { Arrow, MAIN_ACTION, REPO, vars } from './shared';

// The dealt hero: triptych (assets/layout/hero-triptych.html). The headline on span-3, a collage of
// three sets on span-6 that crossfade every 2.8 s (layout.js adds the pause disc), one large button
// on the right line. The photographs are stand-ins for whatever video a room is watching.
export default function Hero() {
  return (
    <section className="hero hero--triptych" data-enter="" aria-labelledby="hero-h">
      <div className="wrap">
        <div className="hero-text grid hero__grid">
          <div className="span-3">
            <h1 id="hero-h">Watch parties for any web video</h1>
            <p className="hero__lead">
              Pick a video on any page and open a room. Everyone who joins plays, pauses and seeks with you, in Chrome,
              Edge or Firefox.
            </p>
          </div>

          <div
            className="ly-collage span-6 start-4"
            role="img"
            aria-label="A surf video picked on a web page with the new room beside it, the list of open rooms over a football match, and a paused nature film with the host's pause sent to every viewer"
          >
            <div className="ly-collage__set is-on">
              <div className="ly-piece pc-video pc-pick" style={vars({ left: '4%', top: '6%', width: '70%', height: '56%', '--at': '22%' })}>
                <img src="/assets/img/photo-f62171462-960.jpg" alt="" style={{ objectPosition: '40% 50%' }} data-source="stand-in" data-subject="a surf film playing on a web page" />
                <div className="bar"><b className="pause" /><i /><span>04:12</span></div>
              </div>
              <div className="ly-piece pc-card" style={{ left: '52%', top: '50%', width: '44%' }}>
                <span className="lbl">Video 1 of 2 on this page</span>
                <span className="ttl">Use this video?</span>
                <div className="ln">
                  <span><kbd>&larr;</kbd> <kbd>&rarr;</kbd> to switch</span>
                  <span><kbd>Enter</kbd> to pick</span>
                </div>
              </div>
              <div className="ly-piece pc-card pc-hide-sm" style={{ left: '4%', top: '68%', width: '40%' }}>
                <span className="lbl">New room</span>
                <span className="ttl">Surf trip edit</span>
                <div className="ln"><span>Starts at</span><em>04:12</em></div>
              </div>
            </div>

            <div className="ly-collage__set" aria-hidden="true">
              <div className="ly-piece pc-video" style={vars({ left: '30%', top: '6%', width: '66%', height: '52%', '--at': '64%' })}>
                <img src="/assets/img/photo-f97504792-960.jpg" alt="" data-source="stand-in" data-subject="a local football match playing on a web page" />
                <div className="bar"><b className="pause" /><i /><span>58:40</span></div>
              </div>
              <div className="ly-piece pc-card" style={{ left: '4%', top: '36%', width: '46%' }}>
                <span className="lbl">Open rooms</span>
                <div className="ln"><span>Match night</span><em>Join</em></div>
                <div className="ln"><span>Surf trip edit</span><em>Join</em></div>
                <div className="ln"><span>Lecture 6 rewatch</span><em>Join</em></div>
              </div>
              <div className="ly-piece pc-card pc-hide-sm" style={{ left: '56%', top: '66%', width: '40%' }}>
                <span className="lbl">Match night</span>
                <span className="live">5 watching, in step</span>
                <div className="dots">
                  <span style={{ background: '#1f6b3a' }}>H</span>
                  <span style={{ background: '#3a3a3a' }}>2</span>
                  <span style={{ background: '#6a6a6a' }}>3</span>
                  <span style={{ background: '#9a9a9a' }}>4</span>
                </div>
              </div>
            </div>

            <div className="ly-collage__set" aria-hidden="true">
              <div className="ly-piece pc-video" style={vars({ left: '4%', top: '6%', width: '64%', height: '50%', '--at': '47%' })}>
                <img src="/assets/img/photo-f117648128-960.jpg" alt="" data-source="stand-in" data-subject="a nature film paused on a web page" />
                <div className="bar"><b /><i /><span>41:08</span></div>
              </div>
              <div className="ly-piece pc-card" style={{ left: '46%', top: '44%', width: '50%' }}>
                <span className="lbl">Host paused</span>
                <span className="ttl">Paused at 41:08 for all 4 viewers</span>
              </div>
              <div className="ly-piece pc-card pc-hide-sm" style={{ left: '4%', top: '64%', width: '38%' }}>
                <span className="lbl">The host left</span>
                <span className="ttl">You hold the remote now</span>
              </div>
            </div>
          </div>

          <div className="span-3 start-10 hero__cta">
            <a className="btn btn--primary" href={REPO}>{MAIN_ACTION}<Arrow /></a>
          </div>
        </div>
      </div>
    </section>
  );
}
