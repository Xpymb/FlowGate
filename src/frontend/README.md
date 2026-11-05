# FlowGate — фронт

SPA на SvelteKit 3 + Svelte 5 + TypeScript (`adapter-static`, режим SPA). На роутере статику из `dist/` и `/api` отдаёт
свой экземпляр nginx (`packaging/files/nginx.conf`), бэкенд слушает только `127.0.0.1:8091`.

```bash
npm ci
npm run dev        # http://localhost:5173, /api проксируется на http://localhost:6000 (dotnet run, профиль http)
npm run check      # svelte-check
npm run lint       # prettier + eslint
npm run build      # dist/: 200.html + .gz для gzip_static (.br удаляются)
npm run gen:api    # src/lib/api/schema.d.ts из api/openapi.json
```

Другой адрес бэкенда в разработке: `FLOWGATE_API=http://192.168.1.1:8091 npm run dev` (через SSH-туннель).

Схема API (`api/openapi.json`) — снимок `/openapi/v1.json` бэкенда, его обновляет `OpenApiSnapshotTests`
(см. `src/backend/CLAUDE.md`). CI падает, если снимок или `schema.d.ts` устарели.
