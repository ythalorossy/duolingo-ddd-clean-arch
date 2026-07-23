// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const boundaries = require('eslint-plugin-boundaries');

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },
  {
    // Module-boundary enforcement (the frontend "NetArchTest"): intra-learning
    // layering (ui -> application -> data -> domain) plus cross-project rules
    // (contexts meet only via contracts; nothing imports shell).
    files: ['projects/**/*.ts'],
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'contracts', pattern: 'projects/contracts/**' },
        { type: 'shell', pattern: 'projects/shell/**' },
        { type: 'learning-domain', pattern: 'projects/learning/src/lib/domain/**' },
        { type: 'learning-data', pattern: 'projects/learning/src/lib/data/**' },
        { type: 'learning-application', pattern: 'projects/learning/src/lib/application/**' },
        { type: 'learning-ui', pattern: 'projects/learning/src/lib/ui/**' },
      ],
      // The bundled default resolver (eslint-import-resolver-node) only
      // resolves .js/.json/.node by default, so extension-less relative
      // imports of .ts files go unresolved and the boundaries rule silently
      // skips them. Widen its extensions so it also finds .ts/.tsx targets.
      //
      // Neither resolver understands the TS path aliases (@duolingo/contracts,
      // @duolingo/learning) defined in tsconfig.json's `paths` — without a
      // resolver that reads tsconfig, boundaries can't map an alias import to
      // a real file and silently skips it (imports via aliases went
      // unchecked). The typescript resolver reads `paths` from these
      // tsconfig files so alias imports resolve to their real module and get
      // policed like any other import.
      'import/resolver': {
        typescript: {
          project: [
            'tsconfig.json',
            'projects/contracts/tsconfig.lib.json',
            'projects/learning/tsconfig.lib.json',
            'projects/shell/tsconfig.app.json',
          ],
        },
        node: {
          extensions: ['.js', '.jsx', '.ts', '.tsx'],
        },
      },
    },
    rules: {
      // eslint-plugin-boundaries@7 renamed `element-types` -> `dependencies` and
      // `rules` -> `policies` (with an object-based `from`/`to` selector shape).
      // See https://www.jsboundaries.dev/docs/releases/migration-guides/v6-to-v7/
      'boundaries/dependencies': ['error', {
        default: 'disallow',
        policies: [
          // intra-learning layering: ui -> application -> data -> domain
          {
            from: { element: { type: 'learning-ui' } },
            allow: { to: { element: { types: { anyOf: ['learning-application', 'learning-domain'] } } } },
          },
          {
            from: { element: { type: 'learning-application' } },
            allow: { to: { element: { types: { anyOf: ['learning-data', 'learning-domain'] } } } },
          },
          {
            from: { element: { type: 'learning-data' } },
            allow: { to: { element: { types: { anyOf: ['learning-domain', 'contracts'] } } } },
          },
          // learning-domain and contracts get no `allow` policy, so the
          // `default: 'disallow'` blocks every import out of them.
          // shell may reach libraries (via their public-api); libraries may not reach shell
          {
            from: { element: { type: 'shell' } },
            allow: {
              to: {
                element: {
                  types: {
                    anyOf: ['learning-ui', 'learning-application', 'learning-data', 'learning-domain', 'contracts'],
                  },
                },
              },
            },
          },
        ],
      }],
    },
  },
]);
