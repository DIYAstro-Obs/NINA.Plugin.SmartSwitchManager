# Changelog

All notable changes to Smart Switch Manager are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.9.2.0] - 2026-11-04

This release combines all changes since 0.9.1.6. The interim versions 1.0.0.0 and 1.0.1.0 were
withdrawn and never became part of a final release.

**Important - read before updating:** the plugin has a new, unique identifier. The old identifier was
also used by the GuidingAnalyzer plugin, so N.I.N.A. showed only one of the two plugins in its plugin
manager. N.I.N.A. treats a new identifier as a different plugin, and saved switch configurations are
stored per identifier. Uninstall version 0.9.1.6 or older first, then install this version and add
your switches again.

The switching functionality itself is unchanged. This release changes how the plugin is identified,
packaged and described, plus one robustness fix in provider discovery.

### Fixed

- The plugin did not appear in the N.I.N.A. plugin manager (identifier collision, see above).
- An outdated provider DLL left in the plugin folder by an older version could be loaded instead of
  the current one and break provider discovery.

### Changed

- New unique plugin identifier (see the warning above).
- Releases are built with the N.I.N.A. plugin template tooling. The archive is named
  `NINA.Plugin.SmartSwitchManager.<version>.zip` and no longer contains the WebView2 files, which
  N.I.N.A. provides itself.
- The change log is now a Markdown file, so the N.I.N.A. plugin manager can display it.
- Plugin tags now use the same terms as other plugins (for example Automation, Sequencer, Smart Plug,
  Power Management, Remote Observatory), so the plugin shows up in the plugin manager's tag filter.
- Documentation: corrected the manual installation path and described the extra repository.
  Every release now publishes a `manifests.json` for it.

## [0.9.1.3] to [0.9.1.6] - 2026-03-31 to 2026-04-01

### Changed

- Release automation and documentation sync only, no functional changes.

## [0.9.1.2] - 2026-03-31

### Fixed

- Options page: labels were unreadable (black on a dark background).

## [0.9.1.1] - 2026-03-26

### Added

- Home Assistant: a second provider that runs a script or timer entity and supports hardware timers.

### Fixed

- Home Assistant: fixed HTTP 400 errors by sending standard payloads and script variables separately.
- Home Assistant: a missing `UseSSL` or `Port` setting no longer causes problems.
- Self-signed SSL certificates of local devices are accepted.
- Boolean options on the options page were missing their labels.

## [0.9.1.0] - 2026-03-22

### Changed

- Tasmota: removed the unreliable rule/PulseTime based hardware timers.
- Tasmota: error responses are now detected even when the device answers with HTTP 200, and the command
  is retried.

## [0.9.0.5] to [0.9.0.9] - 2026-03-21

### Added

- Tasmota: one-shot hardware timers based on rules (removed again in 0.9.1.0).

### Changed

- Sequencer: a normal toggle waits until the device confirms the new state. A switch with a hardware
  timer returns as soon as the device has acknowledged the command.

### Fixed

- N.I.N.A. kept offering an update because the plugin reported a wrong version.
- Tasmota: more reliable switching (a single Backlog command, strictly sequential requests, and no HTTP
  keep-alive, which caused 10 second timeouts after the device had been idle).

## [0.9.0.0] - 2026-03-21

Initial release with Shelly (Gen 1, Gen 2/3), Tasmota, Home Assistant and ESPHome providers, device
scanners for Shelly and Tasmota, and the Smart Switch Set sequencer instruction.
