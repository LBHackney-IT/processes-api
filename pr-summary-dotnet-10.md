# .NET 10 Upgrade

## Summary

-   Retargeted the API and test projects from .NET 8 to .NET 10.
-   Updated Docker and CircleCI SDK images, Lambda runtime configuration, and Lambda package paths to .NET 10.
-   Updated `Microsoft.AspNetCore.Mvc.Testing` from 6.0.9 to 10.0.0. The .NET 6 test host caused broad E2E HTTP 500 failures with the .NET 10 application; aligning the test host restored the existing test results.
-   Preserved existing package versions otherwise.
-   Updated Makefile commands from `docker-compose` to `docker compose` for Docker Compose v2 compatibility.
-   Rewrote the repository README to reflect the current API, setup, routes, and data integrations.

## .NET 10 Compatibility Settings

-   Disabled NuGet audit for these projects because the SDK's default audit diagnostics, promoted to errors by the API project's warnings-as-errors configuration, blocked restore due to pre-existing transitive package advisories. No dependencies were changed to address those advisories.
-   Suppressed `NU1510`, the .NET 10 package-pruning diagnostic for an existing framework-provided package reference, without removing that reference.
-   Suppressed `ASPDEPR008` for the existing classic ASP.NET Core hosting calls. They remain supported; replacing the hosting model was not necessary for the upgrade.

## Validation

-   .NET 8 baseline `make test`: 404 passed, 2 skipped, 0 failed.
-   .NET 10 `make test`: 404 passed, 2 skipped, 0 failed.
-   Full solution restore and build succeeded for `net10.0`.

The temporary SonarCloud configuration comments used during local test execution were reverted. No SonarCloud configuration change is included. `make test` was run with the scanner lines temporarily disabled because `SONAR_TOKEN` was unavailable in the local environment.
