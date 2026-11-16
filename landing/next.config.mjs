import path from 'node:path';
import { fileURLToPath } from 'node:url';

/** @type {import('next').NextConfig} */
const nextConfig = {
  // This app has its own package.json and lockfile inside the extension's repo; build from here.
  turbopack: { root: path.dirname(fileURLToPath(import.meta.url)) },
  // A static site: `next build` writes plain HTML, CSS and JS to out/, which nginx serves on the VPS
  // (deploy/web). No Node server runs in production.
  output: 'export',
  images: { unoptimized: true },
  // The page's photographs and the design kit are plain files in public/; nothing to optimise at build.
  poweredByHeader: false,
};

export default nextConfig;
