# PharmaFlow

An independent, fictional portfolio project for public medication discovery and branch availability comparison. All implementation, visual identity, and copy are original; no previous-employer material is used.

## Current status

Phase A — catalog discovery, medication detail, branch comparison, pharmacy directory/detail, and browser-local favorites. Home search and `/catalog` support name/ingredient discovery, branch/status/form filters, reference-price sorting, and responsive presentation cards backed by nine fictional entries. Exact presentation detail and comparison across all three branches are implemented at `/medications/:slug`. `/pharmacies` lists the three fictional branches; `/pharmacies/:slug` shows their imaginary locations, complete weekly hours, and a link to the branch-filtered catalog. `/favorites` provides local bookmarks with individual removal and confirmed clear-all. The shell and themes now support complete English, Spanish, and Japanese interfaces. The integration review has run; Phase A remains pending the manual accessibility checks described below.

PRODUCT_PLAN.md is the approved product source of truth. The frontend lives in `frontend/` so future backend work can be added without moving it.

### Phase A review — October 5, 2026

At the October 5 review, typecheck, lint, formatting, all 120 tests, and the production build passed. The review corrected bookmark buttons so their visible Save/Remove text is contained in their presentation-specific accessible names; the shared-state test protects both states.

Local Edge browser checks cover the integrated journeys, URL context and history, direct routes, unknown records, missing observations/prices, bookmark persistence and cross-tab synchronization, storage recovery, keyboard focus, mobile navigation, and reduced motion. Automated layout checks cover all six main routes at 320, 360, 768, and 1280 CSS pixels in both themes without page-level horizontal overflow. Representative screenshots were visually inspected. Audited text/status token combinations exceed 4.5:1 contrast; focus colors exceed 3:1 against the main surfaces. Production direct routes and theme persistence pass without console warnings, failed resources, or external requests.

**Phase A is not yet ready to close.** PRODUCT_PLAN.md section 15 requires manual native browser zoom and a real screen-reader/browser check. CSS 200% zoom reflow and an inspected browser accessibility tree passed, but do not substitute for those checks. Complete native 200% zoom and the manual width/theme walkthrough, then verify catalog results, branch comparison, bookmark announcements, and removal focus with a real screen reader before declaring closure. No accessibility certification is claimed.

The canonical nine-presentation dataset covers the required functional distinctions. Expansion toward approximately 24 presentations is optional portfolio enrichment, not a closure blocker: the plan describes an approximate cap, and the approved review scope explicitly retains nine. The plan and dataset were preserved. Phase B has not begun and requires separate authorization.

## Final multilingual experience — October 8, 2026

This increment audited and refined the existing English, Spanish and Japanese implementation. It retains i18next/react-i18next, the native language selector, typed asynchronous local readers, narrow Favorites state/storage boundary, approved artwork, themes, navigation and carousel. No packages, projects, backend, database or deployment were added.

### Locale and domain decisions

English remains the default and fallback. The selector displays **English / Español / 日本語**, without flags, and persists only a supported locale in the separate `pharmaflow:locale` preference. Invalid values and inaccessible storage fall back safely to English; a blocked write still permits a language change in memory. Switching language preserves the route, query/filter values, theme and saved presentation IDs. `html lang` and document titles follow the selected language. Valid medication and branch titles now include translated detail context as well as the canonical name.

Medication/ingredient names, branch proper names, strength units, IDs, slugs, query semantics and dataset values intentionally remain canonical. Surrounding interface text, form labels, package descriptions, fictional location descriptions, disclosures and accessible messages are authored explicitly in all three resources. Search uses the existing canonical medication/ingredient strings with deterministic case/diacritic normalization in every locale; there are no translated medical names, aliases, automatic translation or transliteration. Future translated domain names require a reviewed stable-ID overlay and matching search tests.

Reference prices use locale-aware formatting with the original **USD** currency; localization does not convert currency. Observation timestamps remain fixed and display in **UTC**. Weekly schedules preserve seven days, closed days and split intervals, with localized weekday/hour labels; they do not calculate live opening status. Missing observations remain distinct from unavailable stock, absent prices and absent update timestamps.

### Copy, Japanese typography and completeness

Spanish copy was refined for medication discovery, branch availability, fictional stock disclaimers and opening hours. Japanese copy now consistently describes packaging as 包装, pharmaceutical forms as 剤形, capsules as カプセル剤 and opening hours as 営業時間. Favorite announcements and failure messages were made natural and describe retained bookmarks without exposing implementation IDs. English recovery copy received the same clarification.

