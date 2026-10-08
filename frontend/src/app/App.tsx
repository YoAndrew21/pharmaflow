import { useEffect, useRef } from 'react'
import { BrowserRouter, Route, Routes, useLocation } from 'react-router'
import { useTranslation } from 'react-i18next'
import { AppLayout } from '../layouts/AppLayout'
import { HomePage } from '../pages/HomePage'
import { FavoritesPage } from '../pages/FavoritesPage'
import { FavoritesProvider } from '../features/favorites/FavoritesContext'
import { NotFoundPage } from '../pages/NotFoundPage'
import { CatalogPage } from '../pages/CatalogPage'
import { MedicationDetailPage } from '../pages/MedicationDetailPage'
import { PharmacyDirectoryPage } from '../pages/PharmacyDirectoryPage'
import { PharmacyDetailPage } from '../pages/PharmacyDetailPage'

function RouteAccessibility() {
  const { pathname } = useLocation()
  const previousPath = useRef(pathname)
  const { t } = useTranslation()
  useEffect(() => {
    const titleKeys: Record<string, string> = {
      '/': 'nav.home',
      '/catalog': 'catalog.title',
      '/pharmacies': 'pharmacies.title',
      '/favorites': 'favorites.title',
    }
    // Detail pages own their resolved/not-found titles, including locale changes.
    if (
      !pathname.startsWith('/medications/') &&
      !pathname.startsWith('/pharmacies/')
    )
      document.title = `${t(titleKeys[pathname] ?? 'notFound.title')} | ${t('brand')}`
    if (previousPath.current !== pathname) {
      document.getElementById('main-content')?.focus()
      window.scrollTo({ top: 0, behavior: 'instant' })
      previousPath.current = pathname
    }
  }, [pathname, t])
  return null
}

export function App() {
  return (
    <FavoritesProvider>
      <BrowserRouter>
        <RouteAccessibility />
        <Routes>
          <Route element={<AppLayout />}>
            <Route index element={<HomePage />} />
            <Route path="catalog" element={<CatalogPage />} />
            <Route
              path="medications/:slug"
              element={<MedicationDetailPage />}
            />
            <Route path="pharmacies" element={<PharmacyDirectoryPage />} />
            <Route path="pharmacies/:slug" element={<PharmacyDetailPage />} />
            <Route path="favorites" element={<FavoritesPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </FavoritesProvider>
  )
}
