import { REPO } from './shared';

// faq: every answer open, on the page's split (sections-library.md). The GitHub mark beside the
// link is the section's visual: questions go to the project's issues there.
const questions: [string, string][] = [
  ['What does the extension cost?', 'Nothing. It is open source under the MIT licence; you run the API.'],
  ['Which video sites does it work on?', 'Any page with an HTML5 video, iframes included. Copy-protected players may refuse to seek.'],
  ['Does everyone in a room need an account?', 'Yes: a name, an email and a password of 8 characters or more.'],
  ['What happens to a room when the host leaves?', 'It passes to the next viewer. When nobody is left, the room closes.'],
  ['Is Firefox set up differently?', 'Firefox asks once for site access. Chrome and Edge grant it at install.'],
];

export default function Faq() {
  return (
    <section className="section" id="faq" aria-labelledby="faq-h">
      <div className="wrap">
        <div className="grid grid--top">
          <div className="span-5 stack-4">
            <h2 id="faq-h">Questions</h2>
            <p>About accounts, video sites and what happens when someone leaves.</p>
            <a className="faq-ask" href={`${REPO}/issues`}>
              <img className="ic ic-40 ic-ink" src="/assets/icons/github.svg" alt="" width={40} height={39} />
              <span className="link">Ask on GitHub</span>
            </a>
          </div>
          <div className="faq span-6 start-7">
            {questions.map(([q, a]) => (
              <div key={q} className="faq__item"><h3>{q}</h3><p>{a}</p></div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
