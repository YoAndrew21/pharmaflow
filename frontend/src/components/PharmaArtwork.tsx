import { useId } from 'react'
import styles from './PharmaArtwork.module.css'

type Composition =
  | 'blister-carton'
  | 'tablet-blister'
  | 'horizontal-carton'
  | 'upright-carton'
  | 'tablet-bottle'
  | 'bottle-carton'
  | 'bottle'
  | 'capsule-carton'
// Visual assignment only: never changes presentation identity, form, facts or fixtures.
const compositions: Record<string, Composition> = {
  'med-par-500': 'blister-carton',
  'med-par-1000': 'horizontal-carton',
  'med-cet-10': 'upright-carton',
  'med-par-caf': 'tablet-blister',
  'med-asc-250': 'tablet-bottle',
  'med-lor-1': 'bottle-carton',
  'med-sal-09': 'bottle',
  'med-ibu-200': 'capsule-carton',
  'med-ome-20': 'capsule-carton',
}
type Paint = (name: string) => string

function Carton({
  paint,
  horizontal = false,
}: {
  paint: Paint
  horizontal?: boolean
}) {
  return (
    <g
      transform={horizontal ? 'translate(-53 108) scale(1.16 .58)' : undefined}
    >
      <path
        d="M185 67L263 46L306 74V179L228 201L185 176Z"
        fill={paint('pearl')}
      />
      <path d="M263 46L306 74V179L263 150Z" fill={paint('side')} />
      <path d="M185 67L263 46L306 74L228 97Z" fill="var(--art-white)" />
      <path
        d="M185 136L228 162L306 140V179L228 201L185 176Z"
        fill={paint('blue')}
      />
      <path
        d="M194 82L227 102M194 89L218 104"
        stroke="var(--art-shade)"
        strokeWidth="2"
        opacity=".7"
      />
      <path d="M228 98V158" stroke="var(--art-white)" strokeOpacity=".85" />
      <path
        d="M267 58L300 78V170"
        fill="none"
        stroke="var(--art-white)"
        strokeOpacity=".22"
      />
    </g>
  )
}
function Blister({ paint }: { paint: Paint }) {
  return (
    <g transform="rotate(-22 149 149)">
      <rect
        x="94"
        y="79"
        width="111"
        height="150"
        rx="12"
        fill="var(--art-shadow)"
        opacity=".2"
      />
      <rect
        x="94"
        y="75"
        width="111"
        height="150"
        rx="12"
        fill={paint('foil')}
        stroke="var(--art-shade)"
      />
      <path
        d="M100 87V211M199 89V210"
        stroke="var(--art-white)"
        strokeOpacity=".85"
      />
      {[0, 1, 2].map((row) =>
        [0, 1].map((col) => (
          <g key={`${row}-${col}`}>
            <ellipse
              cx={123 + col * 54}
              cy={104 + row * 47}
              rx="20"
              ry="16"
              fill="var(--art-shade)"
            />
            <ellipse
              cx={123 + col * 54}
              cy={101 + row * 47}
              rx="16"
              ry="12"
              fill={paint('pearl')}
              stroke="var(--art-white)"
            />
            <path
              d={`M${112 + col * 54} ${101 + row * 47}h22`}
              stroke="var(--art-shade)"
            />
          </g>
        )),
      )}
    </g>
  )
}
function Bottle({ paint, wide = false }: { paint: Paint; wide?: boolean }) {
  return (
    <g transform={wide ? 'translate(-18 12) scale(1.1 .94)' : undefined}>
      <rect x="123" y="58" width="72" height="37" rx="8" fill={paint('blue')} />
      <ellipse
        cx="159"
        cy="59"
        rx="35"
        ry="6"
        fill="var(--art-cyan)"
        opacity=".45"
      />
      <path
        d="M132 94V107C107 116 103 126 103 150V197Q103 216 159 216Q215 216 215 197V150C215 126 211 116 186 107V94Z"
        fill={paint('pearl')}
      />
      <path
        d="M104 150Q159 169 214 150V192Q159 212 104 192Z"
        fill={paint('blue')}
      />
      <path
        d="M123 118Q115 141 122 194"
        stroke="var(--art-white)"
        strokeWidth="8"
        strokeLinecap="round"
        strokeOpacity=".65"
        fill="none"
      />
      <path
        d="M135 69V87M147 69V87M159 69V87M171 69V87M183 69V87"
        stroke="var(--art-cyan)"
        strokeOpacity=".5"
        strokeWidth="2"
      />
      <path
        d="M205 136V196"
        stroke="var(--art-deep)"
        strokeOpacity=".17"
        strokeWidth="3"
      />
    </g>
  )
}
function Capsule({ paint, transform }: { paint: Paint; transform: string }) {
  return (
    <g transform={transform}>
      <rect
        x="115"
        y="79"
        width="64"
        height="150"
        rx="32"
        fill={paint('blue')}
      />
      <path d="M115 154V111A32 32 0 0 1 179 111V154Z" fill={paint('pearl')} />
      <path
        d="M116 153Q147 159 178 153"
        fill="none"
        stroke="var(--art-white)"
        strokeOpacity=".5"
      />
      <path
        d="M130 101Q123 135 131 149M129 165Q126 187 135 206"
        stroke="var(--art-white)"
        strokeOpacity=".55"
        strokeWidth="6"
        strokeLinecap="round"
        fill="none"
      />
    </g>
  )
}
function Tablets({ paint }: { paint: Paint }) {
  return (
    <g>
      {[0, 1].map((i) => (
        <g key={i} transform={`translate(${i * 48} ${i * -14})`}>
          <ellipse cx="240" cy="220" rx="25" ry="13" fill="var(--art-shade)" />
          <ellipse
            cx="240"
            cy="216"
            rx="25"
            ry="13"
            fill={paint('pearl')}
            stroke="var(--art-white)"
            strokeOpacity=".6"
          />
          <path d="M221 216H259" stroke="var(--art-shade)" />
        </g>
      ))}
    </g>
  )
}
function BranchBuilding({ paint, variant }: { paint: Paint; variant: string }) {
  return (
    <>
      <path
        d="M53 188L204 114L352 188L202 248Z"
        fill="var(--art-line)"
        opacity=".38"
      />
      {variant === 'willow-arcade' ? (
        <>
          <path d="M119 67L218 28L283 62L185 104Z" fill={paint('pearl')} />
          <path d="M119 67L185 104V221L119 184Z" fill={paint('side')} />
          <path d="M185 104L283 62V179L185 221Z" fill={paint('blue')} />
          <path d="M125 67L218 32L275 62L185 99Z" fill="var(--art-white)" />
          {[0, 1].map((row) => (
            <path
              key={row}
              transform={`translate(0 ${row * 37})`}
              d="M199 113L228 101V124L199 137ZM239 96L268 84V107L239 120Z"
              fill={paint('glass')}
            />
          ))}
          <path d="M204 181L231 169V201L204 213Z" fill="var(--art-cyan)" />
          <path
            d="M132 92L173 115V140L132 117ZM132 137L173 160V183L132 160Z"
            fill="var(--art-white)"
            opacity=".7"
          />
          <path
            d="M278 66V175"
            stroke="var(--art-white)"
            strokeOpacity=".45"
            strokeWidth="3"
          />
          <path
            d="M251 58V38M241 48H261"
            stroke="var(--art-blue)"
            strokeWidth="6"
          />
        </>
      ) : variant === 'lantern-court' ? (
        <>
          <path
            d="M83 105L172 65L231 93L273 65L326 92L232 139L188 119L143 145Z"
            fill={paint('pearl')}
          />
          <path d="M83 105L143 145V213L83 180Z" fill={paint('side')} />
          <path
            d="M143 145L188 119V188L143 213ZM188 119L232 139V210L188 188Z"
            fill={paint('blue')}
          />
          <path d="M232 139L326 92V163L232 210Z" fill={paint('side')} />
          <path
            d="M154 153L178 139V176L154 190ZM199 142L220 151V183L199 174Z"
            fill={paint('glass')}
          />
          <path
            d="M249 146L273 134V163L249 176ZM286 127L311 114V144L286 156Z"
            fill={paint('glass')}
          />
          <path d="M180 183L230 205L215 218L167 194Z" fill="var(--art-shade)" />
          <path
            d="M95 123L131 144V172L95 151Z"
            fill="var(--art-white)"
            opacity=".7"
          />
          <path
            d="M255 91V73M245 82H265"
            stroke="var(--art-blue)"
            strokeWidth="6"
          />
        </>
      ) : (
        <>
          <path d="M72 107L228 46L323 94L165 159Z" fill={paint('pearl')} />
          <path d="M72 107L165 159V220L72 170Z" fill={paint('side')} />
          <path d="M165 159L323 94V156L165 220Z" fill={paint('blue')} />
          <path d="M163 148L324 82L334 101L170 170Z" fill={paint('blue')} />
          <path
            d="M170 168L334 100V107L170 176Z"
            fill="var(--art-deep)"
            opacity=".55"
          />
          <path
            d="M184 179L222 163V200L184 216ZM234 158L269 144V175L234 190ZM280 139L312 126V157L280 171Z"
            fill={paint('glass')}
          />
          <path
            d="M87 127L149 162V183L87 149Z"
            fill="var(--art-white)"
            opacity=".7"
          />
          <path
            d="M205 86V66M194 76H216"
            stroke="var(--art-blue)"
            strokeWidth="7"
          />
        </>
      )}
      <path
        d="M64 198V164M340 196V163"
        stroke="var(--art-line)"
        strokeWidth="4"
      />
      <circle cx="64" cy="160" r="11" fill={paint('blue')} />
      <circle cx="340" cy="159" r="9" fill={paint('blue')} />
    </>
  )
}

