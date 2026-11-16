import type { CSSProperties } from 'react';

export const REPO = 'https://github.com/Okoli-Ryan/watchparty-sync';
/** The main action, word for word in the nav, the hero and the close (copy.md rule 5). */
export const MAIN_ACTION = 'Get the extension';

/** Inline styles that set CSS custom properties (--ax, --at...) alongside ordinary ones. */
export const vars = (v: Record<string, string>) => v as CSSProperties;

export function Arrow() {
  return (
    <svg className="m-arrow" viewBox="0 0 12 12" aria-hidden="true">
      <path className="m-arrow__stem" d="M1 6h8" />
      <path className="m-arrow__head" d="M4.5 2.5 8 6l-3.5 3.5" />
    </svg>
  );
}

/** The kit's interface glyphs, drawn in the text colour. */
export const Glyph = {
  play: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <path d="M3 4h10v8H3z" />
      <path d="m7 6.5 2.5 1.5L7 9.5z" />
    </svg>
  ),
  person: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <circle cx="8" cy="6" r="2.5" />
      <path d="M3.5 13.5c.6-2.3 2.4-3.5 4.5-3.5s3.9 1.2 4.5 3.5" />
    </svg>
  ),
  lock: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <rect x="4" y="7" width="8" height="6" rx="1" />
      <path d="M5.5 7V5.5a2.5 2.5 0 0 1 5 0V7" />
    </svg>
  ),
  clock: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <circle cx="8" cy="8" r="5.5" />
      <path d="M8 5v3l2 1.5" />
    </svg>
  ),
  check: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <path d="m3.5 8.4 2.8 2.8 6.2-6.4" />
    </svg>
  ),
  search: (
    <svg className="ui-icon" viewBox="0 0 16 16" aria-hidden="true">
      <circle cx="7" cy="7" r="4.5" />
      <path d="m10.5 10.5 3 3" />
    </svg>
  ),
};
