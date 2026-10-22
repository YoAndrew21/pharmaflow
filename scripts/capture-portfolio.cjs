/* Public-UI-only screenshots. No app injection, storage injection, API mocks or credentials. */
const assert = require('node:assert/strict')
const fs = require('node:fs/promises')
const path = require('node:path')
const crypto = require('node:crypto')
const { chromium } = require('playwright')
const sharp = require('sharp')

const origin = 'https://pharmaflow-oux0.onrender.com'
const output = path.resolve(__dirname, '../docs/screenshots')
const desktop = { width: 1440, height: 900 }
const mobile = { width: 390, height: 844 }
const captures = [
  { id: 'home', file: 'home-desktop-dark.webp', viewport: desktop, language: 'en', theme: 'dark' },
  { id: 'catalog', file: 'catalog-desktop-light.webp', viewport: desktop, language: 'en', theme: 'light' },
  { id: 'medication-detail', file: 'medication-detail-desktop-dark.webp', viewport: desktop, language: 'en', theme: 'dark' },
  { id: 'pharmacy-detail', file: 'pharmacy-detail-mobile-ja-light.webp', viewport: mobile, language: 'ja', theme: 'light' },
  { id: 'favorites', file: 'favorites-mobile-es-dark.webp', viewport: mobile, language: 'es', theme: 'dark' },
]

async function ready(page) {
  await page.waitForFunction(() => {
    const regions = [...document.querySelectorAll('[data-read-region]')]
    return regions.length > 0 && regions.every(node => node.getAttribute('aria-busy') !== 'true')
      && !document.querySelector('[data-skeleton]')
  }, undefined, { timeout: 180000 })
  const error = page.getByRole('button', { name: /^(Try again|Reintentar|再試行)$/ })
  if (await error.isVisible()) {
    await error.click()
    await page.waitForTimeout(300)
    await page.waitForFunction(() => !document.querySelector('[aria-busy="true"], [data-skeleton]'), undefined, { timeout: 180000 })
  }
  assert.equal(await error.isVisible(), false, 'API error/retry state must not be captured')
}

async function navigate(page, name) {
  const link = page.locator('#primary-navigation').getByRole('link', { name, exact: true })
  if (!(await link.isVisible())) await page.locator('button[aria-controls="primary-navigation"]').click()
  await link.click()
  await ready(page)
}

async function language(page, label, code) {
  await page.getByRole('button', { name: /Language/ }).click()
  await page.getByRole('option', { name: label, exact: true }).click()
  await page.waitForFunction(value => document.documentElement.lang === value, code)
}

async function finish(page, item) {
  await ready(page)
  await page.evaluate(async () => {
    await document.fonts.ready
    await Promise.allSettled(document.getAnimations().map(animation => animation.finished))
  })
  await page.mouse.move(0, 0)
  await page.waitForTimeout(400)
  assert.equal(new URL(page.url()).origin, origin, 'Capture must come from the public application')
  assert.equal(await page.locator('html').getAttribute('lang'), item.language)
  assert.equal(await page.locator('html').getAttribute('data-theme'), item.theme)
  assert.deepEqual(page.viewportSize(), item.viewport)
  assert.equal(await page.locator('[aria-busy="true"], [data-skeleton], [role="listbox"]').count(), 0)
  assert.equal(await page.locator('[role="alert"]').count(), 0)
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth), false, 'Horizontal overflow')
  const buffer = await page.screenshot({ fullPage: false, type: 'png' })
  // Lossless technical conversion only: no crop, compositing, retouching or resizing.
  const webp = await sharp(buffer).webp({ lossless: true, effort: 6 }).toBuffer()
  const metadata = await sharp(webp).metadata()
  assert.equal(metadata.format, 'webp')
  assert.equal(metadata.width, item.viewport.width)
  assert.equal(metadata.height, item.viewport.height)
  await sharp(webp).raw().toBuffer() // Decode completely to check integrity.
  await fs.writeFile(path.join(output, item.file), webp)
  return {
    file: item.file, page: new URL(page.url()).pathname, source: origin,
    language: item.language, theme: item.theme, width: metadata.width, height: metadata.height,
    bytes: webp.length, sha256: crypto.createHash('sha256').update(webp).digest('hex'),
    capturedAt: new Date().toISOString(), loadingOrError: false,
  }
}

