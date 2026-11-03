---
name: competition-pitch-deck
description: >
  Plan, build, review and improve a competition pitch and its PowerPoint deck:
  narrative, judging-criteria fit, slide craft for a projected room, honest
  claims, speaker notes, Q&A prep and file QA. Use when asked for a pitch deck,
  presentation, slides, demo script or judging presentation for Kalyeah, or to
  review or improve an existing .pptx. Builds on the `pptx` skill for file
  mechanics; this skill owns the story and the quality bar.
argument-hint: "[build|review|improve] [deck.pptx]"
license: MIT
---

# Competition pitch deck

A pitch is won in the first 30 seconds and the last 30. Judges see many entries in a row: make yours easy to remember, easy to score and impossible to doubt.

Project rules still bind: `CLAUDE.md` first. **Use project assets only** (`assets/`); never generate art (owner rule: advise which kind of asset is missing instead). Inspired-by, never-copied applies to anything shown to judges, fonts and brand names on screenshots included. For the .pptx mechanics (pptxgenjs, validate, render) follow the `pptx` skill; do not duplicate it here.

## 0. Gather before designing (ask only what is missing)

| Need | Default if unknown |
|---|---|
| Competition name, **judging criteria and weights** | Innovation, gameplay, technical quality, art/culture, polish, mobile fit; say so in the notes |
| Time slot, live or pre-read, Q&A length | 5 min live + 3 min Q&A, about 8 to 10 core slides |
| Audience (game devs, mixed panel, sponsors) | Mixed panel: no jargon without a picture |
| What can be demoed live (APK, video, nothing) | Assume the APK works but plan a no-demo path |
| Team and studio line | Leave out; ask. Never invent names or roles |

## 1. Story: one spine, nine beats

1. **Hook**: the one sentence a judge repeats to a colleague (what it is, for whom, why now).
2. **Opportunity**: the human truth behind the game (not a market-size number you cannot source).
3. **Solution in one picture**: the core loop, shown, not described.
4. **How it plays**: three or four verbs, each with a visual.
5. **What is new**: the differentiators, each tied to a judging criterion.
6. **Proof**: it exists and works. Builds, tests, numbers, screenshots, a QR to try it. Real beats claimed.
7. **Honest status and next steps**: built / being verified / next. Judges trust a team that names its gaps.
8. **The ask or call to action**: one thing (try the build, give feedback, back the next milestone).
9. **Close on the logo and the hook line.**

Cut any slide that does not move a beat. Time rule: about 1 to 1.5 minutes per core slide; put detail in an **appendix** (backup numbers, controls, architecture) used only in Q&A.

## 2. Slide craft (projected in a room)

- **Action titles**: a claim a judge can agree with ("Take their post, or tag them all"), not a topic ("Gameplay"). Same position, size and alignment on every content slide.
- **One message per slide.** Three supports at most. If a slide needs a paragraph, it is a leave-behind, not a pitch.
- **Image first.** Every slide has real project art (key art, cards, kids, props, diorama). No text-only slides, no stock, no clip art. Icons only as small helpers.
- **Legibility at 5 m**: titles 28 pt+, body **18 pt+**, captions 12 pt+; contrast at least 4.5:1; never light text on busy art without a plate. Short lines (about 6 words per bullet, 2 lines per card).
- **One palette, one motif.** Take colours from the logo and key art (deep blue, gold, cardboard brown); repeat one framing motif (rounded white plates). No accent stripes, no underlines under titles, no gradients faked with shapes.
- **Fonts**: a display font for titles only (owner's choice wins), a safe body font (Calibri/Arial). A .pptx does not embed fonts: tell the owner to install the font on the presenting machine and export a PDF backup. Wide fonts need smaller sizes and shorter titles (about 40 characters at 30 pt).
- **Charts and numbers**: big number plus a short label beats a table. Only numbers that exist in `docs/GDD.md`, `data/` or a measured run.
- **Cover and close** are dark and image-led; content slides may alternate light and dark but never leave a slide half empty.

## 3. Honesty rules (non-negotiable)

- Every claim traces to a file (`docs/`, `data/`, `tests/`, a workflow run). No invented stats, user counts, downloads, testimonials or "players love it".
- Say what was not verified: phone feel, performance, real-network Hotspot, release signing.
- Check requirement claims against the build, not the docs alone (for example the minimum Android version).
- Third-party marks inside assets (a real food brand on a prop, a font from another game company) are a risk in front of judges: flag them, do not hide them.
- Mark placeholders that need the owner (team names, criteria, demo link) in the speaker notes, never as fake content on a slide.

## 4. Speaker notes and Q&A

- Notes on every slide: the line to say, the transition to the next slide, and a time cue ("0:45").
- A **Q&A sheet** (notes of the appendix slide or a short section): the five hardest likely questions with a two-sentence answer each (balance, networking, cheating, scope, what is not done yet).
- Backup path if the demo fails: the deck alone must carry the story.

## 5. Review rubric (score 0 to 5, fix anything under 4)

| # | Check | Pass looks like |
|---|---|---|
| 1 | Hook | One-sentence pitch on slide 1 |
| 2 | Narrative | Beats 1 to 9 present, in order, no filler slide |
| 3 | Criteria fit | Each judging criterion has a slide or a named proof |
| 4 | Proof | Something the judge can try or verify (QR, build, test, numbers) |
| 5 | Titles | All action titles, consistent position |
| 6 | Legibility | Body 18 pt+, no overflow, contrast fine |
| 7 | Visuals | Real project art on every slide, one motif |
| 8 | Honesty | Every claim sourced, gaps named |
| 9 | Notes and Q&A | Talk track, timings, hard questions |
| 10 | File QA | Validates, rendered and read slide by slide, fonts checked, PDF backup |

## 6. Workflow

1. **Review**: read the deck text (`slide_text.py`) and render it; score the rubric; list findings ranked by judge impact.
2. **Improve**: change the generator script (not hand-edited XML), keep one script as the source, rebuild.
3. **Verify**: validate the file, render every slide with the display font installed, read each slide image, re-score.
4. **Hand over**: say what changed, what still needs the owner (criteria, team slide, demo, font install) and what was not checked (PowerPoint rendering, projector).
5. Commit only the deck and, if requested, its generator; stage explicit paths. Do not commit fonts.