The Japanese hero previously left an isolated `く。` line on desktop. Its accent now reads `ひと目でわかる。`, with balanced wrapping, normal overflow wrapping, zero heading tracking and a Japanese line height; the approved heading size and composition are preserved. Japanese uses strict line breaking and explicit installed-system fallbacks (`Hiragino Kaku Gothic ProN`, Meiryo, Yu Gothic) after the existing Latin system stack. Windows Edge rendered the Japanese heading with Meiryo Bold during inspection. No downloaded font or network font request was introduced. Glyphs, punctuation, controls and wrapping were reviewed in rendered Spanish/Japanese screens; this is not native-speaker or screen-reader certification.

The source audit found only intentional untranslated autonyms, the UTC zone label and empty decorative image alternatives. Canonical names/units are the documented domain exceptions. An unused placeholder page and its obsolete planned-feature messages were removed; the live unknown-route recovery action now belongs to the `notFound` resource. Recursive locale key, nonempty value and interpolation parity checks remain, and an added TypeScript AST audit verifies statically referenced UI/accessibility messages in every locale. Resource structure is also checked against English at compile time. Dynamic domain overlays retain their existing coverage.

### Verification and limitations

The actual pre-edit baseline was **156 passing tests in 17 files**. Eleven tests were added for translated Home/navigation/carousel labels, announcements, canonical normalized search, real rendered price/date/missing-data states, branch title/schedule/filter links, blocked storage access and static resource references. Three existing detail-title expectations were updated for the added localized context. The final suite passes **167 tests in 17 files**, without reducing previous coverage. Typecheck, lint, formatting check and production build pass. Production output is **426.68 kB JavaScript / 129.96 kB gzip** and **36.40 kB CSS / 7.81 kB gzip**.

Browser evidence against the final multilingual version:

- **264** route/state cases: all three locales, both themes and 320/360/768/1280 CSS pixels; Home, Catalog, medication detail, pharmacy directory/detail, populated/empty Favorites, empty/filtered catalog and unknown records. No page overflow, detected untranslated English UI, runtime warnings/errors or external requests.
- **144** loading/error/retry cases: six routes, all three locales, both themes and 360/1280 pixels; localized messages, successful recovery and saved-ID retention. Fault injection exists only in the temporary browser verification scripts.
- Responsive navigation checked in all three locales at 320/360/768/959/960/1024/1280 pixels: no brand/navigation/utility overlap; full desktop navigation begins at 960px, with 44px-high targets.
- Locale changes/reloads preserve filtered URLs, theme and Favorites; native selector/mobile menu keyboard handling and Escape/focus restoration pass. A real browser with a blocked localStorage getter safely defaults to English, supports in-memory Spanish/Japanese changes and returns to English after reload.
- **12 complete journeys** independently cover English/Spanish/Japanese × light/dark × 360/1280 pixels: Home search with mixed case/accents, medication detail, keyboard Save, branch detail, branch-filtered catalog, Favorites reload and keyboard removal. Japanese headings use installed Meiryo Bold in both themes.
- Production direct routes cover the shell, all nine medication slugs, all three branch slugs and unknown routes, plus persisted theme changes; no console warnings/errors, failed resources or external requests.

Settled Spanish/Japanese screenshots were visually inspected for Home, Catalog, medication detail, pharmacy directory, branch detail and Favorites, including desktop and mobile samples and both themes. The Japanese hero was separately inspected at 320, 360 and 1280 pixels. Artwork, navigation, carousel semantics, motion controls and the approved color identity were preserved.

**Phase A remains open.** Human native browser zoom at 200%, the remaining final visual walkthrough and a real screen-reader/browser check must be performed against this multilingual version. Include localized selector/title announcements, catalog results, branch comparison, saved/removed announcements and removal focus in that check. Automated browser checks and screenshot inspection do not replace these closure requirements. No new implementation blocker was identified; Phase B still requires separate authorization.

### Exact multilingual increment

