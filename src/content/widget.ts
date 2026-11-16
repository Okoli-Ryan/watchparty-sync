import type { RoomInfo } from '../shared/messages';
import type { ChatMessage } from '../shared/types';
import { CHAT_MAX_LEN } from '../shared/constants';
import { beep } from '../shared/beep';
import { T, loadWidgetFont } from './theme';

// Floating on-page widget: a small pill that expands into a status panel.
// Rendered inside a shadow root so the host page's CSS can't reach it — these
// streaming pages are aggressive with global styles and high z-indexes.

const HOST_ID = 'watchparty-widget-host';
const Z = 2147483600; // above the picker overlay and the page's own chrome
/** Keep at least this much of the widget's box inside the viewport (px). */
const MARGIN = 8;

/** Constrain a top-left position so a w×h box stays fully on screen. */
function clampToViewport(x: number, y: number, w: number, h: number): WidgetPos {
  const maxX = Math.max(MARGIN, window.innerWidth - w - MARGIN);
  const maxY = Math.max(MARGIN, window.innerHeight - h - MARGIN);
  return { x: Math.min(Math.max(MARGIN, x), maxX), y: Math.min(Math.max(MARGIN, y), maxY) };
}

/** Persisted widget position, in px from the viewport's top-left. */
export interface WidgetPos {
  x: number;
  y: number;
}

export interface WidgetCallbacks {
  onLeave: () => void;
  onSend: (text: string) => void;
  onOpenDashboard: () => void;
  onResync: () => void;
  onRetry: () => void;
  onSeek: (seconds: number) => void;
  onTransferHost: (uid: string) => void;
  onReselectVideo: () => void;
  onMove: (pos: WidgetPos) => void;
  /** The transcript is on screen up to this message timestamp. */
  onChatRead: (at: number) => void;
}

export class Widget {
  private host: HTMLDivElement | null = null;
  private root: ShadowRoot | null = null;
  private expanded = false;
  private info: RoomInfo | null = null;
  private error: string | null = null;
  private chatOpen = false;
  private messages: ChatMessage[] = [];
  /** null until the first batch lands, so the backfill doesn't beep. */
  private seenMessageIds: Set<string> | null = null;
  private beepEnabled = true;
  private reactionOpen = false;
  /**
   * Messages from others the user has not read yet.
   *
   * Owned by the background and pushed in on ROOM_INFO, not tallied here: a
   * counter local to this content script could only ever be incremented by it,
   * so reading the same conversation in the web dashboard had no way to settle
   * it. It is derived from a read position shared across all of the user's
   * clients.
   */
  private get unread(): number {
    return this.info?.unread ?? 0;
  }
  private resyncTimer: ReturnType<typeof setTimeout> | undefined;
  /** Half-typed chat message, preserved across structural rebuilds. */
  private draft = '';
  private pos: WidgetPos | null = null;
  private dragging = false;
  /** Set while a drag is in progress so it doesn't fire a click on release. */
  private dragMoved = false;
  /** Structure currently built, so updates can patch instead of rebuilding. */
  private builtFor: string | null = null;
  /** Size at the previous render, so a resize can re-anchor before it clips. */
  private lastSize: { w: number; h: number } | null = null;
  /** Message ids already drawn, so only new ones animate in (the list is redrawn on every update). */
  private drawnMessageIds: Set<string> | null = null;

  constructor(private cbs: WidgetCallbacks) {}

