'use client';

import { useEffect } from 'react';

// The kit's runtime: nav sheet, card loops, the hero collage, the scroll moment, icons and grounds.
// These modules change the DOM (pause buttons, classes, measured sizes), so they start only after
// React has hydrated the page; added as plain module scripts, they stay outside the bundler.
const modules = ['/assets/system.js', '/assets/motion/motion.js', '/assets/layout/layout.js'];

export default function KitScripts() {
  useEffect(() => {
    const added = modules.map((src) => {
      const el = document.createElement('script');
      el.type = 'module';
      el.src = src;
      document.body.appendChild(el);
      return el;
    });
    return () => added.forEach((el) => el.remove());
  }, []);
  return null;
}
