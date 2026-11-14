# Refactor dependency notes

CampusGear remains on its existing net8.0 target and EF/Identity package versions. No framework/schema upgrade is bundled into the structural refactor.

The readability pass adds a pinned **CSharpier 0.30.6 development tool** to `.config/dotnet-tools.json`. It runs during code formatting, not inside the application, and is restored with `dotnet tool restore`. Existing dotnet-ef tooling and application framework versions are retained. Layout configuration and usage are documented in [CODE_STYLE.md](CODE_STYLE.md).

The reference uses AutoMapper 14.0.0. Restore reported high-severity recursion advisories. This implementation selects AutoMapper 15.1.4, a patched release that targets .NET 8. It is used only for a declared flat category read-model mapping; Identity, roles and reservation state writes remain explicit.

Sources: [patched release/project](https://github.com/LuckyPennySoftware/AutoMapper/tree/v15.1.4), [advisory and patched versions](https://github.com/advisories/GHSA-6vhm-j9h7-f9gh).

AutoMapper 15.1.4 is governed by RPL 1.5 or a commercial license; see the [package license](https://github.com/LuckyPennySoftware/AutoMapper/blob/v15.1.4/LICENSE.md). No commercial license, account or key has been obtained on the user's behalf. Before distributing the application, the owner must choose/meet the applicable terms. Startup accepts an optional operator-owned AutoMapper:LicenseKey through configuration and does not suppress license diagnostics. Never commit a key.

This note does not change the project's own license or publish its source. Local refactor/test completion is not a production-distribution approval.

The final `dotnet list CampusGear/CampusGear.sln package --vulnerable --include-transitive` scan reported no vulnerable packages in the four production projects. The existing integration-test project still brings in `System.Net.Http` 4.3.0 ([advisory](https://github.com/advisories/GHSA-7jgj-8wvc-jh57)) and `System.Text.RegularExpressions` 4.3.0 ([advisory](https://github.com/advisories/GHSA-cmhx-cq75-c4mj)), both marked high severity. These are test-only transitive dependencies; no unrelated test framework upgrade was included. The scan reflects the configured NuGet sources at verification time.
