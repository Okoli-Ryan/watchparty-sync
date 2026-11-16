import { ext } from '../shared/ext';

/**
 * The theme for what the extension draws ON other sites: the widget (a shadow root) and the
 * video picker (inline styles). Same values as src/shared/theme.css, which those can't read.
 */
export const T = {
  bg: '#f8f8f8',
  surface: '#eeeeee',
  card: '#ffffff',
  ink: '#0a0a0a',
  text: '#262626',
  muted: '#6a6a6a',
  line: 'rgb(10 10 10 / 0.1)',
  lineStrong: 'rgb(10 10 10 / 0.18)',
  accent: '#1f6b3a',
  accentHover: '#185530',
  accentSoft: 'rgb(31 107 58 / 0.1)',
  live: '#2f9a57',
  /** the bright green that marks a picked video on the landing page; reads on any footage */
  pick: '#7fd49a',
  danger: '#b42318',
  dangerSoft: 'rgb(180 35 24 / 0.08)',
  font: `'WP Urbanist', system-ui, -apple-system, 'Segoe UI', sans-serif`,
  mono: `ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace`,
  shadow: '0 1px 2px rgb(0 0 0 / 0.1), 0 22px 48px -22px rgb(0 0 0 / 0.45)',
  ease: 'cubic-bezier(0.2, 0.8, 0.2, 1)',
  spring: 'cubic-bezier(0.2, 1.3, 0.4, 1)',
} as const;

let fontsLoaded = false;

/**
 * Registers Urbanist for the widget. A shadow root can't declare fonts (Chrome ignores
 * @font-face inside one), so the face is declared once on the page under a name no site uses,
 * from the extension's own copy (manifest web_accessible_resources). A page whose CSP refuses
 * it simply gets the system font: nothing breaks.
 */
export function loadWidgetFont(): void {
  if (fontsLoaded || !document.head) return;
  fontsLoaded = true;
  const url = ext.runtime.getURL('fonts/urbanist-latin-wght-normal.woff2');
  const style = document.createElement('style');
  style.dataset.watchparty = 'font';
  style.textContent = `@font-face{font-family:'WP Urbanist';font-style:normal;font-display:swap;font-weight:100 900;src:url('${url}') format('woff2');}`;
  document.head.appendChild(style);
}
