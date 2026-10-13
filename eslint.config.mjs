import eslintConfigXo from 'eslint-config-xo';

const indent = 4;

const config = [
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
            '.nuget/**', // Ignore NuGet package cache (contains vendor JS files after caching implementation)
            '**/*.css',
            '**/*.html',
            '**/*.json',
            '**/*.md'
        ]
    },
    ...eslintConfigXo({ space: indent }),
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
            '@stylistic/indent': ['error', indent],
            '@stylistic/indent-binary-ops': ['error', indent],
            '@stylistic/comma-dangle': ['error', 'never'],
            '@stylistic/function-paren-newline': ['error', 'consistent'],
            '@stylistic/curly-newline': ['error', { minElements: 1 }],
            '@stylistic/object-curly-spacing': ['error', 'always'],
            '@typescript-eslint/no-empty-object-type': ['error', { allowInterfaces: 'with-single-extends' }],
            'max-params': ['warn', 5],

            // Files and directories are named after the .NET types and projects they belong to
            'unicorn/filename-case': 'off',

            // The monitoring chart is redrawn from live data, so none of its paths outlives a draw
            'unicorn/prefer-path2d': 'off'
        }
    },

    // Linting rule adjustments for this file
    {
        files: ['eslint.config.mjs'],
        rules: {
            '@typescript-eslint/no-unsafe-assignment': 'off',
            '@typescript-eslint/no-unsafe-call': 'off',
            '@typescript-eslint/no-unsafe-member-access': 'off',
            '@typescript-eslint/naming-convention': 'off',

            // The npm packages are declared in `src/Blazor.Shared/package.json`, the only workspace
            'import-x/no-extraneous-dependencies': 'off',
            'n/no-extraneous-import': 'off'
        }
    }
];

export default config;
