# Public-demo screenshots

These five viewport captures come exclusively from [the public PharmaFlow application](https://pharmaflow-oux0.onrender.com). They are real browser screenshots, not mockups. English desktop, Japanese mobile, and Spanish mobile views are shared by the three portfolio READMEs; captions and alternative text are localized there.

## Reproduce

The isolated [capture script](../../scripts/capture-portfolio.cjs) uses Playwright **1.62.1**, sharp **0.35.5**, Node.js, and an installed Google Chrome browser. These tools were already available in the authoring environment. No frontend dependency or lockfile was changed.

For an independent setup, install the screenshot tools outside the repository (PowerShell):

```powershell
$captureTools = Join-Path ([IO.Path]::GetTempPath()) 'pharmaflow-capture-tools'
npm install --prefix $captureTools --no-save --package-lock=false playwright@1.62.1 sharp@0.35.5
$env:NODE_PATH = Join-Path $captureTools 'node_modules'
node scripts/capture-portfolio.cjs
```

Run from the repository root with Node 24 and Chrome installed. Alternatively, set `PHARMAFLOW_CAPTURE_BROWSER=msedge` for installed Microsoft Edge. No Playwright browser download is required for those channels. The public demo must be reachable; allow time for a free-hosting cold start.

To replace only one image and its manifest entry:

```sh
node scripts/capture-portfolio.cjs medication-detail
```

Accepted IDs: `home`, `catalog`, `medication-detail`, `pharmacy-detail`, `favorites`.

## Capture contract

- Each image starts in a fresh browser context. Desktop viewports are 1440 × 900; mobile viewports are 390 × 844, all at device scale 1.
- Browser color-scheme preferences select the actual application theme. Language changes use the public language selector. Reduced motion avoids capturing moving decoration mid-transition.
- Navigation, presentation selection, and adding a favorite use the public UI. The favorite is browser-local; it is not a database write.
- The script waits for API regions, fonts, and transitions; checks language/theme, nonempty expected content, no skeleton/busy state, no error/retry alert, no open listbox, and no horizontal overflow; then captures only the viewport.
- Scroll positions emphasize the Home hero, catalog cards/prices, all three branch observations, Japanese weekly hours, and the Spanish saved presentation. Scrolling never changes app content.
- Playwright produces an in-memory PNG. sharp converts it to **lossless WebP**, without cropping, resizing, compositing, or retouching. Full decode and dimension/format checks follow.
- No HTML, storage, or data injection, intercepted API responses, local data substitution, credentials, or direct database access is used.
- Contexts and browser are closed even on capture failure. No browser profile is retained.

`manifest.json` records the public source, browser version, paths, language/theme, dimensions, byte size, SHA-256, and capture time. Regeneration can change pixels as fonts/browser versions or the public application evolve. Review all five resulting images visually before publishing; automated checks alone do not assess composition.

The images contain fictional demonstration catalog information, not medical advice or real pharmacy data.