/** Original conceptual geometry, never real packaging or a real location. */
export function PharmaArtwork({
  kind,
  variant = '',
}: {
  kind: string
  variant?: string
}) {
  const id = useId().replaceAll(':', '')
  const paint: Paint = (name) => `url(#${id}-${name})`
  const composition =
    compositions[variant] ??
    (kind === 'capsule'
      ? 'capsule-carton'
      : kind === 'solution'
        ? 'bottle-carton'
        : 'blister-carton')
  return (
    <div
      className={styles.art}
      data-kind={kind}
      data-variant={variant}
      aria-hidden="true"
    >
      <svg viewBox="0 0 400 260" width="400" height="260" focusable="false">
        <defs>
          <linearGradient id={`${id}-blue`} x1="0" x2="1">
            <stop stopColor="var(--art-deep)" />
            <stop offset=".2" stopColor="var(--art-blue)" />
            <stop offset=".35" stopColor="var(--art-cyan)" />
            <stop offset=".5" stopColor="var(--art-blue)" />
            <stop offset=".92" stopColor="var(--art-deep)" />
            <stop offset="1" stopColor="var(--art-blue)" />
          </linearGradient>
          <linearGradient id={`${id}-pearl`} x1="0" y1="0" x2=".8" y2="1">
            <stop stopColor="var(--art-white)" />
            <stop offset=".45" stopColor="#ffffff" />
            <stop offset="1" stopColor="var(--art-shade)" />
          </linearGradient>
          <linearGradient id={`${id}-side`} x1="0" x2="1">
            <stop stopColor="var(--art-shade)" />
            <stop offset="1" stopColor="var(--art-line)" />
          </linearGradient>
          <linearGradient id={`${id}-foil`} x1="0" y1="0" x2="1" y2="1">
            <stop stopColor="var(--art-shade)" />
            <stop offset=".3" stopColor="var(--art-white)" />
            <stop offset=".4" stopColor="var(--art-shade)" />
            <stop offset=".64" stopColor="var(--art-white)" />
            <stop offset="1" stopColor="var(--art-shade)" />
          </linearGradient>
          <linearGradient id={`${id}-glass`} x1="0" y1="0" x2="1" y2="1">
            <stop stopColor="var(--art-white)" stopOpacity=".85" />
            <stop offset=".2" stopColor="var(--art-cyan)" stopOpacity=".9" />
            <stop offset="1" stopColor="var(--art-blue)" stopOpacity=".4" />
          </linearGradient>
          <radialGradient id={`${id}-halo`}>
            <stop stopColor="var(--art-blue)" stopOpacity=".18" />
            <stop offset="1" stopColor="var(--art-blue)" stopOpacity="0" />
          </radialGradient>
          <filter
            id={`${id}-shadow`}
            x="-50%"
            y="-100%"
            width="200%"
            height="300%"
          >
            <feGaussianBlur stdDeviation="5" />
          </filter>
        </defs>
        <ellipse cx="200" cy="137" rx="182" ry="123" fill={paint('halo')} />
        <ellipse
          cx="200"
          cy="225"
          rx="113"
          ry="10"
          fill="var(--art-shadow)"
          opacity=".2"
          filter={paint('shadow')}
        />
        <g className={styles.object}>
          {kind === 'branch' ? (
            <BranchBuilding paint={paint} variant={variant} />
          ) : (
            <>
              {composition === 'blister-carton' && (
                <>
                  <Carton paint={paint} />
                  <Blister paint={paint} />
                  <Tablets paint={paint} />
                </>
              )}
              {composition === 'tablet-blister' && (
                <>
                  <g transform="translate(75 -5) rotate(14 150 150)">
                    <Blister paint={paint} />
                  </g>
                  <g transform="translate(-90 -5)">
                    <Tablets paint={paint} />
                  </g>
                </>
              )}
              {composition === 'horizontal-carton' && (
                <>
                  <Carton paint={paint} horizontal />
                  <g transform="translate(-38 -22) scale(.82)">
                    <Blister paint={paint} />
                  </g>
                  <g transform="translate(25 -4)">
                    <Tablets paint={paint} />
                  </g>
                </>
              )}
              {composition === 'upright-carton' && (
                <>
                  <g transform="translate(-62 7) scale(1.06)">
                    <Carton paint={paint} />
                  </g>
                  <g transform="translate(5 -5)">
                    <Tablets paint={paint} />
                  </g>
                </>
              )}
              {composition === 'tablet-bottle' && (
                <>
                  <g transform="translate(25 0)">
                    <Bottle paint={paint} wide />
                  </g>
                  <g transform="translate(5 -2)">
                    <Tablets paint={paint} />
                  </g>
                </>
              )}
              {composition === 'bottle-carton' && (
                <>
                  <g transform="translate(32 8) scale(.94)">
                    <Carton paint={paint} />
                  </g>
                  <Bottle paint={paint} />
                </>
              )}
              {composition === 'bottle' && (
                <g transform="translate(9 -8) scale(1.2)">
                  <Bottle paint={paint} />
                </g>
              )}
              {composition === 'capsule-carton' && (
                <>
                  <Carton paint={paint} />
                  <Capsule paint={paint} transform="rotate(-37 147 148)" />
                  <Capsule
                    paint={paint}
                    transform="translate(150 124) scale(.5) rotate(50 147 148)"
                  />
                </>
              )}
            </>
          )}
        </g>
      </svg>
    </div>
  )
}