No files were created. Modified: `AGENTS.md`, `README.md`, `frontend/src/features/preferences/locale.test.ts`, `frontend/src/i18n/{en.ts,es.json,index.ts,ja.json,Localization.test.tsx,resources.test.ts}`, `frontend/src/pages/{HomePage.module.css,MedicationDetailPage.tsx,MedicationDetailPage.test.tsx,NotFoundPage.tsx,PharmacyDetailPage.tsx,PharmacyPages.test.tsx}` and `frontend/src/styles/{global.css,tokens.css}`. Deleted: the unreachable `frontend/src/pages/PlaceholderPage.tsx`. `AGENTS.md` records the domain-name, USD/UTC, title-context and Japanese-rendering requirements for future changes.

`PRODUCT_PLAN.md`, canonical `demo-data/catalog.json`, package/lock files and all artwork are byte-for-byte unchanged from the pre-edit inventory. Plan SHA-256: `5eb9636e57718ce949f1a9650e71608a9f7e1742d5e4fb9f285bfaf882696532`; dataset SHA-256: `4b20e4a739b5014cd590f14c4633e592b200af7ed8f8b9feac7f4598aaeecbb4`.

Read-only Git status remains exactly:

```text
?? .gitignore
?? AGENTS.md
?? PRODUCT_PLAN.md
?? README.md
?? demo-data/
?? frontend/
```

No Git initialization, staging, commit, push or publication was performed.

## Targeted visual polish — October 8, 2026

This second pass preserves the approved Home composition, typography, navy/blue/cyan/violet identity and all Phase A workflows. The pre-edit audit covered Home, shared artwork, carousel, header, forms, branches, theme/motion tokens, responsive breakpoints, locale resources, tests and existing assets. The public asset directory contained only the original favicon, with no true 3D render available. The primary desktop issue was confirmed at 1024px: a 165px header with a Menu button and a second utility row.

### Visual changes

- **Navigation:** the compact horizontal header now begins at 960px (60rem), instead of 1152px. Home, Catalog, Pharmacies, Favorites/count, the native language selector and the theme button are directly available. The measured desktop header including its top spacing is 84px at 960/1024/1280px in all three languages. The visible language label becomes visually hidden at this breakpoint while retaining the select's accessible name; the selector still displays its language autonym. Below 960px, the existing menu and Escape/focus restoration remain. A short animated active-route underline does not change link dimensions. The floating surface has restrained blur and elevation.
- **Hero:** `HeroArtwork` separates the orbit/background SVG, soft ground shadow and central asset. The capsule has cylindrical multi-stop shading, a curved seam, directional reflections and rim lighting. Front/back translucent ribbon strokes, glossy nodes and contact shadows improve overlap and perceived volume. Home text, search, CTAs and callouts stay in their existing positions and use their existing resources.
- **Depth/motion:** capable pointers produce a 5px central lift, a small CSS perspective tilt, separate ribbon/node offsets and shadow displacement using the shared 480ms depth token. This is bounded hover feedback, with no JavaScript animation loop, cursor tracking, continuous rotation or autoplay. Reduced motion suppresses these transforms and all transitions/entrances; native carousel commands remain instant. Mobile orbit opacity is reduced and the central plane is slightly enlarged within its reserved frame.
- **Medication vocabulary:** shared Carton, Blister, Bottle, Capsule and Tablets primitives form eight compositions: blister/carton; tablets/blister; horizontal carton with blister; upright carton with tablets; tablet bottle; bottle/carton; bottle only; capsules/carton. A small visual-only assignment by stable medication ID is shared by Catalog, Home, Favorites and Detail. Unknown IDs use the existing pharmaceutical form fallback. The two capsule presentations intentionally share a composition. Artwork does not encode actual packaging, package quantity, manufacturers or clinical meaning; canonical text identifies every presentation.
- **Branches:** Aster is a wider horizontal blue storefront with a canopy; Willow is a taller violet building with two window levels and a distinct entrance; Lantern has an asymmetric cyan corner plan with a recessed entrance. These original conceptual buildings are shared by Home, Directory and Detail. No location, schedule or dataset changes accompany them.
- **Carousel:** the scrollbar is visually hidden with `scrollbar-width: none`, the WebKit scrollbar pseudo-element and the legacy Microsoft overflow style. Native overflow, snapping, touch/trackpad scrolling, list semantics, endpoint controls, Left/Right/Home/End navigation and nested control focus are preserved. The partially visible next card, instructions and arrow buttons provide discovery cues without overlaying content.
- **Cards/controls:** existing medication card elevation remains, with a small 1.025x artwork scale and 3px artwork lift on eligible pointers. Form and Home branch cards gain shadow/border feedback with 4px lift. Price/status content has no independent motion. Buttons gain a small hover lift, icon feedback, restrained surface illumination and pressed feedback. Essential controls remain available without hover.
- **Themes/continuity:** light surfaces retain the same colors and gain two-layer elevation shadows. Home section lighting extends subtly into the spacing, with short connecting rules; spacing and information architecture are preserved. Dark palette/text/status tokens and dark shadow strengths remain unchanged, with the same restrained depth treatment. Glass remains confined to the header/callouts; information cards remain solid.

