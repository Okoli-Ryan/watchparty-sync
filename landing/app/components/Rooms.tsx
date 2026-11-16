import { Glyph } from './shared';

// app-frame (sections-library.md): the product big. The popup's room list (the kit's `window`)
// beside one open room's chat (`chat`), whose pane carries the section's one loop.
const rooms: [string, string, string, string, string, string][] = [
  // tone, initials, room, now, status class, status, viewers
  ['1', 'MN', 'Match night', 'Playing at 58:40', '', '5'],
  ['3', 'ST', 'Surf trip edit', 'Playing at 04:12', '', '3'],
  ['4', 'L6', 'Lecture 6 rewatch', 'Paused at 1:02:17', '', '7'],
  ['2', 'FF', 'Friday film club', 'Private, passphrase', 'is-you', '4'],
  ['5', 'NS', 'Nature doc Sunday', 'Paused at 41:08', 'is-done', '4'],
  ['3', 'CR', 'Cooking stream', 'Playing at 22:05', '', '2'],
  ['1', 'TR', 'Trailer round', 'Playing at 01:48', '', '6'],
  ['4', 'GD', 'Game day replay', 'Paused at 12:40', '', '3'],
  ['2', 'SC', 'Study club', 'Private, passphrase', 'is-you', '5'],
];
const statusWord = (cls: string) => (cls === 'is-you' ? 'Locked' : cls === 'is-done' ? 'Watching' : 'Live');

export default function Rooms() {
  return (
    <section className="section" id="rooms" aria-labelledby="rooms-h">
      <div className="wrap">
        <div className="grid">
          <h2 id="rooms-h" className="span-5">Every open room, one click from joining</h2>
          <div className="span-6 start-7">
            <p>
              The popup lists the rooms you can open. Pick one and your tab goes to the host&rsquo;s page, finds the video
              and lines it up to the host&rsquo;s second.
            </p>
            <a className="link" href="#steps">See how a room starts</a>
          </div>
        </div>

        <div
          className="ui app fill"
          role="img"
          aria-label="The WatchParty room list beside an open room's chat, where the host's pause shows between messages"
          data-ui-pause-host=""
        >
          <div className="app__pane app__pane--list">
            <div className="ui ui-panel ui-win" data-ui="window" data-ui-opts='{"loop":false}' data-open="Live" data-work="Joining" data-done="Watching">
              <div className="ui-win__bar">
                <span className="ui-dots" aria-hidden="true"><i /><i /><i /></span>
                <span className="ui-win__crumb">WatchParty <i>/</i> <b>Rooms</b></span>
                <span className="ui-spacer" />
                <span className="ui-win__search" aria-hidden="true">
                  {Glyph.search}<span className="ui-win__ph">Search</span><span className="ui-kbd">/</span>
                </span>
              </div>
              <div className="ui-win__body">
                <nav className="ui-win__side" aria-hidden="true">
                  <span className="ui-win__nav is-on">{Glyph.play}Live<em>9</em></span>
                  <span className="ui-win__nav">{Glyph.person}Mine<em>2</em></span>
                  <span className="ui-win__nav">{Glyph.lock}Private<em>3</em></span>
                  <span className="ui-win__nav">{Glyph.clock}History</span>
                </nav>
                <div className="ui-win__main">
                  <div className="ui-win__cols" aria-hidden="true"><span>Room</span><span>Now</span><span>Status</span><span>Viewers</span></div>
                  <ul className="ui-win__rows">
                    {rooms.map(([tone, initials, name, now, cls, viewers]) => (
                      <li key={name} className="ui-win__row" data-row="">
                        <span className="ui-avatar" data-tone={tone}>{initials}</span>
                        <span className="ui-win__who">{name}</span>
                        <span className="ui-win__sub">{now}</span>
                        <span className={`ui-win__st ${cls}`.trim()} data-st="">{statusWord(cls)}</span>
                        <span className="ui-win__age">{viewers}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          </div>

          <div className="app__pane app__pane--open" data-ui-loop="">
            <div className="ui ui-panel ui-chat" data-ui="chat">
              <div className="ui-head">
                <span className="ui-avatar" data-tone="5" aria-hidden="true">NS</span>
                <div className="ui-title">Nature doc Sunday</div>
                <span className="ui-chat__online">4 watching</span>
                <span className="ui-spacer" />
              </div>
              <div className="ui-chat__body">
                <div className="ui-chat__day">Host paused at 41:08 for everyone</div>
                <div className="ui-msg ui-msg--me" data-chat-q="">
                  <span className="ui-msg__text">Back in two minutes, getting tea</span>
                </div>
                <div className="ui-msg ui-msg--bot" data-chat-a="">
                  <span className="ui-avatar ui-msg__av" data-tone="1" aria-hidden="true">H</span>
                  <span className="ui-msg__col">
                    <span className="ui-chat__answer">
                      No rush. I&rsquo;ll hold it at <b>41:08</b> and press play when you&rsquo;re back.
                    </span>
                    <span className="ui-chat__srcs">
                      <span className="ui-chat__src">{Glyph.lock}Encrypted in the browser</span>
                      <span className="ui-chat__src">{Glyph.play}Paused for 4 viewers</span>
                    </span>
                  </span>
                </div>
                <div className="ui-msg ui-msg--me"><span className="ui-msg__text">Ready, go</span></div>
              </div>
              <div className="ui-chat__input">
                <span className="ui-chat__ph">Write to the room</span>
                <span className="ui-chat__send" aria-hidden="true">
                  <svg viewBox="0 0 16 16" width="14" height="14">
                    <path d="M8 13V3.5M3.8 7.5 8 3.3l4.2 4.2" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
