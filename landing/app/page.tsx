import Closing, { ActionBar } from './components/Closing';
import Faq from './components/Faq';
import Features from './components/Features';
import Hero from './components/Hero';
import Nav from './components/Nav';
import Rooms from './components/Rooms';
import Steps from './components/Steps';
import Sync from './components/Sync';

/*
  Built with the taste-skill (design-taste-frontend). Plan:
  type: software (a browser extension with its own API)
  deal: hero triptych, nav nav-centre, close close-word, footer footer-columns, rhythm space, buttons tonal,
        mark wordmark, palette mint-pink-accounts (off-white ground, one green accent: the interface stays neutral
        so the videos carry the colour), type urbanist (funnel and instrument failed hero-lines), radius round, ground sage,
        card ground grain-gradient
  moments: letters-assemble in #sync
  screens: hero 1, rooms 1 (app-frame), steps 1 (2 from 640 to 1279), sync 1, features 2, faq 1, closing 1
*/
export default function Home() {
  return (
    <>
      <Nav />
      <main id="top">
        <Hero />
        <Rooms />
        <Steps />
        <Sync />
        <Features />
        <Faq />
      </main>
      <Closing />
      <ActionBar />
    </>
  );
}
