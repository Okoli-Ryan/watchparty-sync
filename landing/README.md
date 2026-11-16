# WatchParty Sync landing page

The public page for the extension: a Next.js app built as a static export (`output: 'export'`),
served by nginx on the VPS with the web dashboard ([deploy/web/DEPLOY.md](../deploy/web/DEPLOY.md)).

```bash
npm install
npm run dev      # http://localhost:3100
npm run build    # → out/ (plain HTML, CSS and JS)
npm start        # serves out/ on :3100, to check the export
```

It has its own `package.json` (React 19, Next 16) so it never touches the extension's
dependencies or its typecheck.

## How it is put together

- `app/page.tsx` is the page; each section is a component in `app/components/`.
- The look comes from the taste-skill design kit, copied into `public/assets/` as plain CSS and
  ES modules (`system.css`, `system.js`, the UI fragments, the scroll moment). `app/layout.tsx`
  links the stylesheets in the kit's order, then `public/palette.css` (the dealt palette and type)
  and `public/page.css` (the page's own rules).
- The kit's scripts change the DOM (pause buttons, loops, the nav sheet), so
  `components/KitScripts.tsx` starts them only after React has hydrated.
- Photographs in `public/assets/img/` are stand-ins from Wikimedia Commons (credited in
  `public/credits.html`) for the videos a room would be watching.
- `SITE_URL` at build time sets the share image's absolute address and the canonical link.

Don't edit files under `public/assets/` by hand: they are the kit's, and its checks compare
against them. Page changes go in `app/components/` and `public/page.css`.
