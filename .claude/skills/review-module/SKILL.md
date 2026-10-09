---
name: review-module
description: Audit a module for architecture compliance including structure, persistence, messaging, SDK boundaries, resource awareness, and cluster awareness. Use when reviewing or validating module implementations.
---

# Review a module for architecture compliance

Analyze the specified module for compliance with ViciOne Suite architecture rules:

1. **Module structure:** Verify correct project suffixes (.Backend, .Client, .Internal, .Public)
2. **Persistence:** Check that both ISqliteDbContext and IPostgresDbContext are implemented
3. **Messaging:** Verify consumers are idempotent (ADR-002), instance-dependent state uses instance-dependent consumers
4. **Consumer error handling:** every consumer complies with `AGENTS.md` → *Messaging*, i.e. ADR-004 D1, D6, D6a and *Compliance*
5. **SDK boundaries:** Ensure Client projects don't access DB directly or reference Backend projects
6. **Resource awareness:** Flag excessive allocations, unnecessary disk writes, or patterns that won't scale on Edge-S hardware
7. **Cluster awareness:** Check that features work in both standalone and master/slave modes
8. **Semantic versioning:** If Public contracts are modified, verify backward compatibility
9. **Changelog:** `CHANGELOG.md` has an entry for every change a user, operator or module developer can notice, following the *Changelog* rules in `AGENTS.md`: unreleased header, one line from the reader's view, no implementation details

Report **only actual issues found.** For each issue state: file path, line number (if applicable), severity, and a one-line description of the violation. Do not report positive findings or praise. If nothing is found, say so.
