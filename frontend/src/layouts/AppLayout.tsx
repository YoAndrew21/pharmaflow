import { useRef, useState } from 'react'
import { Menu, X, Pill, Info } from 'lucide-react'
import { NavLink, Outlet, Link } from 'react-router'
import { useTranslation } from 'react-i18next'
import { ThemeToggle } from '../components/ThemeToggle'
import { LanguageSelector } from '../components/LanguageSelector'
import styles from './AppLayout.module.css'
import { useFavorites } from '../features/favorites/useFavorites'

export function AppLayout() {
  const { t } = useTranslation()
  const favorites = useFavorites()
  const [menuOpen, setMenuOpen] = useState(false)
  const menuButton = useRef<HTMLButtonElement>(null)
  const links = [
    ['/', 'home'],
    ['/catalog', 'catalog'],
    ['/pharmacies', 'pharmacies'],
    ['/favorites', 'favorites'],
  ] as const

  return (
    <div className={styles.shell}>
      <a className="skip-link" href="#main-content">
        {t('skip')}
      </a>
      <header className={styles.header}>
        <div className={`container ${styles.headerInner}`}>
          <Link
            className={styles.brand}
            to="/"
            onClick={() => setMenuOpen(false)}
          >
            <span className={styles.brandIcon}>
              <Pill size={24} aria-hidden="true" />
            </span>
            <span>
              <strong>{t('brand')}</strong>
              <span className={styles.brandCaption}>{t('brandCaption')}</span>
            </span>
          </Link>
          <button
            ref={menuButton}
            className={`utility-button ${styles.menuButton}`}
            type="button"
            aria-expanded={menuOpen}
            aria-controls="primary-navigation"
            onClick={() => setMenuOpen(!menuOpen)}
          >
            {menuOpen ? (
              <X size={20} aria-hidden="true" />
            ) : (
              <Menu size={20} aria-hidden="true" />
            )}
            {t(menuOpen ? 'closeMenu' : 'menu')}
          </button>
          <nav
            id="primary-navigation"
            aria-label={t('navigation')}
            className={`${styles.nav} ${menuOpen ? styles.open : ''}`}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                setMenuOpen(false)
                menuButton.current?.focus()
              }
            }}
          >
            {links.map(([path, key]) => (
              <NavLink
                key={path}
                to={path}
                end={path === '/'}
                aria-label={
                  key === 'favorites'
                    ? t('favorites.navigation', { count: favorites.ids.length })
                    : undefined
                }
                className={({ isActive }) =>
                  isActive ? styles.activeLink : styles.navLink
                }
                onClick={() => setMenuOpen(false)}
              >
                {t(`nav.${key}`)}
                {key === 'favorites' && (
                  <span
                    className={styles.favoriteCount}
                    aria-hidden="true"
                    style={{
                      visibility: favorites.ids.length ? 'visible' : 'hidden',
                    }}
                  >
                    {favorites.ids.length}
                  </span>
                )}
              </NavLink>
            ))}
          </nav>
          <div className={styles.theme}>
            <LanguageSelector />
            <ThemeToggle />
          </div>
        </div>
      </header>
      <main id="main-content" tabIndex={-1} className={styles.main}>
        <Outlet />
      </main>
      <footer className={styles.footer}>
        <div className="container">
          <p className={styles.demo}>
            <Info size={17} aria-hidden="true" />
            <span>{t('demo')}</span>
          </p>
          <p className={styles.footerNote}>{t('footer')}</p>
        </div>
      </footer>
    </div>
  )
}
