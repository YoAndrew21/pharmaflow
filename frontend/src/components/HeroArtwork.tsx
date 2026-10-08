import { useId } from 'react'
import styles from './HeroArtwork.module.css'

/** Central asset can be replaced without changing the orbit or Home callout layers. */
export function HeroArtwork({ renderSrc }: { renderSrc?: string }) {
  const id = useId().replaceAll(':', '')
  const paint = (name: string) => `url(#${id}-${name})`
  return (
    <div className={styles.scene} aria-hidden="true">
      <svg
        className={styles.orbits}
        viewBox="0 0 640 600"
        width="640"
        height="600"
        focusable="false"
      >
        <g fill="none" stroke="var(--art-line)">
          <circle cx="320" cy="294" r="250" strokeDasharray="2 12" />
          <ellipse
            cx="320"
            cy="294"
            rx="224"
            ry="196"
            opacity=".5"
            transform="rotate(-24 320 294)"
          />
          <path d="M30 294H100M542 294H610M320 6V42M320 548V586M80 167H154L203 216M435 183L484 130H562M435 390L485 440H580" />
        </g>
        <g fill="var(--art-cyan)">
          <circle cx="94" cy="417" r="4" />
          <circle cx="552" cy="229" r="4" />
          <circle cx="320" cy="548" r="3" />
        </g>
      </svg>
      <div className={styles.shadow} />
      <div className={styles.centerpiece}>
        {renderSrc ? (
          <img
            className={styles.render}
            src={renderSrc}
            alt=""
            width="1280"
            height="1200"
            decoding="async"
          />
        ) : (
          <svg viewBox="0 0 640 600" width="640" height="600" focusable="false">
            <defs>
              <linearGradient id={`${id}-ceramic`} x1="0" x2="1">
                <stop stopColor="#a8bfde" />
                <stop offset=".14" stopColor="#e2efff" />
                <stop offset=".28" stopColor="#ffffff" />
                <stop offset=".48" stopColor="#f0f8ff" />
                <stop offset=".76" stopColor="#bcd7f1" />
                <stop offset=".96" stopColor="#759fcb" />
                <stop offset="1" stopColor="#b5eaff" />
              </linearGradient>
              <linearGradient id={`${id}-shell`} x1="0" x2="1">
                <stop stopColor="#163782" />
                <stop offset=".15" stopColor="#3472ec" />
                <stop offset=".28" stopColor="#85bfff" />
                <stop offset=".37" stopColor="#377ef8" />
                <stop offset=".63" stopColor="#245ade" />
                <stop offset=".9" stopColor="#122d77" />
                <stop offset="1" stopColor="#68c8fc" />
              </linearGradient>
              <linearGradient id={`${id}-ribbon`} x1="0" y1="0" x2="1" y2="1">
                <stop stopColor="#bdf8ff" stopOpacity=".8" />
                <stop offset=".33" stopColor="#4aa6fa" stopOpacity=".22" />
                <stop offset=".62" stopColor="#4385ec" stopOpacity=".5" />
                <stop offset="1" stopColor="#a99af7" stopOpacity=".78" />
              </linearGradient>
              <linearGradient
                id={`${id}-reflection`}
                x1="0"
                y1="0"
                x2="0"
                y2="1"
              >
                <stop stopColor="#ffffff" stopOpacity=".75" />
                <stop offset=".5" stopColor="#c3eaff" stopOpacity=".06" />
                <stop offset="1" stopColor="#ffffff" stopOpacity="0" />
              </linearGradient>
              <radialGradient id={`${id}-sphere`} cx=".32" cy=".25" r=".8">
                <stop stopColor="#f1fcff" />
                <stop offset=".2" stopColor="#a9eaff" />
                <stop offset=".55" stopColor="#427de8" />
                <stop offset="1" stopColor="#183372" />
              </radialGradient>
              <clipPath id={`${id}-capsule`}>
                <rect x="242" y="131" width="156" height="326" rx="78" />
              </clipPath>
              <filter
                id={`${id}-contact`}
                x="-50%"
                y="-50%"
                width="200%"
                height="200%"
              >
                <feGaussianBlur stdDeviation="9" />
              </filter>
            </defs>
            <g className={styles.rearRibbon} fill="none">
              <path
                d="M164 380C49 265 159 126 348 181S554 365 430 423"
                stroke={paint('ribbon')}
                strokeWidth="42"
              />
              <path
                d="M163 358C87 272 176 149 346 201S529 357 430 400"
                stroke="#c2efff"
                strokeOpacity=".6"
                strokeWidth="2"
              />
              <path
                d="M164 398C70 288 151 135 348 164"
                stroke="#4e8ce3"
                strokeOpacity=".3"
                strokeWidth="1.5"
              />
            </g>
            <g transform="rotate(32 320 294)">
              <rect
                x="255"
                y="144"
                width="156"
                height="326"
                rx="78"
                fill="var(--art-shadow)"
                opacity=".3"
                filter={paint('contact')}
              />
              <g clipPath={paint('capsule')}>
                <rect
                  x="242"
                  y="131"
                  width="156"
                  height="326"
                  fill={paint('shell')}
                />
                <rect
                  x="242"
                  y="131"
                  width="156"
                  height="165"
                  fill={paint('ceramic')}
                />
                <ellipse
                  cx="320"
                  cy="136"
                  rx="70"
                  ry="32"
                  fill="#ffffff"
                  opacity=".15"
                />
                <path
                  d="M243 293Q320 310 397 293"
                  fill="none"
                  stroke="#12346f"
                  strokeOpacity=".32"
                  strokeWidth="5"
                />
                <path
                  d="M243 291Q320 305 397 291"
                  fill="none"
                  stroke="#d7f4ff"
                  strokeOpacity=".7"
                  strokeWidth="2"
                />
                <path
                  d="M271 154Q254 222 273 282L283 292Q268 215 292 147Z"
                  fill={paint('reflection')}
                />
                <path
                  d="M266 313Q258 359 278 408"
                  fill="none"
                  stroke="#d7eeff"
                  strokeOpacity=".58"
                  strokeWidth="8"
                  strokeLinecap="round"
                />
                <path
                  d="M379 167C402 257 402 367 366 426"
                  fill="none"
                  stroke="#b0e9ff"
                  strokeOpacity=".56"
                  strokeWidth="2.5"
                />
                <ellipse
                  cx="326"
                  cy="447"
                  rx="59"
                  ry="17"
                  fill="#081d5e"
                  opacity=".23"
                />
              </g>
              <rect
                x="242"
                y="131"
                width="156"
                height="326"
                rx="78"
                fill="none"
                stroke="#f1faff"
                strokeOpacity=".42"
              />
            </g>
            <g className={styles.frontRibbon} fill="none">
              <path
                d="M430 423C301 480 137 395 155 283"
                stroke={paint('ribbon')}
                strokeWidth="42"
              />
              <path
                d="M429 402C303 449 164 375 177 281"
                stroke="#ddfaff"
                strokeOpacity=".8"
                strokeWidth="2"
              />
              <path
                d="M429 443C285 500 114 396 135 284"
                stroke="#7abdfb"
                strokeOpacity=".45"
                strokeWidth="1.5"
              />
              <path
                d="M400 430C309 458 224 428 190 382"
                stroke="#f2fcff"
                strokeOpacity=".23"
                strokeWidth="8"
                strokeLinecap="round"
              />
            </g>
            <g className={styles.nodes}>
              <circle cx="161" cy="246" r="25" fill={paint('sphere')} />
              <circle cx="476" cy="369" r="19" fill={paint('sphere')} />
              <circle cx="427" cy="127" r="12" fill="var(--art-violet)" />
              <circle cx="420" cy="121" r="3" fill="#f4faff" opacity=".6" />
            </g>
          </svg>
        )}
      </div>
    </div>
  )
}
