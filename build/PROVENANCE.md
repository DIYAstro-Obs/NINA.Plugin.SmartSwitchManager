# Provenance of the release tooling

The scripts and workflows in this repository come from the official N.I.N.A. plugin template:

- Source: https://github.com/isbeorn/nina.plugin.template (`templates/nina-plugin`)
- Revision: `4c90fac4ecbe0e179c16a16a5103d28c69407dd5`
- Fetched: 2026-11-04

`build/vendor/` is the template's vendored copy of the official manifest tooling (see `build/vendor/PROVENANCE.md` there). It is unmodified.

## Files taken unchanged

`build/Stage-Plugin.ps1`, `Package-Plugin.ps1`, `Deploy-Plugin.ps1`, `Release-Settings.ps1`, `Verify-ReleaseArtifacts.ps1`, `Submit-Manifest.ps1`, `.github/dependabot.yml`, `local.props.example`

## Deliberate differences from the template

This plugin targets the N.I.N.A. 3.2 baseline (net8) and keeps its multi-project layout (Core plus provider projects), so the following was adapted. Review these when pulling in a newer template revision.

| File | Difference |
| --- | --- |
| `Plugin.props` | Filled with this plugin's metadata. `MinimumNinaVersion` is `3.2.0.9001` instead of the template's 3.3 baseline. |
| `Directory.Build.props` | Keeps `net8.0-windows7.0`, `NINAVersion` and the x64 platform (the solution maps Any CPU to x64). Template: net10, `NinaPackageVersion`. |
| `global.json` | SDK 8.0.4xx instead of 10.0.4xx. |
| `build/Plugin.targets` | Also packages `NINA.Plugin.SmartSwitchManager.Core.dll` and auto-discovers `Providers/*/*.csproj` into `Backends/<Name>/`. Host-provided assemblies (WebView2 etc.) are deliberately not packaged. |
| `build/Build-Release.ps1` | Uses the existing `.sln` and names the plugin project explicitly instead of expecting one `.slnx` and one project under `src/`. |
| `.github/workflows/release.yml` | .NET 8 SDK, manifest-PR job enabled, plus the step "Create repository manifest array" that publishes `manifests.json` (a JSON array) so the plugin can be added as an extra repository before it is listed centrally. |
| `.github/workflows/ci.yml` | .NET 8 SDK, `.sln`, explicit plugin project. |