### Future central render slot

The delivered centerpiece remains original **SVG/CSS**, with simulated depth rather than a photorealistic 3D render. No raster, WebGL runtime or third-party artwork was introduced. `HeroArtwork` accepts an optional `renderSrc`; when supplied it replaces only the central SVG with a decorative, asynchronously decoded image. Orbits, ground shadow, frame and Home callouts remain independent.

Recommended future asset: a locally hosted transparent WebP or AVIF at **1280 × 1200 pixels, 16:15**, intended for the existing 640 × 600 scene and approximately 600px maximum displayed width. Keep the object centered, preserve transparent space for callouts, use cool directional blue/cyan/violet lighting, and omit text, logos, real packaging, orbit lines and callouts. Avoid baking in an opaque backdrop or a second ground shadow. Review alpha edges in both themes and at 320px; optimize the asset before adoption, targeting approximately 200 kB or less if visual quality permits. A later render needs visual approval and a new loading/performance review; no such asset is loaded now.

### Verification for this pass

All **156 existing tests in 17 files** pass unchanged. No tests were added or weakened: this pass changes decorative composition/CSS, and robust breakpoint/motion checks were performed in the browser rather than testing exact CSS values in jsdom. Typecheck, lint, formatting check and production build pass. No dependencies or packages were added, and package/lock files and all locale resources are unchanged.

| Browser check | Result |
| --- | --- |
| EN/ES/JA layouts and journeys | 264 route/state × width × theme × locale combinations at 320/360/768/1280; no overflow, untranslated English UI detected, runtime warnings/errors or external requests; existing IDs, queries, persistence and translated workflows preserved |
| Desktop collapse and fit | All three locales at 320/360/768/959/960/1024/1280; menu below 960, full navigation above, no brand/nav/utility overlap; desktop link targets at least 44px high |
| Pending/error/retry | 96 browser-controlled cases across six routes, four widths and two themes; recovery and saved IDs retained; no production fault controls added |
| Carousel and keyboard | Actual native scrolling/endpoints, all 18 nested presentation actions reached and fully visible using Tab, emulated touch swipe, skip link, menu Escape/focus restoration, route focus and history/query preservation |
| Motion/focus | Independent hero hover planes verified, static reduced-motion hero, animations/transitions suppressed, carousel instant scrolling, visible keyboard focus; representative hover/focus screenshots inspected |
| Favorites | Actual two-tab synchronization, reload, individual removal focus, clear/cancel, unrelated storage preservation, malformed/unsupported storage and memory-only recovery |
| Contrast | Audited ordinary text/status pairs at least 4.5:1 and focus/input boundaries at least 3:1 against their audited surfaces in both themes |
| Production | Direct shell routes, all nine medication slugs, all three branch slugs and unknown routes; theme persistence; no console warnings/errors, failed resources or external requests |

Settled screenshots were visually inspected for Home in both themes on desktop/mobile, Catalog in both desktop themes and mobile, Medication Detail dark desktop/mobile, Pharmacy Directory in both desktop themes, populated/empty Favorites, header/menu, carousel, hover and keyboard focus. The matrix is automated browser evidence; it does not replace a human native zoom or screen-reader check. Two initial browser runs timed out without a reproducible route failure; both complete diagnostic reruns passed without application changes.

Production output is approximately **428.4 kB JavaScript / 130.4 kB gzip** and **36.2 kB CSS / 7.7 kB gzip**, compared with 420.0/128.4 and 32.6/7.1 before this pass. Combined compressed growth is approximately **2.6 kB (1.9%)**, attributable to the original reusable SVG vocabulary and CSS depth/navigation treatment. No device performance claim is made.

### Exact increment and preserved scope

Created:

- `frontend/src/components/HeroArtwork.tsx`
- `frontend/src/components/HeroArtwork.module.css`

Changed:

