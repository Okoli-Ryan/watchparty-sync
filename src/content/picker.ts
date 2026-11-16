import { finder } from '@medv/finder';
import { rankedVideos, type VideoCandidate } from './videoScan';
import { T, loadWidgetFont } from './theme';

// Keyboard-driven video picker.
//
// The user steps through the page's videos with the arrow keys — ordered most-
// likely-first by `rankedVideos()` — and confirms with Enter. The candidate in
// focus wears the highlight overlay.
//
// WHY NOT CLICK-TO-SELECT (what this replaced): selecting by click needed a
// transparent full-viewport overlay to catch the mouse, and that overlay sat
// above embedded players and swallowed their clicks. Streaming sites put the
// real player in a cross-origin iframe, so the top document's overlay was
// covering exactly the thing you wanted to click. Keyboard selection needs no
// click target at all, so every element here is `pointer-events: none` and the
// page underneath stays completely interactive.
//
// FRAMES: this script runs in every frame, and each frame can only see its own
// videos and only receives key events when it holds focus. So no frame can own
// the selection. Each reports its candidates to the background, which merges
// them into one ranked list and drives the highlight; every armed frame forwards
// key presses. The banner renders in the top frame only, like the widget.

export interface PickResult {
  selector: string;
  currentTime: number;
  /** URL/origin of THIS frame — the top page URL is resolved by the background. */
  frameUrl: string;
  frameOrigin: string;
  isTop: boolean;
}

type Callbacks = {
  /** This frame's videos, best first. Sent even when empty. */
  onCandidates: (videos: { score: number; label: string }[]) => void;
  /** An arrow key was pressed here; the background owns the actual cursor. */
  onNav: (delta: number) => void;
  onConfirm: () => void;
  onPick: (result: PickResult) => void;
  onError: (reason: string) => void;
  onCancel: () => void;
};

let active = false;
let isTopFrame = false;
let highlight: HTMLDivElement | null = null;
let label: HTMLDivElement | null = null;
let banner: HTMLDivElement | null = null;
/** This frame's candidates, in the same order the background indexes them. */
let candidates: VideoCandidate[] = [];
/** Which local candidate is highlighted, or null when the selection is elsewhere. */
let currentIndex: number | null = null;
let cbs: Callbacks | null = null;

const Z = 2147483000; // sit above virtually everything

function styleEl(el: HTMLElement, styles: Partial<CSSStyleDeclaration>) {
  Object.assign(el.style, styles);
}

