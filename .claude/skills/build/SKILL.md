---
name: build
description: Build the ViciOne Suite solution. Use when the user asks to build, compile, or check for build errors.
---

# Build the solution

Run a full build of the ViciOne Suite solution.

```shell
dotnet build vicione-suite.slnx
```

Note: If the repo was fully reset (e.g. `git clean -xfd`) or files in `src/Blazor.Shared/Scripts` where changed, run `npm ci && npm run build` first.
