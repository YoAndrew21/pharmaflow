import { useEffect, useState } from 'react'
import { Moon, Sun } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import styles from './ThemeToggle.module.css'
import {
  applyTheme,
  getInitialTheme,
  THEME_KEY,
  type Theme,
} from '../features/preferences/theme'

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme)
  const { t } = useTranslation()

  useEffect(() => {
    applyTheme(theme)
    try {
      localStorage.setItem(THEME_KEY, theme)
    } catch {
      // A theme choice remains usable in memory even if persistence is blocked.
    }
  }, [theme])

  return (
    <button
      className={`utility-button ${styles.toggle}`}
      type="button"
      onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
      aria-label={t(
        theme === 'light' ? 'theme.switchDark' : 'theme.switchLight',
      )}
    >
      {theme === 'light' ? (
        <Moon size={18} aria-hidden="true" />
      ) : (
        <Sun size={18} aria-hidden="true" />
      )}
      <span className={styles.label}>
        {t(theme === 'light' ? 'theme.dark' : 'theme.light')}
      </span>
    </button>
  )
}
