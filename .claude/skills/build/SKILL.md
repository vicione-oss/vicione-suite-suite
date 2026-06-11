---
name: build
description: Build the ViciOne Suite solution. Use when the user asks to build, compile, or check for build errors.
---

# Build the solution

Run a full build of the ViciOne Suite solution.

```shell
dotnet build vicione-suite.slnx
```

Note: If TypeScript was changed or the repo was fully reset (e.g. `git clean -xfd`), run `npm install && npm run build` first.
