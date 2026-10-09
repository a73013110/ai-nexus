// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

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
      '@typescript-eslint/no-unused-vars': ['error', { ignoreRestSiblings: true }],
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
