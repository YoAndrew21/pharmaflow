import { Bookmark, CircleHelp, Search, Unplug } from 'lucide-react'
import styles from './StateArtwork.module.css'

export function StateArtwork({
  kind = 'search',
}: {
  kind?: 'search' | 'error' | 'bookmark' | 'missing'
}) {
  const Icon = {
    search: Search,
    error: Unplug,
    bookmark: Bookmark,
    missing: CircleHelp,
  }[kind]
  return (
    <div className={styles.mark} aria-hidden="true">
      <span />
      <Icon size={30} strokeWidth={1.5} />
      <span />
    </div>
  )
}
