// The dealt moment: letters-assemble (assets/moments). The statement's letters fly into line as the
// section rises; the loader in system.js mounts it from data-moment. The words stand as written
// without script, under reduced motion and on a phone.
export default function Sync() {
  return (
    <section className="section" id="sync" data-moment="letters-assemble" aria-labelledby="sync-h">
      <div className="wrap">
        <h2 id="sync-h" className="mo-la__type">Everyone in step</h2>
        <p className="mo-la__sub">
          The host&rsquo;s play, pause and seek reach every viewer over SignalR, and a 3-second heartbeat pulls back
          any player that drifts.
        </p>
        <ol className="grid sync-steps">
          <li className="span-4"><span className="t-label muted">Host</span>Presses pause at 41:08</li>
          <li className="span-4"><span className="t-label muted">API</span>Checks the host holds the remote</li>
          <li className="span-4"><span className="t-label muted">Viewers</span>All 4 players pause at 41:08</li>
        </ol>
      </div>
    </section>
  );
}
