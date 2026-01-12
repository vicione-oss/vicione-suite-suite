# Suite Reset Process Summary

This document outlines the sequence of operations performed during a suite reset, including interaction with HostManagement, shutdown and restart behavior, and file system cleanup.

```mermaid
flowchart TD
    A[Start Reset Process] --> B[Send reset command to HostManagement]
    B --> C{Success?}
    C -- Yes --> D[Write reset-flag file]
    D --> E[Shutdown Suite]
    E --> F[Suite restarted by system]
    F --> G{Reset flag exists?}
    G -- Yes --> H[Clear workspace cache]
    H --> I[Clear workspace home]
    I --> J[Clear backup files]
    J --> K[Delete module manifest]
    K --> L[Delete data version info]
    L --> M[Write final reset file]
    C -- No --> N[Abort Reset Process]
```
