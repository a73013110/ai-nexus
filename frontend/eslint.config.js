// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

const featureImports = {
  regex: '(^|/)features/',
  message: '只有 app 根目錄與 core/layout 可以引用 features；共用程式放 shared 或 core。',
};
const apiClientImports = {
  regex: '(^|/)core/api/api-client$',
  importNames: ['ApiClient'],
  message: '後端呼叫集中在功能的 *-api.ts；這裡改用該服務。',
};

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
  // Folder boundaries: shared is reusable without any feature, core holds app-wide singletons and only the
  // layout shell composes features, and features reach the backend through their own *-api.ts services.
  {
    files: ['src/app/shared/**/*.ts'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [featureImports, apiClientImports] }],
    },
  },
  {
    files: ['src/app/core/**/*.ts'],
    ignores: ['src/app/core/layout/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [featureImports] }],
    },
  },
  {
    files: ['src/app/features/**/*.ts'],
    ignores: ['src/app/features/**/*-api.ts'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [apiClientImports] }],
    },
  },
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
          controlComponents: ['nx-select', 'nx-search-field', 'nx-date-time-picker', 'nx-checkbox'],
        },
      ],
      // Used only inside <dialog>, where it is the standard way to place initial focus.
      '@angular-eslint/template/no-autofocus': 'off',
      '@angular-eslint/template/prefer-self-closing-tags': 'error',
    },
  },
);