  mount() {
    if (this.host || !document.body) return;
    loadWidgetFont();
    this.host = document.createElement('div');
    this.host.id = HOST_ID;
    // The host element itself must not intercept clicks outside the widget.
    Object.assign(this.host.style, {
      position: 'fixed',
      bottom: '16px',
      right: '16px',
      zIndex: String(Z),
      width: 'auto',
      height: 'auto',
      pointerEvents: 'auto',
    } as Partial<CSSStyleDeclaration>);
    this.root = this.host.attachShadow({ mode: 'open' });
    // `.toasts` is a sibling of `.wrap` so reaction popups survive every
    // rebuild of the pill/panel and never affect its render signature.
    this.root.innerHTML = `<style>${CSS}</style><div class="toasts"></div><div class="wrap"></div>`;
    document.body.appendChild(this.host);
    window.addEventListener('resize', this.onViewportResize);
    document.addEventListener('fullscreenchange', this.onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', this.onFullscreenChange);
    this.render();
  }

  update(info: RoomInfo) {
    this.info = info;
    this.render();
  }

  setMessages(messages: ChatMessage[]) {
    const previous = this.seenMessageIds;
    const isFirstLoad = previous === null;
    const incoming = new Set(messages.map((m) => m.id));

    // Only messages that are genuinely new, from someone else, and not part of
    // the initial backfill when you first open the room.
    if (!isFirstLoad) {
      const fresh = messages.filter(
        (m) => !previous.has(m.id) && m.senderUid !== this.info?.myUid,
      );
      if (fresh.length > 0 && this.beepEnabled) beep();

      // Reactions flash over the widget — but only when the chat isn't already
      // on screen, where they'd show up in the list anyway.
      const chatVisible = this.expanded && this.chatOpen;
      if (!chatVisible) {
        for (const m of fresh) {
          if (isReaction(m.text)) this.showToast(m.senderName, m.text.trim());
        }
      }
    }

    this.seenMessageIds = incoming;
    this.messages = messages;
    this.render();
  }

  setBeepEnabled(enabled: boolean) {
    this.beepEnabled = enabled;
  }

  /**
   * Show the outcome of a re-sync on the button, then restore its label. The
   * text comes from the controller, so it reflects what actually happened
   * rather than assuming the sync worked.
   */
  setResyncStatus(text: string) {
    const btn = this.root?.querySelector<HTMLButtonElement>('.resync');
    if (!btn) return;
    btn.textContent = text;
    if (this.resyncTimer) clearTimeout(this.resyncTimer);
    if (text === 'Syncing…') return; // wait for the real result
    this.resyncTimer = setTimeout(() => {
      const b = this.root?.querySelector<HTMLButtonElement>('.resync');
      if (b) b.textContent = '⟲ Sync with host';
    }, 2000);
  }

  /** Restore a saved position (called once, after mount). */
  setPosition(pos: WidgetPos | null) {
    if (!pos || !this.host) return;
    // Clamp on restore: the position may have been saved on a larger window, or
    // on another monitor entirely.
    const r = this.host.getBoundingClientRect();
    this.pos = clampToViewport(pos.x, pos.y, r.width, r.height);
    this.applyPosition();
  }

  /**
   * Move the widget with the pointer. Bound to the pill and the panel header so
   * the rest of the UI stays clickable. A drag that actually moved suppresses
   * the click that would otherwise expand/collapse on release.
   */
  private beginDrag(e: PointerEvent) {
    if (!this.host || e.button !== 0) return;
    const rect = this.host.getBoundingClientRect();
    const offX = e.clientX - rect.left;
    const offY = e.clientY - rect.top;
    this.dragging = true;
    this.dragMoved = false;

    const move = (ev: PointerEvent) => {
      if (!this.dragging || !this.host) return;
      if (Math.abs(ev.clientX - e.clientX) + Math.abs(ev.clientY - e.clientY) > 4) {
        this.dragMoved = true;
      }
      // Keep it on screen whatever the viewport size.
      const w = rect.width || 60;
      const h = rect.height || 40;
      this.pos = clampToViewport(ev.clientX - offX, ev.clientY - offY, w, h);
      this.applyPosition();
    };

    const up = () => {
      this.dragging = false;
      window.removeEventListener('pointermove', move, true);
      window.removeEventListener('pointerup', up, true);
      if (this.dragMoved && this.pos) this.cbs.onMove(this.pos);
      // Let the click handler run first, then clear the flag.
      setTimeout(() => (this.dragMoved = false), 0);
    };

    window.addEventListener('pointermove', move, true);
    window.addEventListener('pointerup', up, true);
  }

  /** Switch from the default bottom-right anchoring to explicit coordinates. */
  private applyPosition() {
    if (!this.host || !this.pos) return;
    Object.assign(this.host.style, {
      left: `${this.pos.x}px`,
      top: `${this.pos.y}px`,
      right: 'auto',
      bottom: 'auto',
    } as Partial<CSSStyleDeclaration>);
  }

  /**
   * Keep the widget on screen when it changes size (expand ⇄ collapse, chat
   * opening, the roster growing).
   *
   * Until it is dragged the host is anchored `bottom/right`, so it grows up and
   * left and stays put. `applyPosition()` switches to explicit `left/top` — from
   * then on it grows DOWN and RIGHT. Expanding a pill parked near the bottom
   * pushed most of the panel off screen, and collapsing again left the pill
   * where the panel's top-left corner had been, which reads as the widget
   * wandering towards the middle of the screen.
   *
   * Fix: hold whichever corner the widget is nearest, then clamp.
   */
  private reflow() {
    const host = this.host;
    if (!host || this.dragging) return;
    const r = host.getBoundingClientRect();
    const prev = this.lastSize;
    this.lastSize = { w: r.width, h: r.height };
    // No explicit position yet — CSS edge anchoring already does the right
    // thing, and taking over here would defeat it.
    if (!this.pos || !prev) return;
    if (prev.w === r.width && prev.h === r.height) return;

    // Judged from where the box sat BEFORE it resized: `r.left/top` is
    // unchanged by a top-left-anchored resize, so the old box is prev-sized.
    const anchorRight = r.left + prev.w / 2 > window.innerWidth / 2;
    const anchorBottom = r.top + prev.h / 2 > window.innerHeight / 2;
    const x = anchorRight ? r.left + prev.w - r.width : r.left;
    const y = anchorBottom ? r.top + prev.h - r.height : r.top;
    this.pos = clampToViewport(x, y, r.width, r.height);
    this.applyPosition();
  }

  /**
   * Follow the page in and out of fullscreen.
   *
   * A fullscreen element is promoted to the browser's *top layer*: it and its
   * descendants are the only things painted, so a `position: fixed` host sitting
   * on `document.body` simply disappears. Re-parenting the host INTO the
   * fullscreen element puts it back in the rendered subtree; `position: fixed`
   * still resolves against the viewport there, so it lands where it was.
   *
   * The shadow root travels with the host, so no state, styling or listener is
   * lost — the widget does not re-render.
   *
   * LIMIT: this only works when the fullscreen element is in THIS document. If
   * the video lives in a cross-origin player iframe and that iframe goes
   * fullscreen, the top document's `fullscreenElement` is the <iframe> itself,
   * which cannot take our host as a child. Covering that would mean rendering a
   * second widget inside the player frame, which DECISIONS.md #12 rejects for
   * good reasons (clipped to the frame, destroyed on every iframe reload).
   */
  private onFullscreenChange = () => {
    const host = this.host;
    if (!host) return;
    const fs = document.fullscreenElement;
    // An <iframe> going fullscreen cannot host our element — leave it be rather
    // than moving the widget somewhere it will never be painted.
    const target =
      fs && fs.nodeName !== 'IFRAME' && fs !== host && !host.contains(fs) ? fs : document.body;
    if (host.parentNode !== target) target.appendChild(host);
    // The fullscreen box is a different size to the page, so a saved position
    // can now be out of bounds.
    this.onViewportResize();
  };

  /** A smaller viewport can strand a saved position off screen. */
  private onViewportResize = () => {
    if (!this.host || !this.pos) return;
    const r = this.host.getBoundingClientRect();
    this.pos = clampToViewport(this.pos.x, this.pos.y, r.width, r.height);
    this.applyPosition();
  };

  private makeDraggable(el: Element | null) {
    el?.addEventListener('pointerdown', (e) => this.beginDrag(e as PointerEvent));
  }

  /**
   * Flash a reaction above the widget for a few seconds. Rendered into the
   * `.toasts` container, which sits outside `.wrap` so it is untouched by
   * pill/panel rebuilds.
   */
  private showToast(who: string, emoji: string) {
    const host = this.root?.querySelector('.toasts');
    if (!host) return;
    const el = document.createElement('div');
    el.className = 'toast';
    el.innerHTML = `<span class="t-emoji">${esc(emoji)}</span><span class="t-who">${esc(who)}</span>`;
    host.appendChild(el);
    // Keep the stack short if reactions arrive in a burst.
    while (host.childElementCount > 4) host.firstElementChild?.remove();
    setTimeout(() => {
      el.classList.add('out');
      setTimeout(() => el.remove(), 260);
    }, 3000);
  }

  setError(message: string | null) {
    this.error = message;
    this.render();
  }

  destroy() {
    window.removeEventListener('resize', this.onViewportResize);
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    document.removeEventListener('webkitfullscreenchange', this.onFullscreenChange);
    this.host?.remove();
    this.host = null;
    this.root = null;
    this.info = null;
    this.error = null;
    this.expanded = false;
  }

  private render() {
    if (!this.root) return;
    const wrap = this.root.querySelector('.wrap');
    if (!wrap) return;

    // The transcript being ON SCREEN is what marks it read — not the click that
    // opened it. `chatOpen` survives collapsing, so re-expanding a widget whose
    // chat was already open put the messages back in front of the user without
    // ever passing through the toggle handler: the badge kept climbing over
    // messages they were looking at, and nothing could ever clear it.
    //
    // Reporting the read position rather than zeroing a local counter is what
    // lets the same conversation read in the web dashboard settle this badge —
    // the background owns the count and shares it across every client.
    if (this.expanded && this.chatOpen) {
      const newest = this.messages[this.messages.length - 1];
      if (newest) this.cbs.onChatRead(newest.at);
    }

    // Only rebuild the DOM when the *structure* changes. Messages arriving
    // while you're mid-sentence must not wipe the input or steal focus.
    // The role must be part of the signature: host and viewer get different
    // controls, and those are structural. Without it a handoff only repainted
    // the text, leaving a new host looking at "Sync with host".
    const signature = !this.expanded
      ? `pill:${this.reactionOpen}`
      : `panel:${this.chatOpen}:${this.info?.role ?? '?'}`;
    if (signature === this.builtFor) {
      this.patch(wrap as HTMLElement);
      // Patching changes height too — chat opening, the roster growing, the
      // error row appearing — so this is not only for expand/collapse.
      this.reflow();
      return;
    }
    // Switching between the pill and the panel animates in; rebuilding the same kind (chat
    // toggled, a host handoff) does not, so the panel never jumps under the user's hand.
    const kindChanged = this.builtFor?.split(':')[0] !== signature.split(':')[0];
    this.builtFor = signature;
    this.build(wrap as HTMLElement);
    if (kindChanged) {
      wrap.classList.remove('enter');
      void (wrap as HTMLElement).offsetWidth;
      wrap.classList.add('enter');
    }
    this.reflow();
  }

  /** Update only the volatile parts of the existing structure. */
  private patch(wrap: HTMLElement) {
    const i = this.info;
    if (!this.expanded) {
      const pill = wrap.querySelector<HTMLElement>('.pill');
      const dot = wrap.querySelector<HTMLElement>('.dot');
      const role = wrap.querySelector<HTMLElement>('.role');
      const text = wrap.querySelector<HTMLElement>('.pill-text');
      const count = wrap.querySelector<HTMLElement>('.count');
      if (dot) dot.className = `dot ${i?.isPlaying ? 'playing' : ''}`;
      if (text) text.textContent = i ? shorten(i.name, 16) : 'WatchParty';
      if (role) {
        // Reveal the HOST/VIEWER chip once we know which we are.
        role.className = i ? `role ${i.role}` : 'role hide';
        role.textContent = i ? roleLabel(i.role) : '';
      }
      if (count) {
        count.className = i ? 'count' : 'count hide';
        count.textContent = i ? String(i.members) : '';
      }
      if (pill && i) pill.title = `You are the ${roleLabel(i.role).toLowerCase()} — click to expand`;
      const unread = wrap.querySelector<HTMLElement>('.unread');
      if (unread) {
        unread.className = this.unread > 0 ? 'unread' : 'unread hide';
        unread.textContent = this.unread > 99 ? '99+' : String(this.unread);
      }
      return;
    }
    if (i) {
      this.setText(wrap, '[data-f="title"]', i.name);
      this.setText(wrap, '[data-f="host"]', i.ownerName);
      this.setText(wrap, '[data-f="members"]', String(i.members));
      this.setText(wrap, '[data-f="state"]', i.isPlaying ? '▶ playing' : '⏸ paused');
      const badge = wrap.querySelector('[data-f="role"]');
      if (badge) {
        badge.className = `v badge ${i.role}`;
        badge.textContent = roleLabel(i.role);
      }
    }
    // Roster changes (someone joined or left) refresh in place rather than
    // rebuilding the panel, so a half-typed chat message survives.
    //
    // The key is `roster`, NOT `members`. The "Watching" row above is also a
    // data-f field, and when both used `members` querySelector returned the
    // count span — so this overwrote the count with the roster markup and the
    // real roster never updated at all.
    const roster = wrap.querySelector<HTMLElement>('[data-f="roster"]');
    if (roster && i) {
      const next = membersHtml(i);
      if (roster.innerHTML !== next) roster.innerHTML = next;
    }

    // Unread count on the Chat button, for messages arriving while the panel is
    // open but the transcript is hidden.
    this.setText(wrap, '[data-f="chatlabel"]', this.chatOpen ? 'Hide chat' : '💬 Chat');
    const tbadge = wrap.querySelector<HTMLElement>('[data-f="tbadge"]');
    if (tbadge) {
      const show = !this.chatOpen && this.unread > 0;
      tbadge.className = show ? 'tbadge' : 'tbadge hide';
      tbadge.textContent = this.unread > 99 ? '99+' : String(this.unread);
    }

    const err = wrap.querySelector<HTMLElement>('[data-f="err"]');
    if (err) err.style.display = this.error ? 'block' : 'none';
    // Patch only the text node, or the Retry button inside would be destroyed.
    this.setText(wrap, '[data-f="errtext"]', this.error ?? '');
    if (this.chatOpen) this.renderMessages(wrap);
  }

  private setText(wrap: HTMLElement, sel: string, value: string) {
    const el = wrap.querySelector(sel);
    if (el) el.textContent = value;
  }

  private renderMessages(wrap: HTMLElement) {
    const list = wrap.querySelector<HTMLElement>('.messages');
    if (!list) return;
    const atBottom = list.scrollHeight - list.scrollTop - list.clientHeight < 40;
    const drawn = this.drawnMessageIds;
    this.drawnMessageIds = new Set(this.messages.map((m) => m.id));
    list.innerHTML = this.messages.length
      ? this.messages
          .map(
            (m) => `
        <div class="msg${m.senderUid === this.info?.myUid ? ' mine' : ''}${drawn && !drawn.has(m.id) ? ' new' : ''}">
          <span class="who">${esc(m.senderName)}</span>
          <span class="body">${renderBody(m.text, this.info?.duration ?? 0)}</span>
        </div>`,
          )
          .join('')
      : '<div class="empty-chat">No messages yet — say hello 👋</div>';
    // Keep pinned to the newest unless the user has scrolled up to read back.
    if (atBottom) list.scrollTop = list.scrollHeight;
  }

  private build(wrap: HTMLElement) {
    const i = this.info;
    // A rebuild replaces the composer, so carry any half-typed message across.
    // Ownership handoff now triggers a rebuild, and it must not eat your draft.
    const draft = wrap.querySelector<HTMLInputElement>('.chat-input')?.value ?? this.draft;
    this.draft = draft;

    if (!this.expanded) {
      // Always emit the full structure — `patch()` fills it in as info arrives.
      // Building conditionally meant the role/count spans never existed, since
      // the widget mounts before the first ROOM_INFO.
      wrap.innerHTML = `
        ${
          this.reactionOpen
            ? `<div class="picker">
                 ${REACTIONS.map(
                   (e) => `<button class="emoji" data-e="${e}" title="Send ${e}">${e}</button>`,
                 ).join('')}
               </div>`
            : ''
        }
        <div class="pillrow">
          <button class="react" title="Quick reaction">☺</button>
          <button class="pill">
            <span class="dot"></span>
            <span class="role hide"></span>
            <span class="pill-text">WatchParty</span>
            <span class="count hide"></span>
          </button>
          <span class="unread hide" title="Unread messages"></span>
        </div>`;

      wrap.querySelector('.react')?.addEventListener('click', () => {
        this.reactionOpen = !this.reactionOpen;
        this.render();
      });
      wrap.querySelectorAll<HTMLElement>('.emoji').forEach((btn) =>
        btn.addEventListener('click', () => {
          const emoji = btn.dataset.e;
          if (emoji) this.cbs.onSend(emoji);
          this.reactionOpen = false;
          this.render();
        }),
      );
      const pill = wrap.querySelector('.pill');
      pill?.addEventListener('click', () => {
        if (this.dragMoved) return; // that was a drag, not a click
        this.expanded = true;
        this.render();
      });
      this.makeDraggable(pill);
      // Fill the placeholders (role chip, count, unread badge) in one place.
      this.patch(wrap);
      return;
    }

    wrap.innerHTML = `
      <div class="panel${this.chatOpen ? ' with-chat' : ''}">
        <div class="head">
          <div class="title" data-f="title">${i ? esc(i.name) : 'WatchParty'}</div>
          <button class="close" title="Collapse">×</button>
        </div>
        <div class="rows">
          <div class="row"><span class="k">You are</span><span class="v badge ${i?.role ?? ''}" data-f="role">${i ? roleLabel(i.role) : '—'}</span></div>
          <div class="row"><span class="k">Host</span><span class="v" data-f="host">${i ? esc(i.ownerName) : '—'}</span></div>
          <div class="row"><span class="k">Watching</span><span class="v" data-f="members">${i?.members ?? '—'}</span></div>
          <div class="row"><span class="k">State</span><span class="v" data-f="state">${i ? (i.isPlaying ? '▶ playing' : '⏸ paused') : '—'}</span></div>
        </div>
        <div class="err" data-f="err" style="display:${this.error ? 'block' : 'none'}">
          <span data-f="errtext">${esc(this.error ?? '')}</span>
          <button class="retry">Retry</button>
        </div>
        ${
          this.chatOpen
            ? `<div class="chat">
                 <div class="messages"></div>
                 <form class="composer">
                   <input class="chat-input" type="text" placeholder="Message…"
                          maxlength="${CHAT_MAX_LEN}" autocomplete="off" />
                   <button class="send" type="submit" title="Send">➤</button>
                 </form>
               </div>`
            : ''
        }
        ${
          i?.role === 'viewer'
            ? `<button class="resync" title="Jump to where the host is now — and follow them if they've changed video">⟲ Sync with host</button>`
            : ''
        }
        ${
          i?.role === 'owner'
            ? `<button class="reselect" title="Point the room at a different video">⇄ Change video</button>`
            : ''
        }
        ${i?.role === 'owner' ? `<div class="members" data-f="roster">${membersHtml(i)}</div>` : ''}
        <div class="actions">
          <button class="chat-toggle">
            <span data-f="chatlabel">${this.chatOpen ? 'Hide chat' : '💬 Chat'}</span>
            <span class="tbadge hide" data-f="tbadge"></span>
          </button>
          <button class="dash" title="Open the dashboard in a new tab">⧉ Dashboard</button>
        </div>
        <button class="leave">Leave room</button>
      </div>`;

    wrap.querySelector('.close')?.addEventListener('click', () => {
      this.expanded = false;
      this.render();
    });
    // Drag the panel by its header, so the rest of the panel stays clickable.
    this.makeDraggable(wrap.querySelector('.head'));
    wrap.querySelector('.leave')?.addEventListener('click', () => this.cbs.onLeave());
    wrap.querySelector('.dash')?.addEventListener('click', () => this.cbs.onOpenDashboard());
    wrap.querySelector('.reselect')?.addEventListener('click', () => {
      this.expanded = false; // collapse so the picker overlay is unobstructed
      this.render();
      this.cbs.onReselectVideo();
    });
    // Delegated: the roster is re-rendered in place when people come and go.
    wrap.querySelector('[data-f="roster"]')?.addEventListener('click', (e) => {
      const btn = (e.target as HTMLElement)?.closest?.('.mk') as HTMLElement | null;
      const uid = btn?.dataset.uid;
      if (!btn || !uid) return;
      btn.textContent = 'Handing over…';
      this.cbs.onTransferHost(uid);
    });
    wrap.querySelector('.retry')?.addEventListener('click', (e) => {
      this.cbs.onRetry();
      const btn = e.currentTarget as HTMLButtonElement;
      btn.textContent = 'Retrying…';
      setTimeout(() => (btn.textContent = 'Retry'), 2500);
    });
    wrap.querySelector('.resync')?.addEventListener('click', () => {
      // Show the real outcome, reported back by the controller — not an
      // assumed success. setResyncStatus() restores the label afterwards.
      this.setResyncStatus('Syncing…');
      this.cbs.onResync();
    });
    wrap.querySelector('.chat-toggle')?.addEventListener('click', () => {
      this.chatOpen = !this.chatOpen;
      // No need to clear `unread` here — render() does it for every route that
      // puts the transcript on screen, this one included.
      this.render();
      if (this.chatOpen) {
        wrap.querySelector<HTMLInputElement>('.chat-input')?.focus();
      }
    });

    const form = wrap.querySelector<HTMLFormElement>('.composer');
    const input = wrap.querySelector<HTMLInputElement>('.chat-input');
    if (input && this.draft) input.value = this.draft;
    input?.addEventListener('input', () => {
      this.draft = input.value;
    });
    form?.addEventListener('submit', (e) => {
      e.preventDefault();
      const text = input?.value.trim() ?? '';
      if (!text) return;
      this.cbs.onSend(text);
      this.draft = '';
      if (input) input.value = '';
    });
    // Streaming sites bind global hotkeys (space = play/pause, arrows = seek).
    // Keep our typing from reaching them.
    input?.addEventListener('keydown', (e) => e.stopPropagation());

    // Delegated: the message list is re-rendered on every update, so the
    // listener is bound to the container rather than to individual buttons.
    wrap.querySelector('.messages')?.addEventListener('click', (e) => {
      const btn = (e.target as HTMLElement)?.closest?.('.ts') as HTMLElement | null;
      if (!btn) return;
      const seconds = Number(btn.dataset.s);
      if (!Number.isFinite(seconds)) return;
      this.cbs.onSeek(seconds);
      btn.classList.add('jumped');
      setTimeout(() => btn.classList.remove('jumped'), 700);
    });

    if (this.chatOpen) this.renderMessages(wrap);
  }
}

/**
 * Matches timestamps people actually type in chat: `4:20`, `04:20`, `1:02:03`.
 * Bounded by non-digits so it won't chop into a longer number.
 */
const TIMESTAMP_RE = /(?<![\d:])(\d{1,2}:\d{2}(?::\d{2})?)(?![\d:])/g;

/** Convert `mm:ss` / `hh:mm:ss` to seconds, or null if the parts are invalid. */
function parseTimestamp(text: string): number | null {
  const parts = text.split(':').map(Number);
  if (parts.some((n) => !Number.isFinite(n))) return null;
  const [h, m, s] = parts.length === 3 ? parts : [0, parts[0], parts[1]];
  // Reject things like 9:75 that merely look like a timestamp.
  if (m > 59 || s > 59) return null;
  return h * 3600 + m * 60 + s;
}

/**
 * Render a message body, turning in-range timestamps into jump buttons.
 *
 * Each literal segment is escaped individually so the markup we inject can't be
 * spoofed by message text. A timestamp beyond the video's length (or when the
 * duration isn't known yet) stays plain text.
 */
function renderBody(text: string, duration: number): string {
  let out = '';
  let last = 0;
  for (const match of text.matchAll(TIMESTAMP_RE)) {
    const raw = match[1];
    const at = match.index ?? 0;
    out += esc(text.slice(last, at));
    const seconds = parseTimestamp(raw);
    const inRange = seconds !== null && duration > 0 && seconds <= duration;
    out += inRange
      ? `<button class="ts" data-s="${seconds}" title="Jump to ${esc(raw)}">${esc(raw)}</button>`
      : esc(raw);
    last = at + raw.length;
  }
  out += esc(text.slice(last));
  return out;
}

/** Emoji offered by the quick-reaction button. */
const REACTIONS = ['😂', '❤️', '🔥', '😮', '😢', '👏', '👍', '🎉'];

/**
 * A message counts as a reaction when it is nothing but emoji. Reactions are
 * ordinary encrypted chat messages — this only decides whether to also flash a
 * popup, so a false negative just means it appears in chat like any message.
 */
function isReaction(text: string): boolean {
  const t = text.trim();
  if (!t || t.length > 12) return false;
  try {
    return /^(?:[\p{Extended_Pictographic}\p{Emoji_Modifier}‍️])+$/u.test(t);
  } catch {
    return false; // engine without Unicode property escapes
  }
}

/**
 * The host's "hand over to…" roster. Kept separate from the panel template so
 * it can be patched in place when someone joins or leaves — rebuilding the
 * whole panel would wipe a half-typed chat message.
 */
function membersHtml(i: RoomInfo): string {
  const others = i.memberList.filter((m) => !m.isHost);
  if (others.length === 0) {
    return `<div class="members-h">Watching</div><div class="members-none">Nobody else here yet</div>`;
  }
  return (
    `<div class="members-h">Hand host to…</div>` +
    others
      .map(
        (m) =>
          `<button class="mk" data-uid="${esc(m.uid)}" title="Make ${esc(m.name)} the host">${esc(m.name)}</button>`,
      )
      .join('')
  );
}

/** "owner" is the internal term; the UI says HOST everywhere. */
function roleLabel(role: RoomInfo['role']): string {
  return role === 'owner' ? 'Host' : 'Viewer';
}

function esc(s: string): string {
  return s.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}

function shorten(s: string, n: number): string {
  return s.length > n ? s.slice(0, n - 1) + '…' : s;
}

// Themed from src/content/theme.ts: the landing page's system (white card, near-black ink, one
// green accent, Urbanist) drawn over whatever the page shows. Every size here stays readable
// over video: 11px is the floor, and text sits on the card, never on the footage.
const CSS = `
:host, * { box-sizing: border-box; }
.wrap { font-family: ${T.font}; color: ${T.ink}; -webkit-font-smoothing: antialiased; }
.hide { display: none !important; }
button { font-family: inherit; }
button:focus-visible, input:focus-visible { outline: none; box-shadow: 0 0 0 3px rgb(31 107 58 / .3); }

@keyframes pop {
  from { opacity: 0; transform: translateY(8px) scale(.94); }
  to   { opacity: 1; transform: none; }
}
@keyframes rise {
  from { opacity: 0; transform: translateY(6px); }
  to   { opacity: 1; transform: none; }
}
@keyframes live {
  0%   { box-shadow: 0 0 0 0 rgb(47 154 87 / .5); }
  70%  { box-shadow: 0 0 0 7px rgb(47 154 87 / 0); }
  100% { box-shadow: 0 0 0 0 rgb(47 154 87 / 0); }
}
@keyframes pulse {
  0%, 100% { box-shadow: 0 0 0 0 rgb(180 35 24 / .5), 0 2px 8px rgb(0 0 0 / .25); }
  50%      { box-shadow: 0 0 0 7px rgb(180 35 24 / 0), 0 2px 8px rgb(0 0 0 / .25); }
}
/* Pill <-> panel: the new one pops in from its anchor corner. */
.wrap.enter .pillrow, .wrap.enter .panel { animation: pop .26s ${T.spring} both; }

/* --- collapsed row: reaction button + pill --------------------------------- */
.pillrow { display: flex; align-items: center; gap: 8px; position: relative; }

.react {
  width: 40px; height: 40px; flex-shrink: 0;
  display: grid; place-items: center;
  border: 1px solid ${T.line}; border-radius: 50%;
  background: ${T.card}; color: ${T.ink};
  font-size: 18px; line-height: 1; cursor: pointer;
  box-shadow: ${T.shadow};
  transition: transform .18s ${T.spring}, border-color .15s ${T.ease};
}
.react:hover { transform: translateY(-2px) rotate(-8deg); border-color: ${T.lineStrong}; }
.react:active { transform: scale(.92); }

.picker {
  display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px;
  margin-bottom: 10px; padding: 8px;
  background: ${T.card}; border: 1px solid ${T.line};
  border-radius: 18px; box-shadow: ${T.shadow};
  animation: pop .22s ${T.spring} both;
  transform-origin: bottom left;
}
.emoji {
  border: none; border-radius: 12px; background: transparent;
  font-size: 20px; line-height: 1; padding: 7px; cursor: pointer;
  transition: transform .16s ${T.spring}, background-color .15s ${T.ease};
}
.emoji:hover { background: ${T.surface}; transform: scale(1.18); }
.emoji:active { transform: scale(.94); }

/* --- incoming reaction popups --------------------------------------------- */
.toasts {
  position: absolute; bottom: 100%; right: 0; margin-bottom: 10px;
  display: flex; flex-direction: column; align-items: flex-end; gap: 6px;
  pointer-events: none;
}
.toast {
  display: flex; align-items: center; gap: 8px;
  padding: 7px 14px 7px 10px; border-radius: 999px;
  background: ${T.card}; border: 1px solid ${T.line};
  box-shadow: ${T.shadow};
  animation: pop .3s ${T.spring} both;
  white-space: nowrap;
}
.toast.out { opacity: 0; transform: translateY(-8px) scale(.96); transition: all .26s ${T.ease}; }
.t-emoji { font-size: 20px; line-height: 1; }
.t-who { font: 600 12px/1 ${T.font}; color: ${T.ink}; }

.pill {
  display: flex; align-items: center; gap: 8px;
  height: 40px; padding: 0 8px 0 14px;
  border: 1px solid ${T.line}; border-radius: 999px;
  background: ${T.card}; color: ${T.ink};
  font: 600 14px/1 ${T.font}; cursor: pointer;
  box-shadow: ${T.shadow};
  transition: transform .18s ${T.ease}, border-color .15s ${T.ease};
}
.pill:hover { transform: translateY(-2px); border-color: ${T.lineStrong}; }
.pill-text { white-space: nowrap; letter-spacing: -.01em; }
.count {
  min-width: 24px; height: 24px; padding: 0 8px;
  display: inline-flex; align-items: center; justify-content: center;
  background: ${T.surface}; color: ${T.ink}; border-radius: 999px;
  font: 600 12px/1 ${T.font};
}
/* Role chip on the collapsed pill, so host/viewer is visible without expanding. */
.role { font: 600 11px/1 ${T.font}; padding: 5px 8px; border-radius: 999px; flex-shrink: 0; }
.role.owner { background: ${T.ink}; color: #fff; }
.role.viewer { background: ${T.accentSoft}; color: ${T.accent}; }
.dot {
  width: 8px; height: 8px; border-radius: 50%;
  background: #b5b5b5; flex-shrink: 0;
  transition: background-color .2s ${T.ease};
}
.dot.playing { background: ${T.live}; animation: live 2s ease-out infinite; }

.panel {
  width: 260px; padding: 16px;
  /* Never taller than the viewport: the roster and chat both grow, and without
     a cap the panel simply ran off the bottom of the screen. */
  max-height: calc(100vh - 24px);
  overflow-y: auto; overscroll-behavior: contain;
  transition: width .22s ${T.ease};
  background: ${T.card}; color: ${T.ink};
  border: 1px solid ${T.line}; border-radius: 22px;
  box-shadow: ${T.shadow};
}
.panel::-webkit-scrollbar { width: 6px; }
.panel::-webkit-scrollbar-thumb { background: ${T.lineStrong}; border-radius: 3px; }
.head {
  display: flex; align-items: center; justify-content: space-between; gap: 8px;
  padding-bottom: 12px; border-bottom: 1px solid ${T.line};
}
.title {
  font-size: 16px; font-weight: 600; letter-spacing: -.015em; overflow: hidden;
  text-overflow: ellipsis; white-space: nowrap;
}
.close {
  width: 30px; height: 30px; flex-shrink: 0; display: grid; place-items: center;
  background: none; border: none; border-radius: 50%; color: ${T.muted};
  font-size: 20px; line-height: 1; cursor: pointer;
  transition: background-color .15s ${T.ease}, color .15s ${T.ease};
}
.close:hover { background: ${T.surface}; color: ${T.ink}; }

.rows { margin-top: 12px; display: grid; gap: 8px; }
.row { display: flex; justify-content: space-between; align-items: center; font-size: 13px; }
.k { color: ${T.muted}; }
.v { font-weight: 600; }
.badge { font-size: 11px; font-weight: 600; padding: 3px 9px; border-radius: 999px; }
.badge.owner { background: ${T.ink}; color: #fff; }
.badge.viewer { background: ${T.accentSoft}; color: ${T.accent}; }

.hint { margin-top: 10px; font-size: 12px; color: ${T.muted}; line-height: 1.45; }
.muted { color: ${T.muted}; font-size: 12px; }
.err {
  margin-top: 12px; padding: 10px 11px; border-radius: 12px; font-size: 12px;
  background: ${T.dangerSoft}; color: ${T.danger};
  border: 1px solid rgb(180 35 24 / .3); line-height: 1.45;
  animation: rise .24s ${T.ease} both;
}
.retry {
  display: block; margin-top: 8px; height: 30px; padding: 0 12px;
  border: 1px solid ${T.danger}; border-radius: 999px;
  background: ${T.card}; color: ${T.danger};
  font: 600 12px/1 ${T.font}; cursor: pointer;
}
.retry:hover { background: ${T.danger}; color: #fff; }

/* Chat makes the panel meaningfully bigger so the transcript is readable. */
.panel.with-chat { width: 344px; }

.chat { margin-top: 14px; border-top: 1px solid ${T.line}; padding-top: 12px; }
.messages {
  height: 260px; overflow-y: auto; overflow-x: hidden;
  display: flex; flex-direction: column; gap: 8px;
  padding-right: 4px;
}
.messages::-webkit-scrollbar { width: 6px; }
.messages::-webkit-scrollbar-thumb { background: ${T.lineStrong}; border-radius: 3px; }

.msg {
  align-self: flex-start; max-width: 88%;
  padding: 7px 11px; border-radius: 14px 14px 14px 4px;
  background: ${T.surface};
  font-size: 13px; line-height: 1.4; word-break: break-word;
}
.msg.mine { align-self: flex-end; background: ${T.accent}; color: #fff; border-radius: 14px 14px 4px 14px; }
.msg.new { animation: rise .26s ${T.ease} both; }
.msg .who { display: block; color: ${T.muted}; font-size: 11px; font-weight: 600; margin-bottom: 2px; }
.msg.mine .who { color: rgb(255 255 255 / .75); }
.msg .body { color: inherit; }

/* Clickable timestamp inside a message. */
.ts {
  display: inline; padding: 1px 6px; margin: 0 1px;
  border: 1px solid ${T.lineStrong}; border-radius: 6px;
  background: ${T.card}; color: ${T.ink};
  font: 500 12px/1.3 ${T.mono};
  cursor: pointer; vertical-align: baseline;
  transition: background-color .15s ${T.ease}, color .15s ${T.ease};
}
.ts:hover { background: ${T.ink}; color: #fff; border-color: ${T.ink}; }
.msg.mine .ts { background: rgb(255 255 255 / .16); color: #fff; border-color: rgb(255 255 255 / .35); }
.ts.jumped { background: ${T.live}; border-color: ${T.live}; color: #fff; animation: pop .3s ${T.spring}; }
.empty-chat { color: ${T.muted}; font-size: 13px; text-align: center; padding: 18px 0; }

.composer { display: flex; gap: 6px; margin-top: 10px; }
.chat-input {
  flex: 1; min-width: 0; height: 38px; padding: 0 14px;
  border: 1px solid ${T.lineStrong}; border-radius: 999px;
  background: ${T.card}; color: ${T.ink};
  font: 400 13px/1 ${T.font};
  transition: border-color .15s ${T.ease}, box-shadow .15s ${T.ease};
}
.chat-input:focus { outline: none; border-color: ${T.accent}; box-shadow: 0 0 0 3px rgb(31 107 58 / .18); }
.send {
  width: 38px; height: 38px; flex-shrink: 0;
  border: none; border-radius: 50%; background: ${T.accent}; color: #fff;
  cursor: pointer; font-size: 13px;
  transition: background-color .15s ${T.ease}, transform .15s ${T.spring};
}
.send:hover { background: ${T.accentHover}; transform: translateX(2px); }
.send:active { transform: scale(.9); }

/* --- unread indicator ------------------------------------------------------ */
.unread {
  position: absolute; top: -6px; right: -6px;
  min-width: 20px; height: 20px; padding: 0 6px;
  display: flex; align-items: center; justify-content: center;
  border-radius: 999px; background: ${T.danger}; color: #fff;
  font: 700 11px/1 ${T.font};
  pointer-events: none;
  animation: pop .3s ${T.spring} both, pulse 1.8s ease-in-out .3s infinite;
}
.tbadge {
  display: inline-flex; align-items: center; justify-content: center;
  min-width: 18px; height: 18px; margin-left: 6px; padding: 0 5px;
  border-radius: 999px; background: ${T.danger}; color: #fff;
  font: 700 11px/1 ${T.font};
}

/* Drag handles */
.pill { cursor: grab; }
.pill:active { cursor: grabbing; }
.head { cursor: grab; user-select: none; }
.head:active { cursor: grabbing; }

/* --- actions: one filled button per role, the rest outlined pills --------- */
.resync, .reselect {
  width: 100%; height: 38px; margin-top: 12px; padding: 0 14px;
  border: 1px solid ${T.accent}; border-radius: 999px;
  background: ${T.accent}; color: #fff;
  font: 600 13px/1 ${T.font}; cursor: pointer;
  transition: background-color .15s ${T.ease}, transform .15s ${T.ease};
}
.resync:hover, .reselect:hover { background: ${T.accentHover}; transform: translateY(-1px); }
.resync:active, .reselect:active { transform: scale(.98); }

.members { margin-top: 14px; border-top: 1px solid ${T.line}; padding-top: 12px; }
.members-h { font-size: 12px; font-weight: 600; color: ${T.muted}; margin-bottom: 8px; }
.mk {
  display: flex; align-items: center; gap: 8px; width: 100%; margin-bottom: 6px; height: 36px; padding: 0 12px;
  border: 1px solid ${T.line}; border-radius: 12px;
  background: ${T.card}; color: ${T.ink};
  font: 500 13px/1 ${T.font}; cursor: pointer;
  text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  transition: border-color .15s ${T.ease}, background-color .15s ${T.ease};
}
.mk::before { content: ''; width: 6px; height: 6px; border-radius: 50%; background: ${T.live}; flex-shrink: 0; }
.mk:hover { border-color: ${T.accent}; background: ${T.accentSoft}; }
.members-none { font-size: 12px; color: ${T.muted}; }

.actions { display: flex; gap: 8px; margin-top: 10px; }
.chat-toggle, .dash, .leave {
  height: 38px; padding: 0 12px; border-radius: 999px;
  font: 600 13px/1 ${T.font}; cursor: pointer;
  transition: border-color .15s ${T.ease}, background-color .15s ${T.ease}, color .15s ${T.ease};
}
.chat-toggle, .dash {
  flex: 1; display: inline-flex; align-items: center; justify-content: center;
  background: ${T.card}; color: ${T.ink}; border: 1px solid ${T.lineStrong};
  white-space: nowrap;
}
.chat-toggle:hover, .dash:hover { border-color: ${T.ink}; }
.leave {
  width: 100%; margin-top: 8px;
  background: transparent; color: ${T.danger}; border: 1px solid transparent;
}
.leave:hover { background: ${T.dangerSoft}; border-color: rgb(180 35 24 / .3); }

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation: none !important; transition: none !important; }
}
`;
