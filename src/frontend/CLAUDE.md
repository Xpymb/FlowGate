# CLAUDE.md

Фронт FlowGate: SvelteKit 3 SPA (`ssr = false`, `adapter-static`, `fallback: '200.html'`, сборка в `dist/`). Команды — в
`README.md`. Бэкенд и его соглашения — `src/backend/CLAUDE.md`.

- Конфигурация SvelteKit 3 — только в `vite.config.ts` (`svelte.config.js` не поддерживается). Алиас `#lib` из
  `package.json#imports`, импорты с расширением: `#lib/api/client.js`. Ссылки — через `resolve()` из `$app/paths`.
- Svelte 5, только руны; общее состояние — `*.svelte.ts` (`$state`).
- API: `#lib/api/client.js` (openapi-fetch, пути относительные `/api/...`), `unwrap()` бросает `ApiError` с полями бэкенда
  `{errorCode, message, traceId}`. Типы — только из `schema.d.ts` (генерируется, руками не править).
- Долгие операции: бэкенд отвечает 202 + `JobDto` → `waitForJob()` из `#lib/jobs.js` (опрос `/api/jobs/{id}`).
- 401 означает «нет сессии» (отвечает nginx через `auth_request`): middleware клиента ставит `session.unauthorized`.
- Числа в DTO типизированы как `number | string` (OpenAPI .NET) — приводить через `toNumber()` / форматтеры `#lib/format.js`.
- Без UI-китов: токены и общие классы (`.btn primary|ghost|danger|sm|icon`, `.panel`, `.table`, `.form`, `.check`,
  `.segmented`, `.choices`) — в `src/app.css`, светлая и тёмная тема (`data-theme` + система, `#lib/theme.svelte.js`).
  Стили компонентов — в `<style>`. Шрифты Onest и JetBrains Mono — пакетами `@fontsource-variable/*`, без CDN.
- Ход операции у кнопки — `aria-busy={busy}` (спиннер вместо текста), не смена надписи.
- Таблицы — `<table class="table cards">`: на ширине ≤ 720 px строка становится карточкой. Главная ячейка —
  `class="primary"`, прочие — `data-label="Подпись"`; `check-cell`, `switch-cell`, `actions-cell` встают в первую строку
  карточки. Отдельных мобильных компонентов не делать. Элементы сеток с длинным содержимым — `min-width: 0`.
- Тексты интерфейса — на русском.
- Данные страницы — `new Resource(() => unwrap(api.GET(...)))` + `<Loadable resource={...}>`; действия — `attempt()` из
  `#lib/toast.svelte.js` (ошибка → уведомление); фоновые задачи — `trackJob()` из `#lib/jobs.svelte.js` (панель хода
  задачи в layout).
- Живое обновление: `new Resource(load, { live: ['dns-routes', …] })` перечитывает данные по событию `change` из
  `GET /api/events` (SSE, `#lib/events.js`). Тема — имя JSON-файла данных бэкенда без расширения, плюс `jobs` и `xray`.
  Состояние интерфейсов Keenetic в события не попадает — на «Соединениях» остаётся опрос раз в 5 с. Форму настроек
  живой не делать: событие затёрло бы несохранённые правки.
- Диалоги — `#lib/components/Dialog.svelte` (`<dialog>`, `bind:open`): `variant="side"` — панель справа для форм
  создания и правки, по центру — просмотр и подтверждения. Подтверждение — `await ask({...})` из `#lib/confirm.svelte.js`,
  нативный `confirm()` не использовать (кроме `guardUnsaved`). Подписи состояний и цвета — `#lib/labels.js`.
- Страница: `PageHeader` (заголовок, описание, `actions`), пустое состояние — `EmptyState` с основным действием, действия
  строки — `Menu` («⋯»), массовые — `Selection` + `BulkBar`. Поиск и фильтры в адресе — `UrlParam` из
  `#lib/table.svelte.js`. Формы-страницы — `SaveBar` (Ctrl+S) + `guardUnsaved()` из `#lib/unsaved.svelte.js`.
- Меню — `#lib/nav.ts` (группы Xray и Маршруты в сайдбаре и палитре Ctrl+K). Клавиша страницы — `hotkey('n', …)` из
  `#lib/hotkeys.js` внутри `$effect`. Палитра открывает формы через query: `?new=1`, `?import=1`.
- Мастер быстрой настройки — `#lib/components/setup/` (окно в layout), состояние `#lib/setup/setup.svelte.js`, конвейер
  `#lib/setup/run.js`. Ответы ничего не меняют до «Настроить»; сервер выбирается по предпросмотру
  (`/api/subscriptions/preview`, `/api/servers/preview`) и находится после сохранения по `importKey`. Шаги конвейера
  идемпотентны: «Повторить» продолжает с упавшего. Черновик — только в памяти (URL подписки с токеном).
- `resolve()` принимает маршрут с query: `resolve(`/routes?${query}`)`.
