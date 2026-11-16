import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import KitScripts from './components/KitScripts';

const title = 'WatchParty Sync: watch parties for any web video';

// The landing's public address (deploy-web.yml passes the LANDING_URL variable as SITE_URL at build).
// It resolves the share image to an absolute URL and sets the canonical link; unset, both stay local.
const siteUrl = process.env.SITE_URL?.trim() || undefined;

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl ?? 'http://localhost:3100'),
  ...(siteUrl ? { alternates: { canonical: '/' } } : {}),
  title,
  description:
    'A browser extension that keeps play, pause and seek in step for everyone in a room, on any page with an HTML5 video.',
  icons: { icon: { url: '/favicon.svg', type: 'image/svg+xml' } },
  openGraph: {
    type: 'website',
    title,
    description: "Pick a video on any page, open a room, and every viewer follows the host's play, pause and seek.",
    images: ['/share.jpg'],
  },
  twitter: { card: 'summary_large_image' },
  other: { 'site-kind': 'software', 'page-type': 'software' },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  viewportFit: 'cover',
  themeColor: '#f8f8f8',
};

// The design kit (public/assets, from the taste-skill) is plain CSS and ES modules. Its stylesheets
// are linked here in the kit's order, then the page's own CSS last. The two inline guards must run
// before the first paint: they hide the hero's words until the entrance runs (with a 700ms fallback)
// and mark the page as ready for its scroll moment. The kit's scripts load after React has hydrated
// (KitScripts), so React never sees the DOM they add.
const kitCss = [
  '/assets/system.css',
  '/assets/ui/cards.css',
  '/assets/motion/motion.css',
  '/assets/backgrounds/backgrounds.css',
  '/assets/layout/layout.css',
  '/assets/icons/icon.css',
];
const fragmentCss = ['base', 'window/fragment', 'chat/fragment', 'search/fragment', 'toggles/fragment', 'notify/fragment'];

const guards =
  "document.documentElement.classList.add('m-enter');setTimeout(()=>document.documentElement.classList.add('m-enter-done'),700);" +
  "document.documentElement.classList.add('mo-js');setTimeout(()=>document.documentElement.classList.add('mo-safe'),2500);";

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html
      lang="en"
      data-type="software"
      data-radius="round"
      data-ground="sage"
      data-buttons="tonal"
      data-motion=""
      suppressHydrationWarning
    >
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="" />
        <link
          rel="stylesheet"
          href="https://fonts.googleapis.com/css2?family=Urbanist:wght@400..700&family=DM+Mono:wght@400;500&display=swap"
        />
        {kitCss.map((href) => (
          <link key={href} rel="stylesheet" href={href} />
        ))}
        <link rel="stylesheet" href="/palette.css" />
        {fragmentCss.map((name) => (
          <link key={name} rel="stylesheet" href={`/assets/ui/${name}.css`} />
        ))}
        <link rel="stylesheet" href="/page.css" />
        <script dangerouslySetInnerHTML={{ __html: guards }} />
        <script src="/assets/backgrounds/backgrounds.js" />
        <link rel="modulepreload" href="/assets/moments/moments.js" />
        <link rel="modulepreload" href="/assets/moments/lib.js" />
        <link rel="modulepreload" href="/assets/moments/letters-assemble/moment.js" />
      </head>
      <body suppressHydrationWarning>
        {children}
        <KitScripts />
      </body>
    </html>
  );
}
