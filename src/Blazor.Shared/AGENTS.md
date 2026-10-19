# Blazor.Shared

Shared Blazor UI: layout, components, settings pages and the browser scripts every page loads. Applies in addition to `../../AGENTS.md`.

- `Scripts/{Interop,Download,Location,Boot}.ts` are bundled into `wwwroot/js/suite.js` by the `BundleSuiteScripts` target; `suite.js` is generated, never edit it
- **A new bundled script** goes into the `SuiteScript` item (`Blazor.Shared.csproj`, order matters: `Boot` stays last), into `files` of `Scripts/tsconfig.json` and into `exclude` of `tsconfig.json`. Any other `.ts` is served on its own as `wwwroot/js/<kebab-case-name>.js`
- `ViciOne.Interop`, `ViciOne.Download` and `ACX.Location` are called by name from C# and other repos (dx); renaming them is a breaking change
- Fonts come from the `@fontsource` npm packages via the `CopyFonts` target; a new weight goes there and into the Sass in `Styles/typography/fonts`
