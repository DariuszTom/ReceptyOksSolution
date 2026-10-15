# HomeSeeker – potencjalne funkcje

Pomysły do rozważenia w przyszłości. Nic z tej listy nie jest jeszcze zaimplementowane.

## Scraper rynku pierwotnego (rynekpierwotny.pl)

**Cel:** uwzględnić w skanach nowe inwestycje deweloperskie (mieszkania i domy).

### Rozpoznanie (2026-10-15)

- Listy: `https://rynekpierwotny.pl/s/nowe-mieszkania-{miasto}/` i `/s/nowe-domy-{miasto}/`,
  stronicowanie `?page=N` (np. Kraków: ~7 300 ofert mieszkań).
- Strona renderowana po stronie serwera; pełne dane w JSON `window.__INITIAL_STATE__`
  (podobnie jak `__NEXT_DATA__` w Otodom) oraz w `application/ld+json`.
- `robots.txt`: listy i `?page=` dozwolone; zablokowane m.in. `/ajax/`, `*/ws/`,
  `?sort=`, `?order_by=`, `?distance=`, `?region_`. Nagłówek `Content-Signal: ai-input=yes, ai-train=no`.
- Element listy to **inwestycja**, nie pojedynczy lokal. Dostępne pola m.in.:
  `id`, `url` (`/oferty/{deweloper}/{slug}-{id}/`), `address` („Kraków, Podgórze, ul. …”),
  `vendor.name`, `stats.ranges_price_min/max`, `ranges_area_min/max`,
  `ranges_price_m2_min/max`, `ranges_rooms_min/max`, `properties_count_for_sale`,
  `stats.distance_from_region.distance` (km od centrum).
- Strona inwestycji nie pokazała w prostym teście łatwo dostępnych danych pojedynczych lokali.

### Wariant 1 – inwestycja jako jedno ogłoszenie (prostszy, rekomendowany na start)

- `RynekPierwotnyScraper : IListingScraper`, parsowanie `__INITIAL_STATE__` jak w `OtodomScraper`.
- `ExternalId` = id inwestycji; cena/metraż wybrane z zakresów tak, by pasowały do filtrów profilu
  (np. najniższa cena w zakresie budżetu, metraż z przecięcia z `MinAreaSqm`/`MaxAreaSqm`).
- `Details` dla AI: pełne zakresy cen, metraży i cen za m², deweloper, adres, liczba lokali,
  odległość od centrum.
- Inwestycje **wyłączone z median** (`MarketPriceStats`), żeby zakresy nie zniekształcały
  benchmarku – wymaga oznaczenia rodzaju rynku (np. pole `IsPrimaryMarket`/`MarketType` na `HouseListing`
  + migracja) i etykiety „rynek pierwotny” w raporcie i aplikacji.
- Jedno zapytanie na stronę listy; respektować `RequestDelay` i `MaxSearchPagesPerPortal`.

### Wariant 2 – pojedyncze lokale (dokładniejszy, droższy)

- Dla każdej inwestycji pobrać listę lokali (cena, metraż, piętro, pokoje) i zwracać je jako osobne ogłoszenia.
- Wiele dodatkowych zapytań na skan; najpierw ustalić źródło danych lokali bez używania
  zablokowanych ścieżek (`/ajax/`).
- Lokale mogłyby wtedy wchodzić do median (osobno lub razem z rynkiem wtórnym – do decyzji).

### Otwarte pytania

- Czy rynek pierwotny ma być osobną opcją w profilu wyszukiwania (włącz/wyłącz)?
- Deduplikacja z Otodom/OLX, gdzie deweloperzy też publikują oferty.
- Mapowanie filtrów profilu (cena, metraż) na parametry URL portalu – czy są dozwolone przez `robots.txt`.