- `README.md`
- `frontend/src/components/DiscoveryCarousel.module.css`
- `frontend/src/components/PharmaArtwork.tsx`
- `frontend/src/components/PharmaArtwork.module.css`
- `frontend/src/features/catalog/MedicationCard.tsx`
- `frontend/src/layouts/AppLayout.module.css`
- `frontend/src/pages/HomePage.tsx`
- `frontend/src/pages/HomePage.module.css`
- `frontend/src/pages/MedicationDetailPage.tsx`
- `frontend/src/styles/global.css`
- `frontend/src/styles/tokens.css`

No files were deleted. PRODUCT_PLAN.md and demo-data/catalog.json retain SHA-256 hashes `5EB9636E57718CE949F1A9650E71608A9F7E1742D5E4FB9F285BFAF882696532` and `4B20E4A739B5014CD590F14C4633E592B200AF7ED8F8B9FEAC7F4598AAEECBB4`. The canonical nine presentations, three branches, 21 observations and snapshot timestamps are unchanged. Reader/domain/query/Favorites storage boundaries, i18n resources, routing, package files and tests are unchanged. No backend, database, Docker, deployment or later-phase work was performed.

Command-local Git status remains exactly:

```text
?? .gitignore
?? AGENTS.md
?? PRODUCT_PLAN.md
?? README.md
?? demo-data/
?? frontend/
```

No commit, push, remote creation or global Git configuration change was made. Remaining visual limitation: vector-simulated materials cannot reproduce a true rendered 3D asset; some presentations intentionally share art. Human approval should cover desktop navigation density, hero material/depth, medication/branch variety, hover restraint and both themes. The next step is that visual approval and the existing native 200% zoom and real screen-reader/browser checks before Phase A closure. This polish pass stops here; Phase B still requires separate authorization.

## Visual redesign — October 6, 2026

The authorized visual redesign replaces the earlier teal direction with midnight navy, electric blue, cyan and restrained violet, translated into cool off-white and pale blue surfaces in light mode. PRODUCT_PLAN.md is preserved: this request supersedes its original visual defaults only, without changing the approved product scope or target architecture. The existing English, Spanish and Japanese implementation is preserved; new interface messages use all three resource files with key/interpolation parity.

Home now combines a search-led introduction with an original capsule/ribbon composition, a medication discovery carousel, links by canonical pharmaceutical form, a fictional branch showcase and a local Favorites entry. Home reads through the existing asynchronous `CatalogReader.discover` operation; forms, branch identities, presentations, prices and summaries are never maintained in a second dataset. Form links use the existing catalog query serializer. Catalog and Favorites reuse the same MedicationCard; detail and directory pages retain their original data and state boundaries.

#### Visual primitives and asset ownership

- `PharmaArtwork`: original inline SVG geometry for the hero, tablets/blister packaging, capsules, solution bottles and fictional branch buildings. Gradients, highlights and layered silhouettes create a 3D-like appearance without WebGL or a rendering library. Branch accents vary by stable ID. Text/data identify each presentation; artwork is explicitly illustrative and hidden from assistive technology.
- `DiscoveryCarousel`: native horizontal overflow and scroll snap, visible previous/next buttons, named list and keyboard instructions, Left/Right/Home/End navigation when the list itself is focused, and normal nested link/button behavior. Focus entering a partially visible card brings that card into view without moving focus. Controls remain focusable at the ends and expose `aria-disabled`. There is no autoplay, infinite looping, hidden duplicate slide set or carousel dependency.
- `StateArtwork` and `LoadingMark`: decorative shared marks beside meaningful empty, error, missing and loading text. Read failures remain separate from stock unavailability, missing observations and storage limitations.
- Semantic tokens cover background/elevation/glass/interactive surfaces, text, borders, primary/hover/focus, status pairs, illustration pigments, shadows, radii and motion. The original favicon and browser accent now use the same electric blue.

All new illustration geometry is authored within this project. No commercial photography, package designs, manufacturer logos, real pharmacy imagery or remote assets are used. The SVG components have explicit dimensions and aspect ratios, no separate image downloads and no missing-image requests. Tablet is the decorative fallback for an unknown form; the canonical textual form still displays. A future approved raster render can replace the visual inside the same reserved frame without changing domain data or route components. No raster asset or image-loading library is needed now.

