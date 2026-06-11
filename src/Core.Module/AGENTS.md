# Core.Module

Manages dynamic module loading, assembly resolution, dependency validation, and artifact repository access.

- Custom `AssemblyLoadContext` for isolated module loading — modules run in separate contexts
- Validates module SDK version compatibility before loading; mismatches become `StartupErrors`
- Performs signature validation on module assemblies (minisign)
- Module naming suffixes enforced: `.Backend`, `.Client`, `.Internal`, `.Public` (see `Constants.cs`)
- No platform-specific code — pure library; references Core.Artifacts and Core.UiHosting only