async function main() {
  const only = process.argv[2]
  if (only && !captures.some(item => item.id === only)) throw new Error('Unknown capture ID')
  await fs.mkdir(output, { recursive: true })
  const browser = await chromium.launch({ channel: process.env.PHARMAFLOW_CAPTURE_BROWSER || 'chrome', headless: true })
  const results = []
  try {
    for (const item of captures.filter(item => !only || item.id === only)) {
      console.log(`Navigating public UI: ${item.id}`)
      const context = await browser.newContext({
        viewport: item.viewport, deviceScaleFactor: 1, locale: 'en-US', timezoneId: 'UTC',
        colorScheme: item.theme, reducedMotion: 'reduce', isMobile: item.viewport === mobile, hasTouch: item.viewport === mobile,
      })
      try {
        const page = await context.newPage()
        page.setDefaultTimeout(60000)
        await page.goto(origin, { waitUntil: 'domcontentloaded', timeout: 90000 })
        await page.getByRole('heading', { level: 1, name: /Medication discovery/ }).waitFor({ timeout: 180000 })
        await ready(page)
        if (item.id === 'home') {
          await page.getByRole('heading', { name: 'A little exploration. More clarity.' }).waitFor()
        } else if (item.id === 'catalog' || item.id === 'medication-detail' || item.id === 'favorites') {
          await navigate(page, 'Catalog')
          await page.getByRole('heading', { name: /Paracetamol.*500 mg/ }).first().waitFor()
          assert.equal(await page.locator('main article').count(), 9)
          if (item.id === 'medication-detail') {
            await page.getByRole('link', { name: /^Paracetamol.*500 mg$/ }).first().click()
            await ready(page)
            await page.getByRole('heading', { name: 'Availability by branch', exact: true }).waitFor()
            assert.match(await page.locator('main').innerText(), /Low stock/)
            assert.match(await page.locator('main').innerText(), /Unavailable/)
            await page.getByRole('heading', { name: 'Availability by branch', exact: true }).evaluate(node => {
              window.scrollTo(0, node.getBoundingClientRect().top + window.scrollY - 32)
            })
          } else if (item.id === 'favorites') {
            await page.getByRole('button', { name: /^Save Paracetamol.*500 mg/ }).first().click()
            const favoritesLink = page.locator('#primary-navigation').getByRole('link', { name: /^Favorites/ })
            if (!(await favoritesLink.isVisible())) await page.locator('button[aria-controls="primary-navigation"]').click()
            await favoritesLink.click()
            await ready(page)
            await language(page, 'Español', 'es')
            await page.getByRole('heading', { level: 1, name: 'Favoritos', exact: true }).waitFor()
            assert.equal(await page.locator('main article').count(), 1)
            await page.getByRole('heading', { level: 1 }).evaluate(node => node.scrollIntoView({ block: 'start' }))
          } else {
            await page.getByText('9 presentations found', { exact: true }).evaluate(node => {
              window.scrollTo(0, node.getBoundingClientRect().top + window.scrollY - 24)
            })
          }
        } else {
          await navigate(page, 'Pharmacies')
          await page.getByRole('link', { name: 'Aster Commons', exact: true }).click()
          await ready(page)
          await page.getByRole('heading', { level: 1, name: 'Aster Commons', exact: true }).waitFor()
          await language(page, '日本語', 'ja')
          await page.locator('[id^="hours-"]').evaluate(node => node.scrollIntoView({ block: 'center' }))
        }
        const result = await finish(page, item)
        results.push(result)
        console.log(JSON.stringify(result))
      } finally { await context.close() }
    }
    let previous = []
    if (only) {
      try { previous = JSON.parse(await fs.readFile(path.join(output, 'manifest.json'), 'utf8')).captures }
      catch (error) { if (error.code !== 'ENOENT') throw error }
    }
    const combined = captures.map(item => results.find(row => row.file === item.file) || previous.find(row => row.file === item.file)).filter(Boolean)
    await fs.writeFile(path.join(output, 'manifest.json'), JSON.stringify({
      source: origin, browser: await browser.version(), conversion: 'PNG to lossless WebP; pixels unchanged', captures: combined,
    }, null, 2) + '\n')
  } finally { await browser.close() }
}

main().catch(error => { console.error(`Capture failed: ${error.message}`); process.exitCode = 1 })
