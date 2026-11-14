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
- Без UI-китов: CSS-переменные в `src/app.css` (светлая и тёмная тема), стили компонентов — в `<style>`.
- Тексты интерфейса — на русском.
- Данные страницы — `new Resource(() => unwrap(api.GET(...)))` + `<Loadable resource={...}>`; действия — `attempt()` из
  `#lib/toast.svelte.js` (ошибка → уведомление); фоновые задачи — `trackJob()` из `#lib/jobs.svelte.js` (панель хода
  задачи в layout).
- Живое обновление: `new Resource(load, { live: ['dns-routes', …] })` перечитывает данные по событию `change` из
  `GET /api/events` (SSE, `#lib/events.js`). Тема — имя JSON-файла данных бэкенда без расширения, плюс `jobs` и `xray`.
  Состояние интерфейсов Keenetic в события не попадает — на «Соединениях» остаётся опрос раз в 5 с. Форму настроек
  живой не делать: событие затёрло бы несохранённые правки.
- Диалоги — `#lib/components/Dialog.svelte` (`<dialog>`, `bind:open`); подписи состояний и цвета — `#lib/labels.js`.
- `resolve()` принимает маршрут с query: `resolve(`/routes?${query}`)`.