Motion uses brief entrances, control feedback, bookmark feedback and small hover transforms on capable pointers. No animation runs indefinitely. Reduced-motion CSS disables animations/transitions; carousel commands scroll instantly and decorative hover transforms are suppressed. The header remains in document flow, with a compact icon-only theme control at the narrowest width. On mobile/tablet, detail illustrations use a shallower frame to keep information nearby. All functionality remains available without hover.

#### Verification evidence and limits

The final suite contains **156 passing tests in 17 files**: all 144 pre-existing tests remain unchanged and pass, with 7 new carousel tests and 5 Home integration tests. New coverage includes endpoints, accessible control relationships, nested-control keyboard behavior, reduced motion, resize behavior, visibility of focused cards, canonical discovery/form navigation, pending/error/retry and empty Home reads. Typecheck, lint, formatting and production build pass.

Detail-page document titles synchronize with the resolved heading before paint, avoiding a timing race between the visible presentation/branch and the browser tab title.

Browser checks use local Edge with the existing bundled automation runtime; no browser-testing package was installed in the project. The review includes:

| Check | Coverage and result |
| --- | --- |
| Existing languages/layouts | 264 combinations: English/Spanish/Japanese × light/dark × 320/360/768/1280 × 11 route/state cases; no page overflow, untranslated resource keys or runtime warnings/errors |
| Final main-route layouts | Home, Catalog, Medication Detail, Pharmacy Directory, Pharmacy Detail and populated Favorites at all four widths in both themes; one h1, labeled controls, at least 44px control heights, no page overflow |
| Pending and reader failure | 96 controlled states: six routes × four widths × two themes × loading/error, followed by successful recovery; stubs existed only in the test browser |
| Carousel | Actual native scrolling, end controls, all 18 presentation links/bookmark buttons reachable and fully visible via Tab, mouse hover, emulated touchscreen swipe and reduced-motion instant movement |
| Integrated workflows | Home search, combined catalog filters/sorting, reset, preserved detail-return query, back/forward history, branch-filtered discovery, skip link, route focus and mobile menu Escape/focus restoration |
| Favorites | Actual two-tab synchronization, reload persistence, removal/clear confirmation focus, unrelated storage preservation, malformed/unsupported recovery, blocked storage and quota fallback |
| Contrast | Audited text/status pairs ≥4.5:1; focus and input/select boundary pairs ≥3:1 against their audited surfaces in both themes |
| Production | Direct shell routes, all nine medication slugs, all three branch slugs and unknown routes; theme persistence; no console warnings/errors, failed resources or external requests |

Representative settled screenshots of all six main experiences and shared states were visually inspected across desktop, tablet and narrow mobile layouts. The full width/theme matrix is automated browser evidence, not a claim of a completed human assistive-technology audit. Native browser 200% zoom, the user's final visual walkthrough and a real screen-reader/browser review remain required before Phase A closure. No accessibility certification or field performance claim is made.

The production build is approximately 420 kB JavaScript / 128.4 kB gzip and 32.6 kB CSS / 7.1 kB gzip. These are bundle measurements, not device load-time measurements. No dependencies, remote fonts, animation runtime, video or WebGL renderer were added. The intentional visual compromise is reusable vector-based depth rather than photorealistic 3D; illustrations are shared by form so they do not suggest real product packaging. Only medication discovery uses a carousel; three form links remain directly visible as tiles.

PRODUCT_PLAN.md and demo-data/catalog.json retain their pre-redesign SHA-256 hashes, respectively `5EB9636E57718CE949F1A9650E71608A9F7E1742D5E4FB9F285BFAF882696532` and `4B20E4A739B5014CD590F14C4633E592B200AF7ED8F8B9FEAC7F4598AAEECBB4`. The dataset remains nine presentations, three fictional branches and 21 observations. No backend, database, Docker, authentication, commerce, deployment, commit, push, remote creation or global Git configuration change was made. Visual approval should focus on the hero composition, illustration style and theme palette. The next step is that review and the pending manual Phase A accessibility checks; later phases need separate authorization.

#### Files in this visual increment

New files under `frontend/src/`:

- `components/DiscoveryCarousel.tsx`, `DiscoveryCarousel.module.css`, `DiscoveryCarousel.test.tsx`.
- `components/PharmaArtwork.tsx`, `PharmaArtwork.module.css`.
- `components/StateArtwork.tsx`, `StateArtwork.module.css`.
- `components/LoadingMark.tsx`, `LoadingMark.module.css`.
- `pages/HomePage.test.tsx`.