function buildUi() {
  loadWidgetFont();
  const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  const glide = reduced ? 'none' : `top 160ms ${T.ease}, left 160ms ${T.ease}, width 160ms ${T.ease}, height 160ms ${T.ease}`;

  // The picked video wears the landing page's bright green outline; the rest of the page dims.
  highlight = document.createElement('div');
  styleEl(highlight, {
    position: 'fixed',
    pointerEvents: 'none',
    border: `3px solid ${T.pick}`,
    background: 'rgba(127, 212, 154, 0.1)',
    borderRadius: '12px',
    boxShadow: '0 0 0 2000px rgba(10, 10, 10, 0.5)',
    zIndex: String(Z + 1),
    display: 'none',
    transition: glide,
  });

  label = document.createElement('div');
  styleEl(label, {
    position: 'fixed',
    pointerEvents: 'none',
    zIndex: String(Z + 2),
    padding: '6px 12px',
    borderRadius: '999px',
    background: T.pick,
    color: T.ink,
    font: `600 12px/1.2 ${T.font}`,
    display: 'none',
    whiteSpace: 'nowrap',
    boxShadow: '0 6px 18px rgba(0, 0, 0, 0.3)',
    transition: reduced ? 'none' : `top 160ms ${T.ease}, left 160ms ${T.ease}`,
  });

  document.body.append(highlight, label);

  // The banner is the top frame's job: the video may be in a nested player
  // iframe, but the instructions belong over the whole page.
  if (!isTopFrame) return;
  banner = document.createElement('div');
  styleEl(banner, {
    position: 'fixed',
    top: '20px',
    left: '50%',
    transform: 'translateX(-50%)',
    zIndex: String(Z + 3),
    width: 'min(440px, calc(100vw - 32px))',
    padding: '18px 22px',
    borderRadius: '22px',
    background: T.card,
    color: T.ink,
    font: `400 14px/1.45 ${T.font}`,
    boxShadow: T.shadow,
    border: `1px solid ${T.line}`,
    textAlign: 'left',
    pointerEvents: 'none',
  });
  const key = (k: string) =>
    `<b style="display:inline-block;min-width:24px;padding:2px 7px;border:1px solid ${T.lineStrong};border-radius:7px;font:500 12px/1.4 ${T.mono};text-align:center;color:${T.ink};background:${T.card}">${k}</b>`;
  banner.innerHTML = `
    <div style="font:600 18px/1.25 ${T.font};letter-spacing:-.02em;margin-bottom:6px">
      Choose the video to sync
    </div>
    <div data-wp="status" style="color:${T.accent};font-weight:600;margin-bottom:12px">
      Looking for videos…
    </div>
    <div style="display:flex;flex-wrap:wrap;gap:6px 14px;color:${T.muted};font-size:13px">
      <span>${key('↑')} ${key('↓')} or ${key('←')} ${key('→')} to browse</span>
      <span>${key('Enter')} to pick</span>
      <span>${key('Esc')} to cancel</span>
    </div>
    <div style="margin-top:14px;padding:10px 12px;border-radius:12px;background:#fdf3df;
                color:#6b4600;font-size:13px;line-height:1.45">
      <b>Start the video playing first.</b> Many players don't create the
      video at all until you hit play, and a stopped one is easy to mistake for
      an advert.
    </div>`;
  document.body.append(banner);
  if (!reduced) {
    banner.animate(
      [
        { opacity: 0, transform: 'translateX(-50%) translateY(-10px) scale(0.97)' },
        { opacity: 1, transform: 'translateX(-50%)' },
      ],
      { duration: 280, easing: T.spring },
    );
  }
}

/** Draw the highlight over `el`, or hide it when there is nothing selected. */
function paint(el: HTMLVideoElement | null) {
  if (!highlight || !label) return;
  if (!el) {
    highlight.style.display = 'none';
    label.style.display = 'none';
    return;
  }
  const r = el.getBoundingClientRect();
  styleEl(highlight, {
    display: 'block',
    top: `${r.top}px`,
    left: `${r.left}px`,
    width: `${r.width}px`,
    height: `${r.height}px`,
  });
  label.textContent = candidates[currentIndex ?? 0]?.label ?? '<video>';
  styleEl(label, {
    display: 'block',
    // Below the box when it is flush against the top of the viewport.
    top: r.top > 34 ? `${r.top - 28}px` : `${r.top + 6}px`,
    left: `${Math.max(4, r.left)}px`,
  });
}

function repaint() {
  if (!active) return;
  paint(currentIndex === null ? null : (candidates[currentIndex]?.el ?? null));
}

/**
 * Highlight one of this frame's candidates, or clear when the background has
 * moved the selection into a different frame.
 */
export function highlightPick(index: number | null) {
  if (!active) return;
  currentIndex = index;
  const el = index === null ? null : candidates[index]?.el;
  if (el) ensureVisible(el);
  paint(el ?? null);
}

/**
 * Scroll `el` into view unless it is already fully on screen.
 *
 * "Fully" matters: checking only for completely-offscreen left a video with a
 * few pixels peeking over the fold unscrolled, so the highlight was drawn where
 * the user couldn't see it. A player LARGER than the viewport can never be
 * fully visible, so that case instead asks whether it covers the viewport —
 * otherwise every keypress would re-scroll a full-bleed player.
 */
function ensureVisible(el: HTMLElement) {
  const r = el.getBoundingClientRect();
  const vh = window.innerHeight;
  const vw = window.innerWidth;
  const okY = r.height >= vh ? r.top <= 0 && r.bottom >= vh : r.top >= 0 && r.bottom <= vh;
  const okX = r.width >= vw ? r.left <= 0 && r.right >= vw : r.left >= 0 && r.right <= vw;
  if (okY && okX) return;
  el.scrollIntoView({ block: 'center', inline: 'center', behavior: 'smooth' });
}

