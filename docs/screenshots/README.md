# Public-demo screenshots

These fifteen viewport captures come exclusively from [the public PharmaFlow application](https://pharmaflow-oux0.onrender.com). They are real browser screenshots, not mockups. Each portfolio README uses five captures rendered by the application in its own language: `en/`, `es/`, or `ja/`. Captions and alternative text are localized too.

Every locale contains the same five scenarios: `home-desktop-dark.webp`, `catalog-desktop-light.webp`, `medication-detail-desktop-dark.webp`, `pharmacy-detail-mobile-light.webp`, and `favorites-mobile-dark.webp`. Medication detail and favorites use Paracetamol 500 mg; pharmacy detail uses Aster Commons. Routes, viewports, themes and semantic scroll anchors are shared. Text wrapping and locale-aware price/date formatting can change the exact scroll offset or visible extent.

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

To replace a scenario in all languages, or just one language, and update the corresponding manifest entries:

```sh
node scripts/capture-portfolio.cjs medication-detail
node scripts/capture-portfolio.cjs medication-detail ja
node scripts/capture-portfolio.cjs all es
```

Accepted scenario IDs: `home`, `catalog`, `medication-detail`, `pharmacy-detail`, `favorites`, or `all`. The optional second argument is `en`, `es`, or `ja`. No arguments generates all fifteen images.

## Capture contract

- Each image starts in a fresh browser context. Desktop viewports are 1440 × 900; mobile viewports are 390 × 844, all at device scale 1.
- Browser color-scheme preferences select the actual application theme. Every capture selects its language through the public language selector, confirms the DOM language/theme, then scrolls to the same semantic anchor. Reduced motion avoids capturing moving decoration mid-transition.
- Navigation, presentation selection, and adding a favorite use the public UI. The favorite is browser-local; it is not a database write.
- The script waits for API regions, fonts, and transitions; checks language/theme, nonempty expected content, no skeleton/busy state, no error/retry alert, no open listbox, and no horizontal overflow; then captures only the viewport.
- Scroll positions emphasize the Home hero, catalog cards/prices, all three branch observations, Japanese weekly hours, and the Spanish saved presentation. Scrolling never changes app content.
- Playwright produces an in-memory PNG. sharp converts it to **lossless WebP**, without cropping, resizing, compositing, or retouching. Full decode and dimension/format checks follow.
- No HTML, storage, or data injection, intercepted API responses, local data substitution, credentials, or direct database access is used.
- Contexts and browser are closed even on capture failure. No browser profile is retained.

`manifest.json` records all fifteen images with their locale-relative paths, public source, browser version, language/theme, dimensions, byte size, SHA-256, and capture time. Regeneration can change pixels as fonts/browser versions or the public application evolve. Review all fifteen resulting images visually before publishing; automated checks alone do not assess composition.

The images contain fictional demonstration catalog information, not medical advice or real pharmacy data.
