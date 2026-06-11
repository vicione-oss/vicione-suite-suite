import xoTypeScript from 'eslint-config-xo-typescript';

// https://github.com/xojs/xo/issues/798
const xoTypeScriptPatched = xoTypeScript.filter(config => config.language?.startsWith('json/') !== true);

export default [
    {
        ignores: [
            '**/wwwroot/_framework/*.js',
            '**/wwwroot/js/*.js',
            '**/bin',
            'src/Blazor.Shared/Scripts/*.js',
            'src/Blazor.Server.Backend/**/*Passkey*.razor.js',
            'src/Blazor.Shared/**/*Passkey*.razor.js',
            'tests/**/*.js',
            '**/*.d.ts',
            '.nuget/**' // Ignore NuGet package cache (contains vendor JS files after caching implementation)
        ]
    },
    ...xoTypeScriptPatched,
    {
        languageOptions: {
            parserOptions: {
                projectService: {
                    allowDefaultProject: ['eslint.config.mjs']
                }
            }
        }
    },
    {
        rules: {
            'no-unused-vars': 'error',
            curly: ['error', 'multi-or-nest', 'consistent'],
            '@stylistic/padded-blocks': 'off',
            '@stylistic/indent': ['error', 4],
            '@stylistic/indent-binary-ops': ['error', 4],
            '@stylistic/comma-dangle': ['error', 'never'],
            '@stylistic/function-paren-newline': ['error', 'consistent'],
            '@stylistic/curly-newline': ['error', { minElements: 1 }],
            '@stylistic/object-curly-spacing': ['error', 'always'],
            '@typescript-eslint/no-empty-object-type': ['error', { allowInterfaces: 'with-single-extends' }],
            'max-params': ['warn', 5]
        }
    },

    // Linting rule adjustments for this file
    {
        files: ['eslint.config.mjs'],
        rules: {
            '@typescript-eslint/no-unsafe-assignment': 'off',
            '@typescript-eslint/no-unsafe-call': 'off',
            '@typescript-eslint/no-unsafe-member-access': 'off',
            '@typescript-eslint/naming-convention': 'off'
        }
    }
];