Updated files:

- `README.md`, `frontend/index.html`, `frontend/public/favicon.svg`.
- `frontend/src/styles/tokens.css`, `global.css`.
- `frontend/src/layouts/AppLayout.module.css`.
- `frontend/src/components/LanguageSelector.module.css`, `SearchField.module.css`.
- `frontend/src/pages/HomePage.tsx`, `HomePage.module.css`, `CatalogPage.tsx`, `MedicationDetailPage.tsx`, `MedicationDetailPage.module.css`, `PharmacyDirectoryPage.tsx`, `PharmacyDetailPage.tsx`, `FavoritesPage.tsx`, `NotFoundPage.tsx`.
- `frontend/src/features/catalog/MedicationCard.tsx`, `Catalog.module.css`, `AvailabilityBadge.module.css`.
- `frontend/src/features/availability/BranchComparison.module.css`, `features/pharmacies/Pharmacies.module.css`, `features/favorites/Favorites.module.css`.
- `frontend/src/i18n/en.ts`, `es.json`, `ja.json`.

## Stack and target architecture

Current: React, TypeScript, Vite, React Router, CSS Modules, ESLint, and Prettier. All three interface languages use the existing i18next/react-i18next foundation. Generic UI icons use Lucide.

Target: React + TypeScript + Vite → ASP.NET Core Web API (C#) → PostgreSQL. Phase A uses local data; B introduces an in-memory API, C adds PostgreSQL, D connects the frontend, and E completes full-stack verification/documentation. No backend, database, or Docker setup exists yet.

## Interface languages

English (`en`), Español (`es`), and 日本語 (`ja`) cover the existing workflows, states, disclosures, accessibility names, and announcements. English is the default and fallback; browser/system language is not used to choose another language automatically. The labeled native selector changes the interface and document language in place, preserving the current route/query, favorites, theme, and stable medication/branch identity. Its autonym options retain their own language metadata.

The selected locale is stored separately as a plain code under `pharmaflow:locale`. Invalid or unreadable preferences safely start in English; blocked writes leave switching usable in memory. No account, translation service, extra dependency, or catalog transport change is involved.

Generic medication/ingredient names and fictional branch proper names retain their canonical spelling in all locales. This intentionally keeps recognition, identity, sorting, and search consistent: search always uses the existing case/diacritic-normalized canonical name/ingredient substring rules. For example, `paracetamol` works in every interface; translated synonyms or transliterations are not added. Package descriptions, presentation-specific bookmark labels, and imaginary location descriptors use explicitly authored overlays keyed by the same IDs. They add no pharmaceutical facts or entities, and completeness tests cover every canonical presentation and branch.

Prices use locale-aware formatting of the same USD minor-unit values; no currency conversion occurs. Dates show the same fixed UTC observations, with the time zone disclosed. Weekday/interval formatting uses the existing deterministic `Intl` schedule helper; closed labels are translated. There are no relative-time or live open/closed calculations.

Resource tests protect message keys, interpolation variables, nonempty translations, English fallback, and domain overlays. Integration tests protect language metadata, titles, accessible bookmark actions/announcements, locale persistence/storage recovery, and preservation of query/bookmarks/theme. The October 6 localization verification adds 24 tests; all 144 tests pass. The localization browser review covers complete journeys and empty/filtered/unknown states across all three locales, both themes, and 320/360/768/1280 CSS pixels. These checks do not complete the pending human visual, native zoom, or real screen-reader review. That final review must now cover the finished multilingual interface; Phase A remains open.

## Local development

Use Node.js 24 LTS (verified with 24.21.0) and npm (verified with 10.2.1).

```sh
cd frontend
npm ci
npm run dev
```

Open the local URL printed by Vite. The development server binds to localhost by default. For checks and production output:

```sh
npm run typecheck
npm run lint
npm run format:check
npm test
npm run build
npm run preview
```

Build output goes to `frontend/dist/`. A future static host must support SPA rewrites for direct routes. No deployment is configured.

## Boundaries

All medication presentation combinations, stock, prices, branches, schedules, prescription flags, and timestamps are fictional demonstration values. Familiar generic ingredient names provide recognition, not authoritative medication information. PharmaFlow makes no live availability claims, is not a production pharmacy service, and provides no medical advice.

The canonical source is `demo-data/catalog.json`, a fixed September 1, 2026, 12:00 UTC snapshot with nine presentations and three fictional branches. The frontend imports it directly; a future backend seed must consume the same file, preserving identities and timestamps. There are no separately maintained copies.

Catalog state lives in `q`, `branch`, `availability`, `form`, and `sort` URL parameters. Filters combine with AND. Without a branch, status matches any branch and “From” pricing selects the lowest price among available/low-stock observations. With a branch, status/price use that branch only; an unavailable item may have a reference price. Missing prices sort last; missing observations remain “No demo information.” Prices are fictional USD values per stated package. Unknown URL filters fall back safely.

No accounts, patient data, analytics, external service calls, or cloud synchronization. Favorites store medication IDs only in this browser/profile; they are convenience bookmarks, not patient information or private medical records. Other users of the same profile may see them, and clearing browser/site data may remove them. The optional theme preference remains separate. Favorites and theme controls continue working when browser storage is unavailable.

## Code organization

- `src/app`: routing and route accessibility behavior.
- `src/layouts` and `src/components`: shell and reusable UI.
- `src/pages`: Home, Catalog, Medication Detail, Pharmacy Directory, Pharmacy Detail, and Favorites.
- `src/features/catalog`: discovery controls, query parsing, read lifecycle, and result cards.
- `src/features/medication` and `src/features/availability`: detail read lifecycle and responsive branch comparison.
- `src/features/pharmacies`: branch read lifecycle, deterministic locale-aware schedule formatting, and shared weekly-hours presentation.
- `src/features/favorites`: narrow Context/reducer, versioned storage boundary, shared bookmark toggle, and favorites catalog read lifecycle.
- `src/features/preferences`: theme state and safe browser-local locale persistence.
- `src/domain`: domain types and pure discovery/filter/price/summary rules.
- `src/data`: typed asynchronous read contract, canonical-data guard, and immutable local adapter; no HTTP, fake delays, or network calls.
- `src/i18n`: complete English/Spanish/Japanese messages, authored domain-label overlays, initialization, and completeness/integration tests.
- `src/styles`: design tokens, themes, and global foundations.

Vitest, React Testing Library, user-event, jest-dom, and jsdom cover dataset integrity, domain rules, URL state, search/filter/reset/history interactions, detail navigation and branch comparison, and controlled pending/error/stale-read recovery. Run `npm test` for the suite or `npm run test:watch` during development. No permanent browser automation dependency is installed.

No empty speculative folders or enterprise abstractions. Lucide icons are provided by the lucide-react package under its ISC license; no third-party product imagery or branding is used.

Detail routes read through the existing asynchronous CatalogReader. The comparison uses one semantic list and labeled definition structure at every screen size. Missing observations stay null; prices and UTC timestamps come from each actual observation. The optional `catalog` detail URL parameter preserves the catalog query across navigation and refresh; direct detail visits return to `/catalog`. No global navigation store is used.

Pharmacy pages reuse `listBranches` and `getBranch`; the read contract and local adapter need no new operations. Weekly schedules show all seven days, closed days, and supported split intervals using fixed formatting anchors rather than the current clock. Times are local wall times in the branch's disclosed demo time zone. No live open/closed calculations, maps, geolocation, or real contact details are provided. Branch names in medication comparison link to pharmacy details; status and price remain informational. Branch detail links to `/catalog?branch=<slug>` using the existing query serializer and catalog filtering rules. Direct routes, unknown branches, read errors, retry, and stale reads are covered by tests.

Favorites use `pharmaflow:favorites` with `{ "version": 1, "medicationIds": ["med-par-500"] }`. Only unique, validated IDs are stored. Missing data starts empty; malformed or unsupported data starts safely empty with an explanatory notice and is not overwritten until a local bookmark mutation. Read/write failures retain in-memory bookmarks and explain that changes may not survive reload. Relevant storage events validate incoming data and apply last-write-wins updates without writing it back. Clear-all uses inline confirmation and removes only the favorites key, preserving preferences and unrelated data.

Catalog and Medication Detail share one bookmark source of truth. Favorites reuse MedicationCard and the existing default discovery projection; a separate complete `listMedications` read confirms which IDs still exist. IDs are retained while reads are pending or failed and reconciled only after the complete read succeeds. Toggles expose presentation-specific accessible names and pressed state; concise polite announcements and focus recovery support removal and clear-all. The navigation count describes saved presentations.