/**
 * Scroll the iframe carrying `origin` into view. Best effort, and only from the
 * top frame: a cross-origin child cannot scroll its parent, so a video inside an
 * off-screen iframe scrolls itself into view within a frame that is itself off
 * screen — the user sees nothing move.
 *
 * Matching is by origin because embed URLs carry per-session tokens. When
 * several iframes share an origin (ad networks, repeated embeds) there is no way
 * to tell which one holds the video, so nothing is scrolled rather than jumping
 * somewhere wrong.
 */
export function revealFrame(origin: string) {
  if (!active || !isTopFrame || !origin) return;
  const matches = [...document.querySelectorAll('iframe')].filter((f) => {
    try {
      return new URL(f.src, location.href).origin === origin;
    } catch {
      return false;
    }
  });
  if (matches.length !== 1) return;
  ensureVisible(matches[0]);
}

/** Update the top frame's banner. No-op in every other frame. */
export function showPickBanner(position: number, total: number, text: string) {
  const status = banner?.querySelector<HTMLElement>('[data-wp="status"]');
  if (!status) return;
  status.textContent = total === 0 ? 'No video found on this page' : `Video ${position} of ${total} · ${text}`;
}

/**
 * Resolve one of this frame's candidates into a stored selector and report it.
 * Called when the user pressed Enter and the background determined that the
 * selection lives here.
 */
export function takePick(index: number) {
  const video = candidates[index]?.el;
  if (!video) {
    cbs?.onError('That video is no longer on the page.');
    return;
  }
  try {
    const selector = finder(video, { root: document.body, timeoutMs: 1000 });
    // Verify the selector actually round-trips to the same node.
    if (document.querySelector(selector) !== video) {
      finish();
      cbs?.onError('Could not compute a stable selector for this video.');
      return;
    }
    const result: PickResult = {
      selector,
      currentTime: Number.isFinite(video.currentTime) ? video.currentTime : 0,
      frameUrl: location.href,
      frameOrigin: location.origin,
      isTop: window.top === window.self,
    };
    finish();
    cbs?.onPick(result);
  } catch (err) {
    finish();
    cbs?.onError('This video can’t be targeted (' + (err as Error).message + ').');
  }
}

function onKey(e: KeyboardEvent) {
  if (!active) return;
  const nav =
    e.key === 'ArrowDown' || e.key === 'ArrowRight'
      ? 1
      : e.key === 'ArrowUp' || e.key === 'ArrowLeft'
        ? -1
        : 0;

  if (nav === 0 && e.key !== 'Enter' && e.key !== 'Escape') return;

  // Streaming players bind arrows to seek and Enter to fullscreen. Swallow the
  // event in the capture phase so picking never disturbs the page underneath.
  e.preventDefault();
  e.stopPropagation();

  if (e.key === 'Escape') {
    finish();
    cbs?.onCancel();
    return;
  }
  if (e.key === 'Enter') {
    cbs?.onConfirm();
    return;
  }
  cbs?.onNav(nav);
}

export function startPicker(callbacks: Callbacks & { isTop: boolean }) {
  if (active || !document.body) return;
  cbs = callbacks;
  isTopFrame = callbacks.isTop;
  active = true;
  currentIndex = null;
  candidates = rankedVideos();
  buildUi();

  // Every frame listens, including ones with no video: key events only reach
  // the frame that holds focus, and that may well be an ad iframe.
  window.addEventListener('keydown', onKey, true);
  window.addEventListener('scroll', repaint, true);
  window.addEventListener('resize', repaint, true);

  callbacks.onCandidates(candidates.map((c) => ({ score: c.score, label: c.label })));
}

/**
 * Tear the picker down without reporting a cancellation.
 *
 * Every caller is the background telling us to stop (PICK_CANCEL, DETACH), so
 * it already knows. Firing `onCancel()` here sent a spurious PICK_ERROR that
 * cleared the action badge — including the badge just set for a pick made in
 * another frame. A user-initiated cancel still reports, from `onKey`.
 */
export function stopPicker() {
  finish();
}

function finish() {
  active = false;
  currentIndex = null;
  candidates = [];
  window.removeEventListener('keydown', onKey, true);
  window.removeEventListener('scroll', repaint, true);
  window.removeEventListener('resize', repaint, true);
  banner?.remove();
  highlight?.remove();
  label?.remove();
  highlight = label = banner = null;
}
