// Public API of @duolingo/learning. Slice 1 exposes ONLY the routes — the store, repository,
// mapper, and domain types stay private to the library (structural boundary ≈ project references).
export { LEARNING_ROUTES } from './lib/learning.routes';

// The composition root (shell) must supply the API base URL; expose the token for that purpose.
export { API_BASE_URL } from './lib/data/api-base-url.token';
