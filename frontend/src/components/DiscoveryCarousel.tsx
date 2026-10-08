import {
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import styles from './DiscoveryCarousel.module.css'

export function DiscoveryCarousel({
  title,
  description,
  items,
}: {
  title: string
  description: string
  items: { id: string; content: ReactNode }[]
}) {
  const { t } = useTranslation()
  const id = useId()
  const track = useRef<HTMLUListElement>(null)
  const [edges, setEdges] = useState({ start: true, end: false })
  const measure = useCallback(() => {
    const node = track.current
    if (!node) return
    const start = node.scrollLeft <= 1
    const end = node.scrollLeft + node.clientWidth >= node.scrollWidth - 1
    setEdges((previous) =>
      previous.start === start && previous.end === end
        ? previous
        : { start, end },
    )
  }, [])
  useEffect(() => {
    measure()
    const observer =
      typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(measure)
    if (track.current) observer?.observe(track.current)
    window.addEventListener('resize', measure)
    return () => {
      observer?.disconnect()
      window.removeEventListener('resize', measure)
    }
  }, [measure, items.length])
  const move = (direction: 'previous' | 'next' | 'first' | 'last') => {
    const node = track.current
    if (!node) return
    const behavior = window.matchMedia?.('(prefers-reduced-motion: reduce)')
      .matches
      ? 'instant'
      : 'smooth'
    if (direction === 'first' || direction === 'last') {
      node.scrollTo({
        left: direction === 'first' ? 0 : node.scrollWidth,
        behavior,
      })
    } else {
      if (direction === 'previous' ? edges.start : edges.end) return
      node.scrollBy({
        left: node.clientWidth * 0.85 * (direction === 'previous' ? -1 : 1),
        behavior,
      })
    }
  }
  return (
    <section className={styles.section} aria-labelledby={`${id}-heading`}>
      <div className={styles.header}>
        <div>
          <p className="eyebrow">{t('visual.discoveryEyebrow')}</p>
          <h2 id={`${id}-heading`}>{title}</h2>
          <p>{description}</p>
        </div>
        <div className={styles.controls}>
          <button
            className="utility-button"
            type="button"
            aria-label={t('visual.previous')}
            aria-controls={`${id}-track`}
            aria-disabled={edges.start}
            onClick={() => move('previous')}
          >
            <ArrowLeft size={20} aria-hidden="true" />
          </button>
          <button
            className="utility-button"
            type="button"
            aria-label={t('visual.next')}
            aria-controls={`${id}-track`}
            aria-disabled={edges.end}
            onClick={() => move('next')}
          >
            <ArrowRight size={20} aria-hidden="true" />
          </button>
        </div>
      </div>
      <p id={`${id}-instructions`} className={styles.instructions}>
        {t('visual.carouselHelp')}
      </p>
      <ul
        id={`${id}-track`}
        ref={track}
        className={styles.track}
        tabIndex={0}
        aria-labelledby={`${id}-heading`}
        aria-describedby={`${id}-instructions`}
        onScroll={measure}
        onFocusCapture={(event) => {
          if (event.target === event.currentTarget) return
          const item = (event.target as HTMLElement).closest('li')
          if (!item || item.parentElement !== event.currentTarget) return
          const visible = event.currentTarget.getBoundingClientRect()
          const card = item.getBoundingClientRect()
          if (card.left < visible.left || card.right > visible.right) {
            item.scrollIntoView({
              behavior: 'instant',
              block: 'nearest',
              inline: 'nearest',
            })
          }
        }}
        onKeyDown={(event) => {
          if (event.target !== event.currentTarget) return
          const direction = (
            {
              ArrowLeft: 'previous',
              ArrowRight: 'next',
              Home: 'first',
              End: 'last',
            } as const
          )[event.key as 'ArrowLeft' | 'ArrowRight' | 'Home' | 'End']
          if (direction) {
            event.preventDefault()
            move(direction)
          }
        }}
      >
        {items.map((item) => (
          <li key={item.id}>{item.content}</li>
        ))}
      </ul>
    </section>
  )
}
