# Duolingo Web (Angular)

Frontend for the Duolingo DDD / Clean-Architecture learning project — built as a **second
learning vehicle** where the backend's bounded-context boundaries are re-expressed in Angular.

Multi-project Angular workspace (Angular 21, standalone, signals, zoneless):

```
projects/
  shell/       application — routing, layout, composition root (providers)
  contracts/   library — generated DTO types (≈ BuildingBlocks/Contracts)
  learning/    library — the Learning bounded context, layered ui → application → data → domain
```

Boundaries are enforced two ways (the frontend analogue of the backend's project references +
NetArchTest): each library's `public-api.ts` is the only external surface, and
`eslint-plugin-boundaries` fails `ng lint` on a disallowed import (e.g. `ui → data`, or a context
importing a sibling).

## Prerequisites

- Node.js `^22.12` (developed on v22.22.2) and npm.
- The backend Host (this repo's `src/Host`) for live data.
- From this folder: `npm install`.

## Run

1. **Backend** — from the repo root: `dotnet run --project src/Host` (listens on `http://localhost:5225`).
2. **Frontend** — from `web/`: `npx ng serve shell` → open `http://localhost:4200`.

The catalog page loads `GET /courses` and renders Course → Unit → Lesson, with loading / error /
empty states. CORS for `http://localhost:4200` is configured on the Host.

## Regenerate the API contract types

The DTO types in `projects/contracts/src/generated/` are generated from the Host's OpenAPI document
(`openapi-typescript`) and committed. After a backend contract change, refresh them:

```bash
# 1. run the Host, then snapshot its OpenAPI document into this folder:
curl -s http://localhost:5225/openapi/v1.json -o openapi.json
# 2. regenerate the DTO types from the snapshot:
npm run generate:contracts
```

Commit the updated `openapi.json` and generated types. Never hand-edit the generated file; the
friendly aliases live in `projects/contracts/src/public-api.ts`.

## Quality gate

```bash
npx ng test contracts --no-watch \
  && npx ng test learning --no-watch \
  && npx ng test shell --no-watch \
  && npm run lint \
  && npx ng build shell
```

Unit tests run on **Vitest** (via the `@angular/build:unit-test` builder). Browser-driven
end-to-end tests (Playwright) are planned for Slice 2 (the first write flow).

> Note: `ng build shell` (the application) is the build gate. A standalone `ng build learning`
> currently fails a `rootDir` check because libraries are consumed from source via TS path aliases
> in this single workspace — that's a known, deferred trade-off; nothing in Slice 1 builds a library
> on its own.
