// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

// Patterns match both the @core/@shared/@features aliases and relative paths.
const featureImports = {
  regex: '(^|/|@)features/',
  message: '只有 app 根目錄與 core/layout 可以引用 features；共用程式放 shared 或 core。',
};
const apiClientImports = {
  regex: '(^|/|@)core/api/api-client$',
  importNames: ['ApiClient'],
  message: '後端呼叫集中在功能的 *-api.ts；這裡改用該服務。',
};
const aliasMessage =
  '跨進 core、shared 或其他功能時用 @core/、@shared/、@features/ 別名；同一區內才用相對路徑。';
const relativeAreaImports = {
  regex: '^\\.{1,2}/(\\.\\./)*(core|shared|features)/',
  message: aliasMessage,
};
// 模組根目錄下 `depth` 層的檔案，多一個 `../` 就離開模組；跨模組（含跨區）一律改用 @features 別名。
const crossFeatureImports = (depth) => ({
  regex: `^(\\.\\./){${depth + 1},}[^.]`,
  message: aliasMessage,
});
// features/<區>/<模組>/ 下：模組根目錄、能力子資料夾，或再下一層。
const featureDepths = [0, 1, 2].map((depth) => ({
  depth,
  files: [`src/app/features/*/*/${'*/'.repeat(depth)}*.ts`],
}));

module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/component-selector': [
        'error',
        [
          { type: 'element', prefix: 'nx', style: 'kebab-case' },
          { type: 'attribute', prefix: 'nx', style: 'camelCase' },
        ],
      ],
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'nx', style: 'camelCase' },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { ignoreRestSiblings: true, argsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/no-unused-expressions': ['error', { allowTernary: true }],
      'no-empty': ['error', { allowEmptyCatch: true }],
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-signals': 'error',
      '@angular-eslint/prefer-output-emitter-ref': 'error',
      // Attribute directives namespace their extra inputs after the selector.
      '@angular-eslint/no-input-rename': [
        'error',
        { allowedNames: ['readerAttachment', 'readerPage', 'readerShare', 'rowEnabled'] },
      ],
    },
  },
  {
    files: ['src/app/**/*.ts'],
    rules: { 'no-restricted-imports': ['error', { patterns: [relativeAreaImports] }] },
  },
  // Folder boundaries: shared is reusable without any feature, core holds app-wide singletons and only the
  // layout shell composes features, and features reach the backend through their own *-api.ts services.
  {
    files: ['src/app/shared/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        { patterns: [featureImports, apiClientImports, relativeAreaImports] },
      ],
    },
  },
  {
    files: ['src/app/core/**/*.ts'],
    ignores: ['src/app/core/layout/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [featureImports, relativeAreaImports] }],
    },
  },
  ...featureDepths.flatMap(({ depth, files }) => [
    {
      files,
      rules: {
        'no-restricted-imports': [
          'error',
          { patterns: [relativeAreaImports, crossFeatureImports(depth)] },
        ],
      },
    },
    {
      files,
      ignores: ['src/app/features/**/*-api.ts'],
      rules: {
        'no-restricted-imports': [
          'error',
          { patterns: [apiClientImports, relativeAreaImports, crossFeatureImports(depth)] },
        ],
      },
    },
  ]),
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'error',
      '@angular-eslint/template/eqeqeq': ['error', { allowNullOrUndefined: true }],
      // Shared custom controls carry their own accessible name.
      '@angular-eslint/template/label-has-associated-control': [
        'error',
        {
          controlComponents: [
            'nx-select',
            'nx-tree-select',
            'nx-search-field',
            'nx-person-picker',
            'nx-date-time-picker',
            'nx-checkbox',
          ],
        },
      ],
      // NavigationEntry renders the icon and name into its host and sets aria-label itself.
      '@angular-eslint/template/elements-content': ['error', { allowList: ['nxNavigationEntry'] }],
      // Used only inside <dialog>, where it is the standard way to place initial focus.
      '@angular-eslint/template/no-autofocus': 'off',
      '@angular-eslint/template/prefer-self-closing-tags': 'error',
    },
  },
);
