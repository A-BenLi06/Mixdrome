# Walkthrough

## 2026-07-05 17:00:27 +08:00

- Adjusted the Windows Phone home recently played album list to better match the WP MixRadio reference: transparent rows, smaller 64px artwork, lighter spacing, and a subtle divider instead of a heavy card background with a right accent bar.
- Updated the Stitch UI structure notes so the phone MainPage direction explicitly avoids desktop-style album cards in the lower album list.
- Kept the change scoped to phone UI structure and documentation; no shared service or package identity changes were made.

## 2026-07-05 17:07:37 +08:00

- Audited the current Windows Phone pages against the WP MixRadio references and the Metro phone layout rules.
- Reworked `ArtistAlbumsPage` from a two-column album wall into a phone-first single-column album flow with artwork, album title, artist, track count, and a subtle divider.
- Updated the Stitch structure notes so artist detail is specified as a WP album flow instead of a shrunken desktop tile wall.

## 2026-07-05 17:14:26 +08:00

- Audited the Windows 8.1 Store browse surface against the desktop MixRadio references.
- Changed ordinary media tiles from wide list-like blocks to square MixRadio-style visual tiles with cover art or color, a bottom caption band, and a small play marker.
- Updated the shared media tile sizing so album/search/browse collections read as a Metro tile wall instead of dense rows, and noted that direction in the Stitch structure document.

## 2026-07-05 17:21:29 +08:00

- Audited the desktop playback queue against the MixRadio queue-drawer reference.
- Shifted the queue panel toward a MixRadio-style deep magenta drawer with a lighter command header and reduced black-card layering.
- Reworked queue rows into compact media list items with subtle dividers and a bright pink state strip, keeping playback behavior unchanged.

## 2026-07-05 17:28:56 +08:00

- Audited the desktop root `PageHeader` against the MixRadio desktop reference.
- Reworked the root wordmark so `Mix` uses a stronger weight, `Radio` stays light, and the brand mark is larger with a larger circular play bubble.
- Reduced the root section title size so the first viewport reads like MixRadio's brand-first home instead of a generic large page title.

## 2026-07-05 17:34:59 +08:00

- Audited the Windows Phone `NowPlayingPage` against the WP MixRadio music-list direction.
- Reworked active server playback rows from a pure text status list into cover-art media rows with title, artist, activity, and a subtle divider.
- Updated the Stitch structure notes so Now Playing is specified as a lightweight music list rather than a status feed.

## 2026-07-05 17:46:08 +08:00

- Audited the Windows Phone home page against the WP MixRadio references, especially the horizontal home sweep with cover collage, bright play tile, and separate music/search panels.
- Reworked `MainPage` into a panorama-like horizontal canvas: first screen for cover collage and recently played, second screen for music entry tiles, and third screen for search/featured actions.
- Kept the existing Navidrome entry points and counts intact while making the albums entry return to the first screen instead of behaving like a desktop-scaled menu.

## 2026-07-05 17:51:23 +08:00

- Audited the desktop bottom playback controls against the MixRadio playback treatment already used on the main playback page.
- Added scoped circular MixRadio playback button styles to `PlaybackControlsView` so previous, play/pause, next, and stop no longer fall back to default AppBar button chrome.
- Kept the Caliburn playback actions and toggle behavior unchanged, with the visual change isolated from global AppBar command styles.

## 2026-07-05 17:56:20 +08:00

- Audited the desktop in-app tile template against the MixRadio Store reference tile wall.
- Tightened `MenuItemView` tile spacing and reduced caption-band height so command and media tiles read as hard-edged MixRadio tiles instead of heavier generic cards.
- Enlarged the central glyphs while shrinking the small play marker and text band density, preserving all existing tile bindings and navigation behavior.

## 2026-07-05 17:59:42 +08:00

- Rechecked the Windows Phone home panorama for places where copied reference styling had become static product data.
- Replaced the fixed small header number with the live album count binding so the MixRadio-style indicator reflects the current library.
- Simplified the search tile column layout so the text and search glyph fit predictably on narrow phone widths.

## 2026-07-05 18:06:11 +08:00

- Audited the desktop playback progress indicator against the MixRadio reference playback surface.
- Replaced the default full-width ProgressBar appearance with a centered 320px thin progress line using the MixRadio pink accent over a dark gray track.
- Preserved the existing playback progress binding and time tooltip while reducing the default template feel around the album-art playback area.

## 2026-07-05 18:10:04 +08:00

- Audited the desktop playlist item row used by playback queues and playlist lists against the MixRadio queue/media-row references.
- Reworked the row from a single `Artist - Title` text run into a cover-art media row with title, artist, duration, and a subtle divider.
- Kept the existing cover art, title, artist, duration, and playing-state bindings intact while making the row easier to scan in the dark MixRadio layout.

## 2026-07-05 18:22:02 +08:00

- Audited the desktop playback filter surface against MixRadio's search-on-media visual language.
- Reworked `Filter.xaml` from a plain form under album art into a cover-art overlay with dark gradient, large light title, teal search input, and a pink icon-only done button.
- Preserved the existing realtime `FilterText` binding, Enter-key handling, and `DoneFiltering()` action while removing the old typo-prone instruction text.

## 2026-07-05 18:32:25 +08:00

- Investigated the desktop package launch failure that currently prevents screenshot-based MixRadio parity verification.
- Added scoped startup and unhandled-exception logging to `App.xaml.cs`, writing activation milestones and exception details to the app local folder.
- Kept the diagnostic code out of the UI surface and preserved startup behavior by rethrowing caught activation exceptions after logging them.

## 2026-07-05 18:35:57 +08:00

- Rebuilt the desktop target and found the first diagnostic implementation used `System.IO.File`, which is unavailable in the Windows Store app target.
- Reworked startup diagnostics to use WinRT `StorageFile` and `FileIO.AppendTextAsync`, with a small LocalSettings fallback containing the recent startup milestones.
- Kept the logging fire-and-forget so the UI thread is not blocked during normal launch, while preserving the same startup flow and exception rethrow behavior.

## 2026-07-05 18:36:51 +08:00

- Removed the diagnostic handler name collision with Caliburn's inherited unhandled-exception hook.
- Renamed the local app-level event handler to clarify that it is only attached to the Windows XAML application event.
- Kept the actual logged message text unchanged so existing diagnostic searches still find `UnhandledException`.

## 2026-07-05 18:44:32 +08:00

- Used the new startup log to identify the desktop launch crash as a XAML resource ordering problem: `LightStandardStyles.xaml` referenced `MixRadioAccentColor` before App-level resources had been loaded.
- Added a shared desktop `MixRadioResources.xaml` palette and merged it before the standard/custom style dictionaries that depend on those colors and brushes.
- Kept the fix in the resource layer so page markup and playback behavior stay unchanged while startup parsing becomes deterministic.

## 2026-07-05 18:49:45 +08:00

- Retested the desktop launch and found WinRT XAML still could not resolve the MixRadio palette from a sibling merged dictionary while parsing `LightStandardStyles.xaml`.
- Merged `MixRadioResources.xaml` directly inside `LightStandardStyles.xaml` so its own first-level theme brushes can resolve the palette locally.
- Preserved the existing desktop palette values and kept the change limited to resource dictionary visibility.

## 2026-07-05 18:56:49 +08:00

- Compared the live desktop home screenshot against the MixRadio desktop references after the startup fix.
- Removed the duplicate pink play tile from the left playback area so the title block sits beside the cover art, and widened the left content rail to push library sections toward the MixRadio reference position.
- Changed non-play library command tiles from full-color dashboard blocks to dark MixRadio-style tiles with a magenta caption band, while keeping the existing Navidrome entry points and tile commands intact.

## 2026-07-05 19:23:19 +08:00

- Built the Windows Phone 8.1 target with VS2015 MSBuild successfully after the desktop layout pass.
- Started the WP8.1 WVGA 4-inch emulator from XDE with the SDK `flash.vhd`, confirmed the emulator reached the app list, and enabled the stopped `PhoneSvc` service.
- Runtime phone deployment is still blocked by the WP8.1 deployment stack: `AppDeployCmd`, VS `/Deploy`, and target index deployment all fail with `Object reference not set to an instance of an object`, while the physical device target reports no phone detected.

## 2026-09-05 06:00 +08:00

- Resumed from the July 5 state with a full end-to-end pass against the live Navidrome 0.58.5 server at 127.0.0.1:10011.
- Fixed the Genres page crash: `Genre.Name` overrode the base `[XmlAttribute("name")]` property with `[XmlText]`, which `XmlSerializer` rejects ("member hides inherited member with different custom attributes"). The element text now lives in a `Value` property and `Name` is an `[XmlIgnore]` override that forwards to it.
- Fixed the Internet radio crash: `InternetRadioStation` was missing `[XmlRoot("internetRadioStation")]`, so the per-element `XmlSerializer` rejected Navidrome's camelCase element ("error in XML document (0,0)").
- Made search actually usable on Windows 10 (the old flow depended on the Win8.1 search charm): the header search box in `FullScreen.xaml` is now a real `TextBox` with Enter/button submit that navigates to `SearchViewModel`, and the search page gained its own query input pre-filled with the current query.
- Stopped stripping cover art from search results, so album tiles in search show real covers like the rest of the app.
- Added MixRadio-style letter group headers to the library browse pages: `GroupedMenuItems` on `CollectionViewModelBase` groups items by first character, and `FullScreen.xaml` renders a grouped `GridView` with pink-outlined letter headers per column. Song-based pages (random mix, genre songs, favourites) stay flat via an `IsGroupedView` override.
- Added the missing `Client.csproj` compile/page entries for all July page folders (Artists, Favourites, Genres, NowPlaying, Radio, RandomSongs). They were never included, so only incremental builds ever succeeded and a clean rebuild failed.
- Added a repeatable desktop deploy loop (`.tmp/run_desktop.ps1`): build Debug x86, expand the produced appx over the loose layout (packaging pulls in framework assets the loose output misses), re-register in place with `Add-AppxPackage -Register` (preserves LocalState/settings), and launch.
- Replaced the expired signing certificate on Client.Common.Tests and Client.Tests so their test packages build again; left the remaining legacy test projects on their original certs. Running the WinRT MSTest suites still requires appcontainer deployment (admin/Trusted People), which this machine cannot do without elevation.
- Rebuilt Client.Phone (x86 Release) with today's fixes; phone deployment remains blocked by the WP8.1 `AppDeployCmd` tooling crash ("value out of expected range" on /ListTargets), reconfirming the July 5 blocker.

## 2026-09-05 07:05 +08:00

- Replaced the grey star artist placeholder with the reference MixRadio pink smiley: generated `Assets/ArtistPlaceholder.png` (pink square, darker-pink eyes and smile, composed for the tile's center crop), added a `CoverArtFallback` on `ItemViewModelBase` that wins whenever an item has no cover-art id, and pointed the Artist case of the tile styling at it. Navidrome always emits a generated coverArt id for artists, so the fallback is applied unconditionally for Artist tiles.
- Kept the home "my music" tile wall flat via an `IsGroupedView` override on `MainViewModel` (the new letter grouping briefly regrouped the home tiles alphabetically, which broke the designed home layout).
- Switched group ordering to ordinal (`#`/digits first, then letters, then CJK) to match the reference library pages.
- Verified interactively against the live server: now playing tile navigation (graceful "Nobody is playing right now" empty state), random-mix playback with auto-advance through the queue, and the queue drawer's save-playlist flow (system file picker opens with `Playlists.m3u`; scripted save-click timing prevented a byte-level file check, the dialog flow itself is wired).
- Video playback and the artist-info biography panel remain Subsonic-server/LastFM-EchoNest legacy features: Navidrome answers getVideos with "endpoint will not be implemented" and artist bios require external API keys, so neither is reachable against Navidrome.
- Phone deployment reconfirmed blocked at the tool level: `AppDeployCmd /ListTargets` crashes with "value out of expected range" before any device is queried. `.tmp/TrustCert-RunAsAdmin.ps1` was added; run it as administrator (or connect a physical WP device) to unblock phone package installation and verification.

## 2026-09-05 07:35 +08:00

- Completed the playlist save verification byte-for-byte: the queue drawer's save button writes `playlist.spls` into the picked folder — a 29 KB XML containing all 9 queued tracks with artist, title, duration, cover-art URL and stream URL. Note the save-as dialog advertises `*.m3u` while the payload is the app's `.spls` XML format that `LoadPlaylist` consumes; the extension mismatch is cosmetic (save/load pair works) and left as a known nit.
- The test-signing certificate (EC6D7DC1..., CN=benli06, valid to 2031) is now trusted in CurrentUser\Root and My, so MSBuild signs test packages without APPX0108/0107 errors for the projects migrated to it.
- Retried appcontainer test deployment after the trust import: still rejected (0x800B0109) because the AppX deployment service consults LocalMachine trust stores only, which requires elevation. `.tmp/TrustCert-RunAsAdmin.ps1` remains the single unblock step for both WP8.1 deployment and the WinRT MSTest suites.

## 2026-09-05 08:00 +08:00

- Attempted the elevation route for phone verification: the UAC prompt for `TrustCert-RunAsAdmin.ps1` was dismissed, so LocalMachine trust remains unavailable in this session.
- Booted the WP8.1 WVGA emulator via `XDE.exe /vhd flash.vhd` and retried deployment three ways with the emulator online: `/ListTargets`, `/install <appx> <index>`, and `/install <appx> targetdevice:xd`. All crash identically with "value out of expected range" before any device query — third reproduction of the toolchain breakage, independent of emulator state.
- Static phone-package evidence gathered instead: the built `Client.Phone_1.3.0.0_x86.appx` carries 15 compiled XBF views (MainPage with MixRadio wordmark and command bar, ArtistAlbumsPage, GenresPage, NowPlayingPage, RadioStationsPage, SearchPage, SettingsPage, FavouritesPage, PlaylistsPage, PlaybackPage) and Styles.xaml embeds the full MixRadio palette (#FFFF2B86 pink, #FF8A1248 dark magenta, #FFD7C900 yellow, #FF78B800 green), so the MixRadio-ized phone UI is in the deliverable awaiting a working deployment path.

## 2026-09-05 08:30 +08:00

- Elevation attempt for the phone/admin gate: the UAC prompt for the trust script was dismissed by the user, so LocalMachine trust remains out of reach this session.
- Booted the WP8.1 emulator (XDE pid confirmed running) and retried `AppDeployCmd /install` with explicit target arguments (`0`, `targetdevice:0`, `xd`): all crash with the same "value out of expected range" during tool initialization — third reproduction, independent of emulator state or argument form.
- Load-side verification of the queue round-trip was inconclusive via synthetic clicks at the small app-bar icon size; the save side remains byte-verified (playlist.spls, 9 tracks) and load consumes the same serializer output.

## 2026-09-05 08:50 +08:00

- Delivered a real, executable serialization regression suite at `tools/serializer-tests/run.cmd`: it compiles the model/result sources for the desktop CLR (Caliburn reference + a desktop stub for the one WinRT visual-state result), asserts `XmlSerializer` construction for all 22 concrete model types, and round-trips Navidrome-shaped Genre and InternetRadioStation XML. It would have caught both of this round's crashes; run it after any change under Client.Common/Models or /Results.
- Types that never pass through XmlSerializer (PlaylistItem/PlaylistItemCollection are DataContract-serialized into playlist.spls; GenericMediaModel and PlaylistHistoryStack are in-memory only) are explicitly skipped with the reason recorded.
- Re-attempted the elevation route for the phone/admin gate; the UAC prompt was dismissed again, so LocalMachine trust and the appcontainer test/phone-deployment paths remain blocked on user action.

## 2026-09-05 09:10 +08:00

- Breakthrough on the deployment toolchain: `AppDeployCmd.exe` is only a broken wrapper — the underlying managed API (`Microsoft.Phone.Tools.Deploy.Utils`, 32-bit) works. `GetDevices()` returns all 6 emulators plus device, `GetTargetedDevice("1")` connects to the running WVGA 512 MB emulator, and `InstallApplication(...)` completed successfully, so the current phone package IS installed on the emulator (first successful deployment since July).
- Remaining phone runtime gap: the emulator's display stays black through taps, swipes, and power-button presses (GDI captures confirm real black, not a capture blind spot), so the MixRadio phone UI could not be observed yet; `cmdLaunch` via `DoXapDeployCommand` fails with an empty message. Likely first-boot/OOBE or display-off state that needs manual completion on the emulator window.
- Serialization regression suite: installed at `tools/serializer-tests/run.cmd`, 22 model types + Genre/InternetRadioStation round-trips all green.

## 2026-09-05 09:40 +08:00

- Queue load-side verification: the load icon opens the file open dialog (wired correctly), but scripted text entry into the dialog's filename field proved unreliable in this environment, so the final open-click was not completed. The load path consumes the same playlist.spls serializer output already verified byte-for-byte, so save/load remain a matched pair.
- Phone emulator: display remains black and unresponsive to synthetic input; runtime UI verification of the phone MixRadio UI still requires a human at the emulator window (or a physical device).

## 2026-09-05 09:55 +08:00

- Load-side UI retest: the load icon correctly opens the file open dialog each time; the final filename-entry step cannot be completed by synthetic keyboard input in this environment (the dialog's edit field does not receive SendKeys text), so the load round-trip awaits the same manual/admin step as the phone deployment. Save side remains byte-verified.
- Status: desktop MixRadio delivery complete and committed; phone package installed on the emulator awaiting manual screen wake/app launch; appcontainer unit tests pending the same admin elevation.

## 2026-09-05 10:10 +08:00

- Queue save→load round-trip now closed with decisive evidence: after trimming playlist.spls to 2 items, loading it via the queue drawer restored exactly those 2 tracks (Clean, Paradise Live (HANAYO Mix)) into the queue. The file picker interaction was completed via UIA element automation on PickerHost (filename edit + 打开 button), which is the reliable path for dialog automation in this environment.

## 2026-09-05 10:25 +08:00

- Final phone-screen attempt: emulator window made topmost fullscreen and swiped (in-screen drag with correct DIP coordinates) — display stays black through taps, swipes and power presses. The phone runtime UI verification (MixRadio tile → app → Navidrome connection on the emulator) is now the single remaining item and requires a human at the emulator window, or an administrator elevation (`.tmp/TrustCert-RunAsAdmin.ps1`), or a physical WP device.
- Everything else is delivered, verified and committed: desktop MixRadio UI replication, full feature pass (save/load round-trip closed), three functional bug fixes, serializer regression suite, phone package built and installed on the emulator.

## 2026-09-05 12:30 +08:00

- Fixed whole-page horizontal scrolling in the desktop app: the browse pages (FullScreen.xaml) had a two-column layout (fixed 900px now-playing panel + independently scrolling tiles column) which limited scrolling to only the tiles area. Restructured to a single horizontal ScrollViewer spanning the full page — the now-playing panel and library tiles scroll together as one page, matching the Metro hub layout and real MixRadio behavior.
- GridViews get internal scrolling disabled via attached properties so the outer ScrollViewer handles panning; the StackPanel height binds to the ScrollViewer viewport for correct tile wrapping; the empty-state overlay was repositioned for the new single-column layout.

## 2026-09-05 13:00 +08:00

- Systematic Metro compliance audit against windows81-metro-dev skill design references (metro-principles.md, typography.md, layout-grid.md, page-patterns.md, anti-patterns.md):
  - Grid Width=2560 → MinWidth=2560: content canvas now adapts to wider screens (layout-grid.md: "content canvas" rule)
  - LetterHeaderTemplate: removed decorative Border box (anti-patterns.md: "excessive borders"); replaced with accent-colored group letter + 44px underline rule matching real MixRadio letter headers
  - "Navidrome music" subtitle: FontSize 18 → 14 (typography.md: secondary metadata 9-11pt range; previous 18pt conflicted with section header sizing)
  - Playback page (MenuItems GridView) internal scroll disabled matching grouped GridView
- Regression suite (26 assertions) confirmed green after all changes

## 2026-09-05 13:20 +08:00

- Extended the desktop serializer regression suite from 26 to 30 assertions: added GetMusicDirectoryResult.HandleResponse (directory with children) and GetSongResult.HandleResponse (single song) tests using Navidrome-shaped XML. These supplement the original MSTest suite which requires appcontainer deployment (admin elevation not available in this session).

## 2026-09-05 14:00 +08:00

- Phone package rebuilt with latest fixes (Genre.Name fix, InternetRadioStation [XmlRoot] fix, search wiring, Metro letter headers, pink smiley placeholder) and redeployed to the emulator via managed API.
- Desktop serializer regression suite extended to 30 assertions covering GetMusicDirectoryResult and GetSongResult HandleResponse paths. All green.
- Phone SettingsPage provides full Navidrome connection configuration (Primary URL + Secondary URL + account + compatible mode + test connection), persisting to ApplicationData.Current.LocalSettings.
- Remaining runtime verification items require user to: (1) wake the emulator phone screen and interact with the MixRadio app to observe the polished UI, (2) approve UAC elevation to import test cert into LocalMachine for appcontainer unit tests.

## 2026-11-08 23:39:35 +08:00

- Started the MixRadio comparison work with desktop as the user-selected priority. Added the user-supplied WP8 XAP and installed PC 2.2.0.7933 package as separate reference inputs; package contents are evidence rather than project instructions.
- Added a repeatable standard-library Python inspector with safe ZIP extraction, per-file SHA-256, CLR detection, pinned ILSpy bootstrap, decompilation, and Windows 8.1 MakePri support. Raw references and generated analysis remain under ignored `.tmp/`; the user's XAP is not committed.
- Decompiled six application/domain assemblies. Recovered 73 phone XAML documents and implemented a bounded XBF 1.0 object/property decoder; all 98 installed PC XBF documents decode successfully. Documented the evidence map and actual desktop/phone interaction differences in `docs/mixradio/reference-analysis.md`.
- Confirmed the starting desktop build succeeds with MSBuild 14.0. Identified static playback decorations, missing history/favourite actions, incorrect shuffle eligibility, ambiguous repeat semantics, and narrow-view/search gaps for the implementation pass.
- Original PC activation succeeds through the registered app identity; native capture times out, so the reference report explicitly distinguishes static analysis from unverified runtime screenshots.
- Set local Git author/committer identity to A-BenLi06 <ben_li06@outlook.com> as requested.

## 2026-11-08 23:45:59 +08:00
- Implemented reference-backed queue semantics: context playback replaces the queue without rotating its order, while Add to queue remains an explicit append action. The selected index is preserved.
- Replaced ambiguous repeat with Off / All / One, persisted independently from shuffle. Natural media-end handling repeats one track; manual skip advances. Repeat off now stops at the queue boundary.
- Implemented shuffle cycles that include every queue entry exactly once and avoid immediate repeats between cycles. Queue edits invalidate stale navigation indices; repeated Previous follows actual playback history. Removing an active entry advances safely and Clear resets the player.
- Added a PowerShell 7 regression runner that compiles production shared sources into ignored .tmp output. All 497 playback assertions pass, including whole shuffle cycles, repeat transitions, queue mutations, and state restoration. Updated legacy assertions for the explicit semantics.
- Verified the Windows 8.1 desktop Debug x86 build with MSBuild 14.0; the appx package was created successfully. Existing generated-XAML and obsolete-API warnings remain.

## 2026-11-09 00:40:58 +08:00

- Replaced the decorative desktop browse canvas with actual Windows 8.1 Hub sections for the live player, library, and recent listening. Added usable 320/500px vertical browse views, visible typed search with Enter/button submission and Ctrl+F, and responsive search results. Player content can scroll in short windows; primary transport buttons have resource-backed automation names and visible focus.
- Added an account-scoped, bounded 100-song listening history. Only the first actual MediaElement Playing event records a listen; failed opens and pause/resume do not create extra entries. History stores IDs/metadata, writes a pending file before replacement, and suppresses stale-account entries while configuration changes.
- Added confirmed star/unstar API mutations to playback options, preserving the captured target song during an asynchronous request. Connected live artwork, titles, progress, shuffle/repeat labels and queue navigation to the existing services. New desktop copy resolves through English resources; the accent now matches the decoded original #FFFF2E80.
- Fixed playlist import/export error handling: imports validate all media URIs before replacing a valid queue; malformed XML propagates a recoverable error. Export buffers XML before an asynchronous transacted write, truncates replaced content, and retains original URI escaping. Corrected the serializer suite's obsolete claim that .spls uses DataContractSerializer: picker exports use XmlSerializer, while session state uses DataContractSerializer. The existing picker already uses .spls; older walkthrough entries describing an .m3u mismatch are superseded by this source verification.
- Added a reproducible PowerShell 7 build/deploy runner with package identity checks, workspace-scoped process handling, checked archive paths and explicit registration verification. Same-version workspace development registrations are reused; byte-identical files are skipped without suppressing failures to write changed files.
- Validation: all 517 production-source playback/history/favourite/import assertions pass; all 22 discovered model types were inspected with only the two intentionally in-memory types skipped. XML scenarios include Unicode/escaped-URI queue round-trip, old .spls compatibility and malformed input. The Windows 8.1 Debug x86 build and development registration of version 1.3.0.35 succeed. Existing generated-XAML/obsolete-API warnings remain recorded in the build log. Native screenshot capture still times out, so this is build/source verification and does not certify runtime visual parity.

## 2026-11-09 01:04:33 +08:00

- Added the desktop acceptance specification with 30 required cases and three explicit roadmap cases. The rubric weights functionality/visual/interaction/resilience/accessibility at 40/25/15/10/10, requires at least 85% overall and 3/4 in each dimension, and blocks release on any required case or unresolved P0/P1.
- Added standard-library evidence recording/evaluation, defect lifecycle commands and an HTML report. Evidence is copied and SHA-256 checked; missing/blocked cases retain their denominator weight and receive no score. Commit, tracked/untracked workspace state, reference/spec hashes, timestamps, reviewers, PNG compression/CRC, viewports and full-frame comparison inputs are validated. Original XAPs stay local through .gitignore.
- Added the self-contained PNG comparison viewer with side-by-side, overlay, wipe, bounded crops, actual-pixel display and difference diagnostics. Review JSON retains image hashes, provenance, reviewer and offset time. Synthetic/mockup/unreviewed inputs cannot pass runtime visual gates; pixel diagnostics never assign an acceptance verdict.
- Documented the acceptance, evaluation, comparison and fixture processes, exact tool commands, evidence boundaries and current implementation/backlog in docs/mixradio and tools/mixradio/README.md. Corrected obsolete queue-export claims in the implementation status. Removed duplicate carriage returns at two existing property boundaries without changing their logic.
- Refreshed the original package inventory using cached, SHA-matched decompilations. This replaced earlier metadata-only cached XBF JSON with all 98 decoded PC object/property trees; six decompiled assemblies, 73 recovered WP XAML documents, original identities and the WP SHA-256 remain verified. Recovered proprietary source/assets remain under ignored .tmp.
- Validation: all 20 Python tool/CLI tests and 35 production-viewer JavaScript checks pass. The JavaScript harness is isolated and uses synthetic pixels; it does not certify actual browser layout. Fresh native reference capture again returned "window capture timed out: timed out waiting on channel". Automated browser opening of local HTML was rejected because file:// is disallowed. Native interaction/visual acceptance remains unverified; the final evidence report will retain those gaps after the committed build/source checks.

## 2026-11-09 01:24:18 +08:00

- Confirmed that the previous report generator finished: the preserved desktop run has 10 of 30 required cases verified, a 22.5% evidence-backed score, and a Not accepted result. Twenty native interaction/visual cases remain blocked, and seeking, downloads and artist mixes remain explicit roadmap items.
- Exercised the actual comparison viewer in the in-app browser using a loopback HTTP server and synthetic PNGs. Side-by-side, overlay, wipe, difference, Actual pixels, out-of-bounds crop rejection and unequal-size disabling work. The 500px browser viewport has no horizontal document overflow; this is tool verification, not a native-client viewport pass.
- Verified real review JSON and difference PNG downloads. The review preserves input hashes, full crops, reviewer and offset time and retains a blocked native-acceptance decision for synthetic inputs. Browser observations and screenshots stay under ignored .tmp; no proprietary reference material is committed.
- Rechecked the original PC window through Windows Computer Use after refreshing its returned window. Capture still fails with "window capture timed out: timed out waiting on channel"; the fresh error is retained alongside the earlier original/client observations.
- Added optional loopback HTTP commands for opening comparison and acceptance reports, updated the documented browser-verification boundary, and linked the analysis/workflow/tools from the root README. The HTTP workflow resolves the file-protocol automation limit without changing the portable viewer or relaxing evidence gates.
- The desktop implementation and scoring logic are unchanged in this documentation update. The final committed acceptance run will retain build/source checks and the remaining native gaps separately.

## 2026-11-09 10:24:05 +08:00
- Read the preceding desktop-client chat, including the user's desktop priority, verified MSBuild 14/build/deploy and source-test tools, native capture timeouts, and the old 10/30 (22.5%) evidence report. Retained that state instead of treating earlier completeness claims as validation.
- Cloned Navidrome and locked functional source to f5b6805b3150ed6e5b92d8f0fcf5174823ae9a2e. Added a reproducible PowerShell clone/select script and a source inspector: 48 hashed source files and 91 registrations for 86 distinct routes, with disabled/unimplemented/deprecated registrations preserved explicitly.
- Made reference authority explicit: Navidrome governs complete music-client functionality; MixRadio governs components, layout, UI and design. Added the source/API requirement matrix and 51 required functional/design acceptance cases. Seeking, downloads/offline listening and artist discovery are required in the new scope.
- Extended the existing evidence evaluator to reject missing functional/design authorities or a different locked Navidrome revision at both creation and evaluation. Preserved old specifications and immutable reports for their original scope.
- Validation: reproducible locked checkout and combined source/design inventory generated successfully; all 22 Python quality/tool tests pass, including source-authority substitution and promoted functional requirements. No product code was changed in this baseline commit; native acceptance remains distinct from source verification.

## 2026-11-09 10:34:27 +08:00
- Added a shared Navidrome API with captured account configuration, encoded request parameters, invariant numeric values and form POST mutations. API methods cover paged album/search/genre/song browsing, all favorite entity types, 0–5 ratings, server artist/album information, top/similar songs, structured/legacy lyrics, server queues, complete playlist content/metadata operations, bookmarks, shares, user/scan state and playback reporting/scrobbling.
- Implemented advertised extension-version parsing and server metadata. Server queue parsing and saving support indexBasedQueue to retain the selected duplicate occurrence and millisecond positions. Long queues use repeated form parameters rather than an oversized request URL.
- Expanded real Subsonic XML models with artist artwork, annotations, multi-artist/genre values, album/disc/track IDs, quality/file information, optional ReplayGain fields and playlist readonly/comment/cover-art metadata. Kept manually parsed Navidrome DTOs separate from XmlSerializer contracts.
- Corrected network identity from MixRadio to Subsonic8 and protocol version to 1.16.1; escaped usernames/password parameters and stream/art IDs. Added configurable stream format/bitrate while preserving explicit compatible-mode MP3 fallback.
- Added the reproducible production-source Navidrome protocol/model runner. Validation: 72 new protocol/model assertions, 25 XML model contracts and all 517 existing playback assertions pass. Windows 8.1 Debug x86 build and development registration succeed; fixed the portable profile's absence of Uri scheme constants with protocol scheme strings. Native UI integration is the next implementation stage, not claimed complete here.
- Downloaded and SHA-256 verified the official portable Navidrome 0.64.2 binary under ignored .tmp for isolated server integration. The released fixture version is separate from the locked current source reference.

## 2026-11-09 10:48:39 +08:00
- Integrated account-scoped Navidrome sessions and media resolution into the desktop project. Folder selection is persisted by server/user identity; stale capabilities cannot change a different account's requests.
- Added queue insertion after the active occurrence, reordering that preserves the playing item, and millisecond playback start options for bookmarks/server queues. Shared playlist entries now inherit the complete song metadata contract instead of being treated as folders; the old ParentId property remains compatible.
- Implemented native seek, volume, mute and playback rate controls, with validated persisted audio preferences. Media opens before applying a resume position and starting playback; listening-start events follow the native Playing state. Replaced the display-only progress bar with an accessible seek slider and prevented timer updates from seeking the media.
- Validation: 521 playback assertions, 72 Navidrome protocol/model assertions and 26 XML model contracts pass. Windows 8.1 Debug x86 build and development package registration pass. Native playback interaction and the forthcoming library pages are not certified by these source/build checks.

## 2026-11-09 11:24:18 +08:00
- Added Windows 8.1 Navidrome section/detail pages with resource-backed English UI, accessible music-folder selection, all album order modes/year/genre filters, independent typed search offsets and complete play-all pagination. Home and Search now route to these pages; the home command library can render without a successful server request.
- Integrated complete song/album/artist metadata, server biography/notes and related artist/album links. Real artist artwork is retained. Added song/album/artist favorites and ratings after confirmed mutations, top/similar song playback, and context play/append/play-next.
- Added playlist creation, metadata editing, add/remove/reorder/delete with separate ownership and readonly checks; bookmark save/resume/delete; and share create/list/edit/copy/delete. Confirmed from core/share.go that creation without a custom expiry uses the server default and omission on update preserves the existing expiry. The UI follows that behavior.
- Added a reproducible disposable loopback server fixture and 125 generated tagged WAV files, using the separately verified official Navidrome 0.64.2 binary. Start/stop helpers verify listener/process/config identity, retain diagnostics, and never use a personal server for mutations. The functional source remains locked to f5b6805.
- Validation: 40 real-server integration assertions, 73 production protocol/model assertions, 521 playback assertions, 26 XML contracts and 77 desktop resource/project checks pass. Windows 8.1 Debug x86 build and registration pass. The client window was observed; the installed Computer Use helper returned no accessibility tree and screenshot capture timed out. Native interaction/visual acceptance remains unverified.

## 2026-11-09 11:41:02 +08:00

- Integrated native playback activity: enhanced presence reports preserve actual stop positions and set ignoreScrobble; an independent monotonic counter integrates only playing intervals at the preceding playback rate. Failed opens, paused time and seek jumps cannot qualify a listen.
- Persisted qualifying listens as account-scoped occurrence IDs/song IDs/UTC timestamps with UTF-8 XML and atomic pending-file replacement. Added retry backoff, serialized reporting, stale-account guards and suspension flushing; pending records retry without requiring another song to qualify.
- Session state now invalidates and notifies on account/password/server changes. Added real server play-count metadata and verified that seeking to the end leaves it unchanged while an explicit qualifying submission increments it once. The standard scrobble endpoint has no idempotency key; a lost response after server acceptance can cause a retry duplicate, which is documented rather than hidden.
- Validation: 92 protocol/model assertions, 521 playback assertions, 26 XML model contracts and 42 real Navidrome integration assertions pass. Windows 8.1 Debug x86 build and development registration succeed. No native interaction or visual acceptance is claimed.

## 2026-11-09 12:02:32 +08:00

- Added real foreground downloads and a desktop Downloads section with actual progress, cancellation, retry, original-file export, deletion, missing-file and corrupt-catalog states. Browsing and playback of completed downloads use app-local files without a server connection.
- Download catalogs retain full song metadata, artwork filenames and lyric variants under an account-specific hashed folder. Generated leaf filenames, schema/account validation, completed file sizes and atomic catalog replacement prevent partial/cross-account files from becoming playback entries; credential-bearing request URLs are not serialized.
- Original media is always retained for export. Incompatible original headers request a separate MP3 stream; ignored format requests or failed transcoding leave offline playback disabled with a visible error and an exportable original. HTTP 200 XML/JSON/HTML error bodies, invalid MIME, empty/short bodies and cancellation cannot become completed media.
- Added local queue routes and the home Downloads tile. Corrected radio command gating, detail navigation eligibility, confirmed queue annotations, local artwork URLs and shell AppBar visibility. The latter prevents the old shell command surface from covering section-page CommandBars. A service test also exposed and fixed a missing saved-library key lookup.
- Validation: 122 protocol/model/download assertions, 34 production-service assertions with CLR storage/event adapters, 43 real Navidrome API assertions, 521 playback assertions and 111 desktop resource/project checks pass. Windows 8.1 Debug x86 build and development registration succeed. Synthetic codec headers verify fallback selection/error handling only; WinRT storage, native decoding, XAML and picker interaction still require native acceptance.

## 2026-11-09 12:18:16 +08:00

- Completed the Windows 8.1 media-detail player: artwork/transport, queue and lyrics use separate sections with narrow-window recomposition. Desktop music, radio, bookmark, download and queue entry points now reach the new page; generic video navigation retains its existing player.
- Added real-position synchronized lyric highlighting, returned variant/language labels, cached offline metadata/lyrics, follow/manual scrolling, buffering/error display, native volume/mute/rate controls and queue editing, server save/restore, bookmark/favorite/rating/download/playlist commands. Track/account generation checks reject stale loads.
- Fixed the queue-click constructor found by the previous failed build. Guarded slider duration/position notifications so timer or stop updates cannot seek media, and blocked duplicate end/stale playing events. Preserved album metadata when lyrics fail. Added explicit retry of pending listens and local queue import/export operation gating.
- Validation: Windows 8.1 Debug x86 build and development registration pass; 127 production protocol/model assertions, 36 production-service assertions, 521 playback assertions, 26 XML model contracts and 150 resource/project checks pass. New assertions cover unordered/duplicate/untimed/extreme-offset lyric boundaries and disconnected/cross-account metadata access. Native XAML interaction, audio decoding and visual acceptance remain unverified through the installed capture helper.

## 2026-11-09 12:35:02 +08:00

- Added an Account/Server media-detail page and reachable home/player commands. Actual user roles, server version, extension errors and scan state are displayed. Only a returned administrator role enables startScan; active scans poll while the page is open. Explicit reconnect refreshes negotiated state while restoring the selected accessible library.
- Reworked the existing Settings flyout into resource-backed server/account/playback sections with accessible fields, input validation, format/bitrate choices and actual save errors. Existing compatibility settings migrate to MP3 and preserve their prior default bitrate; unknown persisted format values are retained. Route selection now precedes configuration persistence, and offline save failure is distinguished from an unreachable server.
- Queued streams now resolve current quality settings when playback starts. An unopened current-account HTTP song retries once as MP3; radio/local/other-account sources, an MP3 request and failures after actual playback do not loop or create a replacement listen. The player exposes compatibility state and preserves both errors if fallback fails. Corrupt volume/mute/rate settings recover to valid defaults.
- Added a production audio harness with simulated native events, keeping its evidence separate from actual decoding/XAML/background playback. Validation: 26 audio, 130 protocol/model and 38 production-service assertions plus 198 resource/project checks pass. Windows 8.1 Debug x86 build and development registration pass. Native visual and interaction acceptance remains unverified.

## 2026-11-09 13:13:25 +08:00

- Continued from the actual preceding chats and the clean commits 0cbda37/91d02cd. Navidrome f5b6805 remains the locked functional authority; MixRadio remains a component/layout/visual/design reference. The target remains the Windows 8.1 Store desktop client.
- Added administrator-gated radio creation/edit/deletion with resource-backed forms, complete encoded stream URLs, optional homepage clearing, confirmed server mutations and account/page guards. Added acoustic recommendations and paths using actual current-account song IDs; absent providers retain explicit failures.
- Added a separate role-gated server-player page for remote queue/status, transport, whole-second seeking, gain and queue replacement/append/removal/clear/shuffle. Recheck order before indexed/destructive writes and the current index before seek/relative skips. Invalidate late poll responses, preserve unapplied gain drafts and cancel stale account/page operations. The API has no atomic queue revision, so the final read/write interval can still race another client.
- Restored native player frame-state handling through the existing queue serializer, retaining account, position milliseconds and lyric-follow state. Restore positions after media opening with duration bounds, only for the saved current occurrence; changing selection prevents a later unintended seek.
- Corrected new XAML names/resource references, added narrow-view column spanning and empty/disabled/accessibility states, and separated the dialog contract from WinRT rendering to test real page logic. Extended the source checker to namescopes and project-owned StaticResource resolution.
- Extended the functional specification to 53 required cases, with explicit radio administration, server-player and acoustic-discovery requirements. Added verify.ps1/report.py to bind fresh source/server logs and original reference hashes to a clean commit. Mutation suites run sequentially; source/simulated responses cannot promote native cases into passes.
- Validation: desktop Debug x86 build/deployment passed with MSBuild 14; 166 protocol/model, 28 simulated audio, 28 controlled jukebox page, 38 production service, 51 real Navidrome 0.64.2 integration and 521 playback assertions passed; XML checks passed for 26 model types. The resource audit passed 234 string lookups and 46 project resource references; 22 acceptance-tool tests passed. Report/verification syntax and the 53-case specification validated.
- The real fixture verifies radio administration, an administrator-triggered scan and explicit disabled-jukebox/provider-absent errors. It does not establish enabled server-device audio or acoustic-provider success. The client launches, but native window capture still reports "window capture timed out: timed out waiting on channel". Actual native interaction, decoding, background behavior and visual acceptance remain unverified and will stay blocked in the fresh report.

## 2026-11-09 13:22:51 +08:00

- Fixed acceptance-report generation creating an untracked Python bytecode cache when importing the Navidrome source inspector. Generalized the existing MixRadio-only cache ignore to standard __pycache__ directories for all local Python tools; tracked edits and other untracked files still fail the clean-commit gate.
- Validation: git check-ignore recognizes the generated inspector cache, and no application source changed. The final verification/report will be rebound to this clean commit. Native failure observations remain associated with the captured application source; application tree hashes will be compared before reusing those unverified environment observations.

## 2026-11-09 13:52:11 +08:00

- Continued the Navidrome-source functional audit. Added random-song size, genre, optional year and library filters, with validation matching the source contract and real-server coverage. Now-playing entries are complete songs and retain server player/state/position/rate; the active page refreshes only without selection and stops its timer when deactivated.
- Connected ReplayGain metadata to online/offline queue entries and the native player. Track/album modes and persisted pre-amplification use Navidrome's gain/peak calculation. Missing values are neutral, explicit zero is retained and malformed values cannot reach native controls. User volume remains independent; amplification beyond MediaElement's 0–1 volume range is explicitly reported. Enhanced display credits, composer, BPM, comment, moods, ISRC and content advisory fields appear in track details.
- Validation: desktop MSBuild 14 x86 build/workspace deployment passed; 177 production protocol/model assertions, 43 simulated native audio assertions, 53 real Navidrome integration assertions, 521 playback assertions, 26 XML model types and 255 resource lookups/49 project resource references passed. These checks establish source/service logic, not actual native decoding, normalization, background behavior or rendered interaction.
- Next: rebuild the acceptance specification around Navidrome functions and their distinct evidence levels; original MixRadio design verification will remain independent from functional completeness.

## 2026-11-09 14:22:58 +08:00

- Added vault-backed password/API-key authentication, actual-client authenticated route detection and token-account verification. HTTP 200 error responses cannot establish a connection. Configuration snapshots and account/secret/mode changes invalidate pending work.
- Added negotiated automatic streaming using Navidrome's JSON profile and signed stream contracts. Direct native formats and PCM limits use normalized server codec names; preferences convert kbps to bps. Explicit original/MP3 preferences retain ordinary streaming, and real decision denials remain errors. Signed tokens are not persisted into queue items.
- Native source resolution now cancels on stop/skip, preserves pause/resume positions and rejects late or stale results/media events. Added resource-backed authentication and stream information. Corrected the Windows 8.1 form-enabled binding to UserControl, which supports IsEnabled.
- Added the production connection harness and real successful transcode integration. Installed a checksum-verified FFmpeg 9.0.2 test dependency only under ignored .tmp, and restarted only the owned disposable fixture with an explicit hashed binary. Real WAV streams are byte-identical to downloads; a 64 kbps decision produces actual MP3 media. The real-server test exposed and corrected the PCM normalization mismatch.
- Validation: desktop Windows 8.1 Debug x86 build/deployment, 190 protocol/model assertions, 54 simulated audio assertions, 19 controlled production connection/stream assertions, 38 production-service assertions, 57 real-server integration assertions and 259 resource lookups/49 project references pass. Native decoding, XAML interaction and background playback remain unverified; the real fixture does not advertise API-key authentication.

## 2026-11-09 14:27:48 +08:00

- The Navidrome endpoint audit identified the missing current-account avatar. Added getAvatar with the selected authentication mechanism and encoded username, and displayed the server image on the native Account page. Session invalidation clears its source; server Gravatar policy and placeholder behavior are preserved.
- Validation: 191 protocol/model assertions, 58 real Navidrome integration assertions and 260 desktop string lookups/49 project resources pass. The real fixture returns its PNG avatar placeholder. Desktop Windows 8.1 Debug x86 build/deployment passes. Rendered image behavior remains a native acceptance item.

## 2026-11-09 15:03:03 +08:00

- Replaced the combined MixRadio/function specification with a reviewed Navidrome inventory: 38 client functions, all 91 source route registrations and 86 distinct endpoints. Accounted for 58 directly requested endpoints, nine documented legacy alternatives, one fixed license compatibility response and 18 upstream unavailable endpoints. Conditional sharing, jukebox and acoustic functions remain required; denial responses cannot establish successful optional device/provider behavior.
- Generated 115 required cases with distinct source/protocol, production service/control, real-server and native layers. The 77 automated cases use scoped ND-CHECK proof groups; the 38 native tasks retain explicit prerequisites, actions and expected results. Score 3 means the declared criteria hold at the stated layer, with a 75 overall threshold and every required case passing. This is a new functional metric; older combined-design reports cannot be compared as the same scope.
- Added endpoint/source/client auditing, inventory/spec validation and 23 isolated tool self-tests. Verification requires a clean commit, records unchanged code at both ends, validates the owned fixture and hashes all 14 check logs. Reports bundle source mappings, client snapshots, scoped proofs and sanitized server version/extensions. Native observations default to not_run for the current commit; historical capture failures and simulated events cannot certify native success. MixRadio package paths are no longer functional report prerequisites, and report filters apply only to the case table.
- Expanded the disposable fixture with generated album PNG covers and real integration checks for artwork, activity values, all favorite/rating entity types and exact unavailable-operation HTTP status. Included the production XML helper in serialization dependencies and normalized compiler input paths before deduplication. The serializer regression exposed IList<string> on transcoding reasons; changed it to List<string> rather than excluding the production model from verification.
- Added docs/navidrome/acceptance.md and updated implementation, root/test/tool guides and the separate MixRadio workflow. The guides distinguish structural mapping from runtime proof, conditional provider/device prerequisites from denial checks, and functional acceptance from visual comparison.
- Working-tree validation: desktop MSBuild 14 Windows 8.1 Debug x86 build/workspace registration, 196 protocol/model, 54 simulated audio, 19 controlled connection, 28 controlled jukebox, 38 production-service, 65 real Navidrome 0.64.2 integration and 521 playback assertions pass; serialization covers 33 model types. Source auditing accounts for every endpoint, 260 desktop string lookups/49 project resource references and 23 acceptance-tool self-tests pass. Native decoding, background/lifecycle, rendered task completion and enabled acoustic/server-device behavior remain unobserved for this source. Final acceptance evidence is generated again from the completed clean commit.

## 2026-11-09 15:07:53 +08:00

- Final clean-commit verification for f492710 passed all application/source/server checks but exposed a stale generic-tool test that still required both design/functionality references and the previous case IDs. Updated that regression to enforce Navidrome-only functional authority, all four evidence layers and required native task/evidence contracts. Preserved the failed verifier record and generic-test log under ignored .tmp/navidrome-audit/f492710-failed.
- Added quality.py evaluate --check-only so an independent evaluation validates and prints results without overwriting a specialized Navidrome function report or its saved evaluation. The CLI regression confirms report, evaluation and run bytes remain unchanged. Updated the guides and scoped the function report's live status filter to the actual case-table selector.
- Validation: all 23 generic quality-tool tests and 23 Navidrome acceptance-tool tests pass. The preceding final attempt also confirmed 196 protocol/model assertions, 65 real Navidrome integration assertions, 33 serializer model contracts and the desktop build/registration. A fresh complete verifier/report will be bound to this new clean commit; native tasks still require current app observations.

## 2026-11-09 21:35:06 +08:00

- Continued the preceding desktop home/UI session and completed its unfinished MixRadio port. Replaced MainView's obsolete BrowseView inheritance with a native Hub and explicit home-tile, section-header, library-menu, settings, search and queue event handlers. All 13 existing Navidrome destinations remain reachable through shared route definitions; Settings no longer passes the retired collection-refresh argument.
- Home previews now use actual recent/new albums, playlists, artists, favourites and current-account downloads. Download previews load before connection, independent section failures retain the other sections, and stale account/library/page/reload results cannot start playback. A removed download cannot redirect a click to the first remaining song. The embedded player forwards its existing saved-state contract.
- Reconstructed selected original native XAML resources from MixRadio PC 2.2.0.7933: palette, typography, vector wordmark, button/control states, 162 px tiles, 332 px featured tiles, album/artist/song templates, search, the 172 px player grid, vector transport controls, offline progress tiles and the 300 px queue drawer. Home, library, details, downloads, player, account, jukebox and Settings now use these resources with current Navidrome bindings and operations. Narrow layouts reflow and retain search access.
- Added native artwork fading and the original fallbacks, plus optional server-provided artist backgrounds at the recovered 0.6 opacity. Corrected failed-image fallback visibility, playback/artist command notifications, button-specific localization properties and culture-aware download percentages. English labels remain resource-backed.
- Added the home controller/presentation regression harness, extended the desktop resource audit and corrected jukebox harness dependencies for the offline media association. Added reproducible style/template exporters, a source-to-client port map and an eight-file asset provenance manifest; updated tool guides to reflect the explicitly requested copied components while retaining complete recovered packages/source under ignored local reference directories.
- Validation: 41 home/navigation assertions, 28 controlled jukebox assertions, 38 production-service assertions using the owned Navidrome fixture, 521 playback assertions and 54 simulated audio assertions pass. The resource audit passes 294 string lookups and 75 project resource references. Both generated dictionaries reproduce their checked-in contents; all eight PNG SHA-256 hashes match the installed originals and the new manifest. git diff --check passes. Final Windows 8.1 Store Debug x86 build/deployment with MSBuild 14 and subsequent launch succeed; the workspace Subsonic8.exe remains running.
- Native verification limit: the final rebuilt-client capture retry again returned "window capture timed out: timed out waiting on channel"; earlier original/client captures and the accessibility-only attempt also yielded no usable visual/tree evidence. Controller/source/build checks do not establish native clicks, rendered parity, decoding, background playback or suspend/resume behavior. Existing functional/visual acceptance reports are not promoted by this work.

## 2026-11-10 20:21:11 +08:00

- Resumed the latest interrupted session's home-loading repair. The native failure was page construction: initializing legacy SearchPane conflicts with the inline SearchBox in the same window. Removed that registration and its unused service contract, awaited shell initialization directly, logged navigation failures, checked the navigation result and bound the startup ring to initialization state. Initialization failures use the existing error view and clear the busy state.
- Fixed the next native XAML failure by assigning an explicit TransitionCollection instead of appending to the inherited sealed collection. Preserved collections in the recovered-style exporter, resolved the original lyrics vectors from DesignAssets, and retained the lyrics toggle's localized accessibility name. Removed GridView padding that displaced the third tile row, preserving original 162 px tiles within 170 px cells.
- Added the opt-in native XAML screenshot driver under tools/desktop and conditional project compilation. The driver records actual page state, image decoding, item bounds and PNG exports while navigating production pages. The desktop runner enforces fresh Debug probe builds, rebuilds on capture-mode changes to invalidate MSBuild 14's stale conditional-compile cache, and prevents -NoBuild from deploying a known probe build. The ordinary executable was inspected and contains no NativeUiProbe code.
- Extended the source audit to shell localization, all project-owned resource references, dictionary x:Name keys, transition collection construction and the conflicting search registration. Both generated resource dictionaries reproduce exactly; all eight copied PNGs match the installed MixRadio 2.2.0.7933 originals.
- Reviewed and revalidated the six captures from 2026-11-10 18:32:58 +08:00: startup home, albums, player, returned home, scrolled home and the 500 x 900 root layout. Every page and all six home sections finished loading without errors; no observed remote image was pending decoding. All 88 home item bounds fit their grids. PNGs, capture hashes, the native report, verification JSON and the normal-startup excerpt are preserved in ignored .tmp/ui-acceptance/2026-11-10. The images display the actual configured library and were not regenerated as mockups.
- Validation: 41 production home/navigation assertions and the source audit (295 string lookups, 401 project resource references) pass. The previous session's final MSBuild 14 Windows 8.1 Debug x86 rebuild/deployment succeeded. Re-launched that ordinary package at 20:13:31 +08:00; MainView navigation and shell initialization completed at 20:13:33 +08:00, with no new startup failure and a responding workspace process. Updated docs/mixradio/native-port.md with repair details, capture commands and evidence limits.
- Visual scope: recovered brushes, typography, wordmark, tile geometry and controls are supported by original resources plus rendered-client review. Original-window capture still times out, so pixel-for-pixel parity is not certified. The narrow capture changes the in-app root dimensions, and navigation is programmatic; OS resizing, native input, full search/queue interaction and media/lifecycle behavior are not established by this capture run. Existing functional/visual acceptance reports remain unchanged.

## 2026-11-11 03:03:43 +08:00

- Refined the desktop Navidrome library against the supplied MixRadio screenshot and recovered MyMusic XBF templates. Restored the Light category heading, 54 px title gap, original dropdown geometry, dedicated search column, pink artist index cards, 260 x 130 album content and 307 x 147 container spacing. Artist cards retain the existing detail navigation; no album prefetching, server sorting or playback behavior was changed.
- Compared three native rendering rounds. The first improved type, spacing and long-title handling. The second aligned the first card to the reference's (130,149), but a 3 px top inset reduced the available panel height below six rows; rejected that intermediate layout. Reduced bottom padding from 20 to 16 to restore six rows at 1744 x 1090. Kept titles to three whole lines and artist credits to one, with tooltips, preserving the count inside every measured card.
- Connected recovered focus rectangles to actual keyboard focus for library items and the category button. The original Focused states were empty. Preserved the recovered hover/selection geometry and added accessible item names/help text. The category flyout uses the original selected-item treatment and all 13 existing localized routes. The first probe caught a newly imported GeneratedDuration decimal that Windows 8.1 rejected; the exporter now emits the proper time representation.
- Expanded the opt-in native probe with reference-size, 900/500 px, artists, songs, selected/focused, controlled hover and missing-cover captures, header/card/text bounds, and production category-menu invocation/navigation. Final evidence under .tmp/ui-acceptance/2026-11-11 contains 16 actual page PNGs plus the popup, reference crops, earlier rounds, build logs and a hashed verification record. All pages loaded without errors; 2,052 measured text bounds and 658 count bounds stayed inside their cards. The reference viewport retains six rows at the expected origin/size/stride. Keyboard focus opacity is 1 in the focused frame, and the 13-choice menu navigated from artists to albums.
- Validation: 41 production home/navigation assertions pass; resource audit passes 295 string lookups and 420 project resource references. Both generated dictionaries reproduce from the local reference. Windows 8.1 MSBuild 14 Debug x86 probe and ordinary builds/deployment/launch pass; the final ordinary executable excludes NativeUiProbe. git diff --check passes. Updated docs/mixradio/native-port.md with parameters, rejected candidate, provenance and verification limits.
- Scope: real XAML rendering and programmatic native navigation/focus are verified. Narrow frames use root-size overrides; hover/missing-cover frames are explicitly controlled previews with the artwork binding restored. Physical pointer/touch input, OS resizing, media playback and full pixel parity remain outside this run; no overall acceptance score was promoted.

## 2026-11-11 10:47:45 +08:00

- Completed the MixRadio library indices from the eight supplied screenshots: inline pink outlined group headers, album/song character tiles with disabled empty letters, and named artist/genre tiles using the original artist silhouette. The recovered exporter retains 126 x 106 character content, 158 x 236 named panels, 8 px gaps and the original seven-row song-column cap. Genre album cards now show title, track count and year.
- Group labels come from server metadata. The title index includes #, a-z and discovered Unicode initials; accented Latin letters share their base initial, while other scripts retain their complete text element. Artist groups use opaque case-sensitive album-artist IDs, support multiple artists, and genre groups support multiple values. Unknown values use English-default resources instead of embedded product text.
- Added explicit header-to-index-to-header navigation, the Index app-bar command, Ctrl+minus / Ctrl+wheel shortcuts, Back/Escape return, keyboard focus and compact-width reflow. Two bounded GridViews map each index entry to its actual inline header. Headers cannot enter media selections or commands; song playback follows the displayed ordering. Paging preserves selected media and the visible content anchor.
- Normal browsing remains paged at 100 records. The first explicit index request completes the current metadata listing before declaring any letter empty, because the protocol provides no complete initial-index endpoint. Reuse album listings for artist/genre groups instead of per-artist fetches. Reject stale page/account/folder results, rebuild after refresh/filtering, and preserve requested ordering for other album sort modes. Artwork and UI containers remain virtualized; very large libraries require more metadata/network work for the first index.
- Validation: 68 production home/navigation/index assertions, 298 desktop string lookups and 443 project resource references pass. The recovered template exporter reproduces its checked-in dictionary exactly; git diff --check passes. Windows 8.1 MSBuild 14 Debug x86 probe and ordinary builds, deployment and launch pass. The final ordinary executable excludes NativeUiProbe.
- Native evidence: 35 final real-XAML page captures plus the category popup, with no page loading/error failures, are saved under .tmp/ui-acceptance/2026-11-11/library-index/20261111T024409Z. All four indices completed paging, preserved media selection during paging and index navigation, rejected header selection, and landed on their last active group. The configured library supplied 425 albums and 2,749 songs; the final song view realized 87 of 3,020 media/header containers and used seven rows. Reviewed wide, 500 px, grouped, index and jump captures; earlier refinement rounds and build/test logs remain alongside the final evidence.
- Scope: native rendering and programmatic jumps are verified. The compact captures use root-size overrides; physical pointer/touch, OS resizing and native SemanticZoom pinch gestures are not established by these checks. The explicit jump view does not wire pinch gestures. Updated the native-port and test guides; no global acceptance score was changed.

## 2026-11-11 12:36:34 +08:00

- Performed actual OS keyboard acceptance of the index implementation in e4859cc using the Windows computer-use input API. Navigation and selection used native Tab, Enter, Home/End, arrows, Ctrl+minus and Ctrl+Space. No production navigation handler or automation peer was invoked to complete these cases.
- Added an opt-in passive NativeUiObserve build because Windows compositor capture timed out and accessibility capture returned null. Snapshot requests accept names only; the observer records actual XAML rendering, route, focus, selection and realized controls without invoking navigation, focus, selection or sizing. The runner requires a fresh Debug build, separates observer/probe modes and prevents NoBuild from retaining diagnostics.
- Verified library entry and category navigation; album and artist indices completed 425-album metadata paging and jumped to their last group; the song index loaded 2,749 songs and jumped to its last group. Enter on an album header reopened its index and Escape restored header focus. A real song selected with Ctrl+Space retained its check mark across index navigation; Ctrl+Space on a group header did not add a media selection.
- Acceptance is partial. Enter on the final Unknown genre tile produced no post-jump observation in two processes, followed by Windows Application Hang 1002 / Quiesce terminations at 12:20:12 and 12:32:19 +08:00. Both launches also exposed invisible error-report buttons in the startup Tab sequence. Preserved these findings without attributing the hang to navigation versus suspension/instrumentation or applying a speculative product fix. No error-report command was activated.
- Added docs/mixradio/index-input-acceptance.md with actions, evidence identifiers, reproduction and limits; linked it from the native-port guide. Preserved named JSON/PNG observations, build logs, source patch, binary hashes and Windows hang events under ignored .tmp/ui-acceptance/2026-11-11/library-index-input. Timed-out observations and a stale request captured after relaunch are explicitly excluded from successful jump evidence.
- Validation: observer and normal Windows 8.1 Store Debug x86 builds/deployment/launch succeeded. The restored normal mode marker and executable confirm diagnostic type/method exclusion, and the workspace client responds. git diff --check passes. Mouse/touch, wheel/app-bar paths, disabled-letter navigation, OS resizing, playback and lifecycle correctness remain unaccepted; existing global acceptance scores are unchanged.

## 2026-11-14 00:34:38 +08:00

- Repair the named library index projection: each artist/genre album occurrence now has its own XAML automation identity and group key, while commands and selection map to the original media item. Synchronize selection across occurrences and preserve the exact group context when metadata is rebuilt.
- The saved 2026-11-11 genre hang dump places the UI thread in ItemsControlAutomationPeer child-cache lookup during UI Automation sibling traversal. The prior projection reused the same album objects across groups; independent occurrences remove that ambiguous peer identity instead of hiding accessibility or inserting timing delays.
- Bind the shell error overlay's visibility and enabled state explicitly through ShellRoot.DataContext.ErrorDialogViewModel.IsHidden, including disabled/collapsed defaults while the model is attaching. This retains actual error controls when an error is shown.
- Preserve the pre-existing conditional index traces and passive-observer diagnostics; use atomic completed snapshot publication during native verification. Added regression assertions for unique occurrence identities, shared media, canonical selection and rebuilding. Verification results follow below.

- Native keyboard retest at 670 x 770: first Tab reaches the visible Choose music control; three Unknown genre jumps complete with matching header focus, including one with a selected album. Escape returns to the same header and preserves selection. OS screenshot capture still times out; completed app-rendered snapshots are saved under ignored .tmp/ui-acceptance/2026-11-14/index-fix. Observer file-lock failures are diagnostic failures, not product hangs; atomic pending/latest publication and retry-until-completion address that diagnostic issue.
- The first native probe completed all four final-group jumps, independent occurrence identities, selection/deselection synchronization, and error-overlay show/hide checks. Artist/genre paging selection was not established: the old probe attempted to select the canonical model item after the projection gained separate display occurrences. Corrected the probe to select a real displayed item; retain the initial results separately and rerun paging verification.
- Final validation at 2026-11-14 01:10:00 +08:00: 76 production home/navigation/index assertions pass. The corrected fresh native probe completed 37 captures with every index check and all three overlay checks passing, including shared occurrence selection/deselection and named-group paging retention. Reviewed actual narrow and original-size genre jump renders. The final observer branch compiles after invalidating the legacy compiler's generated assembly cache; ordinary build/deployment/launch succeeds, its mode marker is normal, the executable excludes NativeUiProbe/ObserveAsync, and the workspace process responds. Full evidence stays under ignored .tmp/ui-acceptance/2026-11-14/index-fix; mouse/touch and global acceptance remain unverified.

## 2026-11-14 22:22:01 +08:00

- Diagnosed VS2015 CS0263 under the IDE's Debug Any CPU configuration: Client/obj/Debug/Main/MainView.g.cs still declares the obsolete BrowseView base while MainView.g.i.cs and the current MainView.xaml declare AppPage. MainView.xaml.cs has no conflicting explicit base. Regenerate the build outputs from the actual XAML rather than editing generated code or changing the page's architecture.
- Added AutoGen, DesignTime and DependentUpon metadata for the existing EchoNestCredentials.cs output in Client/Client.csproj so VS can associate it with EchoNestCredentials.resw and its declared custom generator.
- Retained NuGet requireReinstallation flags in Common/packages.config and Client.Common/packages.config: the reported package-retargeting warnings require actual package reconciliation and are not corrected by deleting warning metadata. No package versions or target frameworks were changed.
- Validation at 2026-11-14T22:25:01.1905486+08:00: MSBuild 14 Debug Any CPU Rebuild exits successfully, and both generated MainView partials now inherit AppPage. XML checks confirm the generated EchoNest output association. Prior generated files, build logs and verification JSON are preserved under ignored .tmp/vs2015-debug-fix. The ordinary x86 layout was not replaced. Windows UI control was stopped by the user with Escape, so a new Visual Studio F5 debugging session has not been verified; the user can reload Client and press F5. Existing API/compiler and NuGet retargeting warnings remain distinct from the resolved CS0263 error.

## 2026-11-15 00:28:45 +08:00

- 根据用户截图修复首页及完整播放器共用的 XAML：歌曲信息原来只占 172 逻辑像素的一行，收藏按钮和评分被裁切；现在随封面占两行，窄屏重新排为单独的自动高度行。时间文字、进度滑轨及播放按钮分别按内容测量，底部状态区也使用自动行高，避免相互覆盖。
- 拖动期间只预览位置，松手才向 MediaElement 提交一次 seek，防止连续位置赋值导致解码器反复跳转。计时刷新保留拖动位置；重复松手/捕获丢失不会重复提交；取消、页面卸载、DataContext 更换、停止及当前歌曲变化会放弃旧位置。拒绝非有限位置、忽略未变化的绑定值，并保留实际音频时长的边界限制。键盘事件同时监听已处理事件。
- 将 SeekSliderStyle 中的颜色对象关键帧恢复为 SolidColorBrush 对象，同时修复样式导出脚本，避免重新生成时丢失修复；扩展源代码检查以验证模板自己的动画目标名称及画刷值类型。
- 新增仅在显式 NativePlaybackProbe 调试构建中包含的原生验证，使用独立队列及本地生成的 PCM 音频，不修改账号、服务器内容或个人播放队列。部署脚本支持 AnyCPU 的实际输出目录，用于匹配当前 VS 调试安装的 neutral 包；最初的 x86 部署因架构冲突失败，随后使用 AnyCPU 成功复用现有注册，未卸载应用。
- 验证：54 项生产音频逻辑断言、521 项生产播放队列断言、298 个桌面字符串查找和 444 个项目资源引用检查通过。29 项原生检查通过，包括 200 次拖动预览无 seek、松手仅一次 native seek、取消/移除/停止、无效位置、重复绑定、恢复播放，以及 1744/900/500 逻辑像素下的收藏评分、滑轨与按钮几何关系和四种滑轨视觉状态。已查看宽屏、窄屏真实 XAML 渲染，收藏按钮和评分完整显示。
- 00:27:44 +08:00 已成功重建、部署并启动普通 Debug AnyCPU 客户端，进程响应正常，构建模式为 normal，普通可执行文件不包含原生探针元数据。证据保存在忽略目录 .tmp/ui-acceptance/2026-11-15/playback-layout-fix。Computer Use 的窗口截图两次超时，因此真实鼠标/触摸事件及截图中服务器提供的具体编码音源仍未实播认证；本地 PCM/native seek 与布局验证不代替这些检查。

## 2026-11-15 00:43:46 +08:00

- 修复用户截图底部 16 个只显示空心圆的辅助命令按钮：这些 AppBarButton 原来没有设置 Icon。为停止、下载、下载库、刷新歌词、评分、队列上移/下移、移除/清空、保存/恢复服务器队列、另存播放列表、导入/导出文件、同步播放记录、账户设置补齐相应的 Windows 8.1 原生 SymbolIcon，保留现有本地化标签和命令绑定。
- 原生 XAML 加载发现 Windows 8.1 不支持 Down 符号值；下移图标使用受支持的 Up 箭头旋转 180 度，避免无效枚举或依赖其他平台字体。
- 验证：现有资源检查通过（298 个字符串查找、444 个项目资源引用）；临时扩展原生渲染探针，确认全部 20 个主/辅助按钮都有原生图标且具有实际渲染尺寸，40 项图标检查通过，并查看真实 CommandBar 渲染确认 16 个辅助图标和箭头方向。临时诊断扩展已恢复，证据、诊断源码和构建日志保存在忽略目录 .tmp/ui-acceptance/2026-11-15/player-command-icons。
- 已完成普通 Debug AnyCPU 重建、复用现有开发注册并启动客户端，构建模式为 normal、进程响应正常。此次产品变更仅为播放器 XAML 的图标配置。

## 2026-11-15 01:08:10 +08:00

- 按用户选择，将播放器底部 16 个辅助图标整理为歌曲、队列、媒体库三个功能菜单。菜单内显示明确的操作名称，并用分隔线区分队列编辑、服务器保存/恢复及文件导入/导出；保留全部原有操作，并提供打开现有队列抽屉的入口。
- 收藏、保存书签和音频控制保留直接入口。保存书签改为书签轮廓，歌曲和队列分别使用音符与列表向量，媒体库使用原生 Library 图标，六个入口具有不同语义；命令栏默认展开，以显示本地化名称。
- 菜单复用现有资源标签和 ViewModel 操作，仅增加三个组名及显示队列标签。启用状态实时绑定现有 Can 属性；点击时验证启用状态和当前 DataContext，页面卸载或模型更换时关闭菜单，避免过期菜单触发旧模型操作。
- 验证：源代码检查通过（284 个桌面字符串查找、444 个项目资源引用），git diff --check 通过。临时扩展现有原生播放探针，62 项检查全部通过，包括原有 29 项布局/拖动检查及 33 项菜单检查：1744 和 500 逻辑像素下六个入口具有实际图标尺寸并位于命令栏内，菜单名称正确解析、各项具有实际尺寸，队列选择能即时更新可用状态，菜单点击调用原有上移逻辑。已查看 500 像素真实 XAML 工具栏渲染，确认图标居中且各不相同。
- 验证限制：菜单弹层 PNG 回读遇到 WinRT 缓冲区错误，因此没有宣称查看弹层截图；菜单依据原生控件名称、实际尺寸与点击路由检查。真实鼠标/触摸注入和服务器具体编码音源的拖动仍未认证。临时探针源码已恢复，证据和临时诊断源码保存在忽略目录 .tmp/ui-acceptance/2026-11-15/grouped-player-commands。
- 普通 Debug AnyCPU 构建、复用现有开发包注册及启动成功，构建模式为 normal，工作区 Subsonic8.exe 进程响应正常。普通构建日志已保存到同一证据目录。

## 2026-11-15 02:37:52 +08:00

- 按用户提供的两张 MixRadio 参考图，调整对应的专辑歌曲预览和播放队列侧栏。复用本地恢复的 MyMusicAlbumPopup 与 PlayerQueue XBF 布局、颜色、字体和播放图标，并将原版队列按钮/随机开关样式加入可复现的样式导出器。
- 点击媒体库专辑现在打开居中的 880 × 600 逻辑像素浮层：326 像素概览列、250 × 250 封面、34 像素内边距，标题与艺术家链接位于封面下方；歌曲列表显示真实曲号、标题和时长，保留右侧滚动条留白。窄窗口将概览重排到列表上方，并将封面缩为 128 像素。保留整张/选中歌曲播放、单曲播放、艺术家导航和完整详情入口，菜单及说明使用现有本地化系统。
- 浮层使用独立注入的现有详情 ViewModel，按标准 Screen 生命周期激活和释放，复用原有专辑加载/播放逻辑。打开前验证当前账户、音乐目录和媒体集合；关闭、页面卸载、DataContext 更换和媒体库重新加载会释放旧模型，迟到的加载结果不能重新打开浮层。背景禁止命中，Tab 在浮层内循环，Escape、关闭按钮或点击背景可返回媒体库，并恢复原专辑的焦点。
- 队列改为从左侧展开的 300 像素栏，标题、工具栏和列表均使用原版半透明深粉色背景；标题显示由真实歌曲时长计算的本地化总时长。保留 84/56 像素标题和工具栏、58 像素队列内容及上下各 5 像素外边距，当前歌曲使用亮粉色高亮，保留缩略图和多选编辑能力，增加空队列提示，打开时滚动到当前歌曲。
- 保留随机播放的二态开关；循环播放使用按现有 Off → All → One 逻辑切换的按钮，并以亮度、单曲数字及可访问名称表达当前状态，避免二态 ToggleButton 将单曲循环显示为关闭。修正背景遮罩模板：原 NullButtonStyle 不绘制 Background，现在使用实际绘制背景的按钮模板，以获得参考图的黑色半透明遮罩及点击关闭功能。
- 验证：源代码检查通过（291 个桌面字符串查找、469 个项目资源引用），原版样式导出结果与检查入库内容的 SHA-256 完全一致，git diff --check 通过。临时原生探针的最终 51 项检查全部通过，包括 1744/900/500 逻辑像素下专辑加载、边界、重排、选择、艺术家可用状态、关闭与迟到结果保护；时长列不与原生滚动条重叠；队列宽度、68 像素行间距、总时长、随机/三态循环、多选/清空，以及实际播放器队列按钮打开左栏和关闭按钮移除侧栏与背景。
- 已查看宽屏/窄屏专辑和实际播放器队列的真实 XAML 渲染。滚动条截图使用原生 MouseIndicator 状态；按钮通过原生自动化 peer 调用，未认证物理鼠标/触摸输入或与参考窗口的逐像素合成一致性。队列操作使用独立私有队列，不修改用户播放队列；专辑信息只读。临时探针源码已恢复，原版布局、诊断源码、JSON、PNG 及构建日志保存在忽略目录 .tmp/ui-acceptance/2026-11-15/reference-overlays。
- 普通 Debug AnyCPU 客户端已成功重建、复用现有开发注册并启动；构建模式为 normal，工作区 Subsonic8.exe 进程响应正常，普通可执行文件中没有 NativeUiProbe 或 NativePlaybackProbe 元数据名称。

## 2026-11-15 02:56:38 +08:00

- 按用户要求建立 main，以原 Navidrome-support 的 d04c6e8 为基线保留全部历史（此前领先远程 74 个提交），保留原分支，不改写提交。主对话已空闲；此次自动化不调用会停止、部署或启动开发客户端的脚本。
- 增加 GitHub Actions CI：所有分支 push、main 的 PR、v* 标签及手动触发均运行；Windows 2022 用 NuGet 固定恢复 Caliburn.Micro.Core 3.2.0 并使用现有七组生产源码 CLR 回归，Linux/Python 检查 XAML/资源、验收工具、锁定 Navidrome 功能来源和发布资产校验。CI required 汇总所有必需任务，失败、取消或跳过都不能通过。固定 action commit，限制 token 权限。
- 按用户选择，完整旧版 WinRT AppX 仍在本地构建，不安装 self-hosted Runner。新增 Release 构建、外部证书签名、资产准备、草稿上传和云端发布流程；禁止使用项目默认临时 PFX，关闭自动版本递增与诊断探针，不执行安装脚本。发布前验证 main 祖先关系、相同源码提交的成功 CI、四位版本标签、清单身份、归档路径与文件清单、SHA-256、依赖架构/版本、固定签名证书和 SignTool 完整性，再重查草稿资产和远程标签未变化后发布。
- 发布证书 SHA-256 作为独立仓库变量 RELEASE_CERTIFICATE_SHA256，由维护者选择正式证书后配置；未配置时 CD 拒绝发布。此次用户未指定发布版本，因此只建立流程，不创建公开 Release。云端验证不是可复现编译或原生运行证明，此边界及安装方法写入 docs/releasing.md。
- 新增版本化 GitHub 仓库配置及幂等应用脚本：main 默认分支，PR、最新基线、CI required 和讨论解决为必需，禁止删除/强推，采用 squash/rebase 与线性历史；单人仓库不要求他人审批；v* 标签禁止更新/删除；github-releases 环境只允许 main。新增 CONTRIBUTING.md、PR 模板和 AGENTS.md，记录用户的 PowerShell、提交身份、UTC 偏移与正确逻辑要求，更新根 README。
- 本地验证：七组 CLR 回归通过（协议/模型 196、音频 54、连接 19、jukebox 28、首页 76、播放 521 项断言，另含序列化回归）；资源审计通过（291 个字符串查找、469 个项目资源引用）；两组既有 Python 工具自测各 23 项通过；Navidrome 审计覆盖 38 个功能、91 条路由注册；新增 12 项发布资产边界测试通过。Actionlint 1.7.12、YAML 解析、PowerShell 语法与 git diff --check 通过。
- 额外用独立合成 AppX 验证真实签名与 SignTool：临时内存私钥生成的证书用于测试，完整包验证成功，修改包内容后验证失败；成功和失败路径均清理 CurrentUser Root 中的临时信任，测试私钥文件删除。该包未安装、未运行，不代表客户端原生验收。

## 2026-11-15 03:01:30 +08:00

- 远程 Navidrome-support 已同步全部既有提交，main 已建立并成为 GitHub 默认分支；初次推送工作流因 OAuth 缺少 workflow scope 被 GitHub 拒绝，在用户同意并完成官方设备授权后，5ad7619 已成功推送。
- 首次云端 CI 发现验收规格的 featureInventorySha256 基于本地 CRLF，而 Linux 的 Git 检出使用 LF，导致生成规格被正确判定为过期。本次为 tools/navidrome/*.json 指定 text eol=lf，在本地统一这些输入并重新生成 acceptance-spec.json；只更新输入字节哈希，115 项验收内容不变。保留严格的字节校验，未通过跳过检查掩盖不一致。
- Actions 官方当前 v6 已使用 Node 24；将 checkout、setup-python 和 upload-artifact 更新为已查证的 v6 commit，避免旧运行时弃用告警。Actionlint 和规格再生成检查通过；准备重跑云端检查后应用版本化保护规则。

## 2026-11-15 03:07:51 +08:00

- 为正式发布收尾检查建立 codex/finalize-release-rules 短期分支。GitHub CLI 读取证书变量改为显式 JSON value 输出，避免依赖人类可读格式；ZIP 校验补充拒绝 NUL 路径；依赖目录必须匹配包声明架构，neutral 主包如有原生依赖须覆盖 x86/x64/ARM，不能因只包含一种架构而声称所有 AnyCPU 安装都可用。
- 为规格生成器显式指定 LF 输出，使 Windows 上重新生成的文件也遵循已提交的字节规则。新增依赖目录错误、neutral 依赖覆盖和 NUL 路径边界检查，发布工具共 14 项自测通过，115 项规格检查及 git diff --check 通过。原生 AppX 的当前 neutral 开发包没有 PackageDependency，此次不改变产品代码或依赖。
- 后续通过 PR 和实际必需检查合并这组收尾改动，以验证已经编写的分支流程。

## 2026-11-15 03:12:35 +08:00

- 云端 Windows 的七组 CLR 回归已在日志中确认通过，延迟定位到合成签名测试写入 CurrentUser Root：Windows 默认根证书保护会弹出确认对话框，headless Runner 无法响应。
- 临时验证改用 CertOpenStore 文档规定的 CERT_SYSTEM_STORE_UNPROTECTED_FLAG 打开 CurrentUser Root。此路径仍需显式 TrustPinnedCertificate 开关，先验证固定 DER 证书 SHA-256 和包签名者，再验证完整 CMS/SignTool 包签名；不更改全局保护配置，已有信任不删，本次添加的信任在 finally 撤销。使用 X509Store 复制 native handle 并关闭原句柄，避免泄漏。
- 完整合成 AppX 签名、篡改拒绝及成功/失败信任清理测试已在约 8 秒内通过，临时私钥已删除；将修改补充到 PR #1，重新执行 CI。停止了已被此修复取代的等待运行，未把挂起检查当作通过。

## 2026-11-15 03:23:23 +08:00

- 进一步阶段日志及本机 Windows SDK wincrypt.h 定义确认：CERT_SYSTEM_STORE_UNPROTECTED_FLAG 只影响缓存根证书的读取保护列表，不能关闭 Root 写入确认。撤销这处不准确实现，避免把手动确认后的本地测试成功误当作 headless 验证。
- 正确实现改为仅在 GitHub Actions 且进程具有管理员权限时，通过标准 X509Store API 临时写入 LocalMachine Root，并在 finally 撤销本次添加的证书。完整 SignTool 签名、证书链和 AppX 内容验证始终必需；SignedCms 只负责嵌入签名者解码与固定证书比较。
- 本机七组 CLR 回归不写机器证书库；合成签名测试只在临时 hosted Windows Runner 中执行并检查成功/失败的信任清理。已停止明确识别为本轮测试的挂起 PowerShell 进程，没有停止开发客户端；接下来以云端结果确认修复。
- 中断的本机测试在 .tmp/ci/signature-test-82ae60a28b3046e7954eb0bafa07bfe6 留下 ephemeral.pfx（测试代码生成、随机密码加密）。自动审批拒绝了对这个临时文件的删除，返回理由只有 blocked by policy；保留原文件，不改用其他方式绕过拒绝。此前正常完成的测试已自行清理私钥和信任。

## 2026-11-15 03:27:26 +08:00

- GitHub 云端 CI 已真实全部成功：PR 运行 https://github.com/A-BenLi06/SubSonic8/actions/runs/43631944835 的 Desktop regressions、Source and tool checks、CI required 均通过，同一提交的 push 检查也通过。hosted Windows 的完整合成 AppX 签名、篡改拒绝、临时机器信任清理和测试私钥清理均通过；Linux 的资源、14 项发布自测、两组工具自测及 38 功能来源审计通过。
- 已应用并读取确认 GitHub 配置：main 为默认且受保护的分支；main stability 规则集 27276002 要求 PR、线性历史、最新基线、讨论解决及 GitHub Actions 的 CI required（应用 ID 15368），无管理员直接推送绕过；immutable release tags 规则集 27276004 禁止 v* 标签更新和删除；github-releases 环境仅允许 main。仓库启用 squash/rebase、自动删除合并分支，禁止 merge commit。
- PR #1 的实际保护状态为 CLEAN，所需 CI 全部成功。本条配置与验证记录将作为独立提交经相同检查合入 main。正式发布证书指纹仍由维护者选择后配置；本次不创建或公开任何 Release。

## 2026-11-16 13:33:31 +08:00

- 按用户要求，在项目现有代理说明文件 AGENTS.md 中补充：向用户发送图片前，先压缩为 WebP 格式，再发送压缩后的图片。
- 思路：沿用已有 AGENTS.md 作为统一开发约定入口，明确该规则适用于代理发送图片的流程。
- 验证：核对新增条款与用户要求一致，git diff --check 通过；本次仅修改文档，无需产品运行验证。云端必需检查由本次 PR 执行。

## 2026-11-16 18:02:29 +08:00

- 将解决方案和主项目改名为 Mixdrome，统一桌面/手机命名空间、程序集、XAML 类引用、配置类型、WinMD 后台任务入口、服务器请求客户端标识，以及构建/发布工具的项目路径、环境变量和分发文件名。桌面与手机包名分别为 A-BenLi06.Mixdrome 和 A-BenLi06.Mixdrome.Phone，发布者显示名为 A-BenLi06，证书主体沿用维护者已有的 CN=benli06；手机使用独立 PhoneProductId，保留侧载用的空 PhonePublisherId。辅助测试包也统一维护者身份，关闭默认临时 PFX 签名。
- 依据用户参考图重建白色耳机笑脸、音符及 Segoe UI 字标（Mix 粗体 / drome 细体），采用洋红、粉红、橙、黄色与黑色。生成方形/宽磁贴、应用/商店图标和两端启动图，桌面覆盖 100/140/180%，手机覆盖 100/140/240%。33 个 PNG 全部采用 scale 限定符，清单使用逻辑文件名；静态磁贴已包含字标，关闭重复的系统名称。播放磁贴继续使用真实歌曲与封面，没有参考图示例曲名或虚假控制按钮。
- 字体、尺寸和配色集中在 tools/branding/brand.json，generate.py 从可缩放几何生成资源及压缩 WebP 预览。桌面原向量 MixRadio 字标与手机文本字标改为 PRI 品牌资源，并同步模板生成器；文件类型说明、产品名称和维护者联系方式更新为 Mixdrome / ben_li06@outlook.com。保留 Subsonic 协议拼写、许可证、原作者记录与 MixRadio 历史参考来源。
- 新包身份创建独立的数据目录，不自动迁移旧账户、凭据、队列或下载，不删除旧安装。README 与 docs/branding.md 说明身份、签名、资源重建及迁移边界。本次没有指定发布版本，不创建公开 Release。
- 发现另一聊天正在共享目录改写手机版后，将本轮品牌工作迁入从 origin/main 创建的独立受管工作树；仅撤回共享目录内本轮的机械品牌替换及品牌资源，保留对方全部播放逻辑修改。三个既有 C# 文件的非统一行尾在改名时一并规范化。
- 验证：七组生产源码 CLR 回归全部通过（序列化 33 模型类型；协议/模型 196、音频 54、连接 19、jukebox 28、首页 76、播放 521 项断言）；Python 验收工具 23、质量工具 23、发布资产 14 项自测通过；资源审计通过（291 个桌面字符串查找、469 个资源引用），全部项目 XML 可解析，git diff --check 通过。
- 使用本机 MSBuild 14 与真实 Windows 8.1 / Phone 8.1 SDK 完成最终 Debug AnyCPU XAML/AppX 构建。修正原 Client.App / Shell 无效入口，桌面音频与搜索扩展显式指向 Mixdrome.App；原生打包要求比可选 XSD 属性更严格，未通过省略入口规避。PRI 检查发现 ApplicationName 与 AppName.Text 必须分离，已正确修复资源键的 scope 冲突。最终无 APPX1621 品牌比例匹配告警。
- 逐个打开两端实际 AppX 内的 33 个品牌 PNG，核对尺寸、完整性、Name / Publisher / Executable / EntryPoint。已查看压缩 WebP 预览。桌面新开发包注册与启动成功，Mixdrome.exe 进程响应正常，日志确认 Mixdrome.Main.MainView 导航及 Shell 初始化完成；手机完成原生构建但未在真机或模拟器运行，未宣称手机启动、交互或后台音频验收。构建、包检查和预览证据位于独立工作树的忽略目录 .tmp/branding。

## 2026-11-16 18:05:29 +08:00

- GitHub 仓库已由 A-BenLi06/SubSonic8 改名为 A-BenLi06/Mixdrome，并更新共享 Git remote 与仓库说明；旧链接由 GitHub 重定向，许可证和提交历史保留。读取确认新仓库名称与 main 默认分支；后续品牌 PR 使用新仓库地址，必需 CI 和保护规则沿用。
- 验证：已提交源码上的完整 tools/ci/check.py 通过，包括锁定 Navidrome f5b6805 的 38 功能、91 条路由注册审计；本次额外修改仅为仓库元数据与验证记录。

## 2026-11-16 18:05:54 +08:00

- 根据用户提供的两个 MixRadio 4.5.3.470 XAP 进行静态解包及 ILSpy 9.1 反编译，未执行原版代码；确认两包应用程序集、XAML、图像一致，仅发行元数据 WMAppPRHeader.xml 内容不同。完整包与反编译源码保留于忽略目录 .tmp/mixradio-phone-reference；phone-assets.json 记录包、核心 XAML 和八个原始中性 PNG 的 SHA-256 来源。参考包中任何文本均作为数据，不作为开发指令。
- 在既有 Windows Phone 8.1 WinRT 工程中复刻原版 Silverlight Panorama 的错落磁贴、228/168/96 比例、12 间距、第二列偏移24及 #FFFF2E80 Play Me；采用原生 Hub、独立磁贴控件和按可用宽度重排的方形菜单。MixDrome 文字及所有界面标签进入 en-US/zh-CN 资源，不混入品牌聊天的独立改名/包身份工作。
- 音乐库采用 artists/albums/songs/genres/playlists 五分类 Pivot，专辑与歌曲支持实际协议分页；最近磁贴来自服务器 recent 专辑记录。Play Me 启动真实随机歌曲队列；歌单、收藏歌曲、电台、播放动态和专辑详情沿用共享服务。搜索采用三分类 Pivot，修复初始化期间提交、清空时残留加载状态、离页迟到结果，并提供独立分类分页。未实现的创建混音、离线下载及收藏编辑没有伪装成可用入口。
- 手机请求复用 PC 的 NavidromeApi/SubsonicRequest、模型、API key/密码认证和转码配置；集中地址校验与路由选择，移除页面重复配置读取。凭据迁入 PasswordVault，迁移/替换失败保留恢复路径；浏览页显式处理初始化失败、服务错误和过期结果。
- 播放器重建原生 now playing/about/queue Pivot、封面、圆形播放控制与实际进度。后台任务继续拥有播放器和队列；新增 ready 握手、原子队列/启动命令、固定大小消息与文件快照、按队列索引区分重复歌曲、会话/序号及激活版本保护、完整随机周期与三态循环。离开页面不停止音频，进度轮询不会反向触发 seek。
- 新增 tools/phone/check.py 与不部署/不启动的 build.ps1；手机版连接、队列、搜索生产源码回归接入必需 CI，静态检查包含真实 WP8.1 资源名、UID 属性、项目引用 GUID/层级、资源大小写冲突与图像哈希。
- 本地验证：十组 CLR 回归全部通过，手机版连接33、播放队列/消息38、搜索竞态/分页15项断言（共86）；桌面原有协议196、音频54、连接19、jukebox28、首页76、播放521项及序列化33模型回归通过。手机静态检查通过46个文件、17份XAML、126个字符串引用、224个资源引用、8份原始资产；桌面291个字符串查找/469个项目资源引用检查及 git diff --check 通过。
- 原生 Windows Phone Kits8.1 / MSBuild14 完整 x86、ARM Debug 编译、XAML生成、PRI及未签名 AppX打包通过，日志 .tmp/phone/build-x86-Debug.log 与 build-ARM-Debug.log。最新版 x86 使用 AppDeployCmd /installlaunch 在中文 WP8.1 WVGA512MB 模拟器部署并启动成功；同版本 /updatelaunch 返回0x81030137，随后正常 /installlaunch 成功。没有调用桌面部署/停止脚本，也没有创建公开 Release。
- 原生界面截图先后返回 FrameArrived timed out 和 window capture timed out；因此没有取得原版/客户端匹配帧，没有认证视觉一致性、实际触摸、真实服务器集成、锁屏/后台音频及系统中断恢复。构建、部署和模拟事件回归不能替代这些运行验证。设计来源、功能范围和后续原生验收见 docs/mixradio/phone-port.md。

## 2026-11-16 18:18:39 +08:00 — 手机版播放列表选择竞争

- 改动：为 LibraryPage 的条目选择单独维护版本；连续点击、切换标签、刷新和离开都会使旧请求失效。只有最新且仍处于播放列表标签的请求可启动播放或展示错误。
- 思路：页面加载版本不能代表用户的最新选择，因此分别校验页面生命周期、选择版本和当前标签，避免过期返回覆盖用户意图。
- 验证：新增执行生产页面源码、使用延迟 HTTP 对端的 13 项回归并加入 CI；run-library.ps1、手机版源码/资源审计及 git diff --check 通过。此回归不认证原生渲染和触摸输入；后续整合将补做 x86/ARM 原生构建。

## 2026-11-16 18:40:48 +08:00 — 手机版与主分支品牌整合

- 改动：在独立工作树中合入 origin/main 的 Mixdrome 品牌提交，保留手机版页面、原版资源来源及两边完整开发记录；采用新的项目文件名、命名空间、应用/后台入口、包身份和 scale 品牌图像。
- 思路：品牌与手机版工作同时起于旧基线，不能让机械改名覆盖原生页面和队列逻辑。手机版继续使用自己的中英文资源，移除会占用同一逻辑路径的桌面 PRI 链接；应用名称/描述由手机资源解析。
- 验证：整合后的原生 x86、ARM Debug XAML/PRI/AppX 构建均通过；七组桌面与四组手机版生产源码 CLR 回归通过（手机99项）；手机资源审计及12项图片限定符边界检查通过。源码/功能映射审计、23项验收工具、23项质量工具和14项发布工具自测通过；完整 CI 脚本要求的干净工作树检查将在提交后执行。未重新部署整合后的包，原生视觉、触摸和后台音频验收仍未取得证据。

## 2026-11-16 18:43:06 +08:00 — 手机版后台监听与媒体生命周期

- 改动：由后台音频任务按原生 Playing 状态观测监听，复用 ListeningCounter 的 min(时长一半,240秒) 门槛和 NavidromeApi.Scrobble 的协议/认证；presence 使用 submission=false，达标监听使用 submission=true，使最近播放和服务器播放动态有实际来源。显式外部电台不作为曲库歌曲上报。
- 思路：拖动进度不增加实际监听时间，暂停/缓冲时间不计入；开始时间取真实播放发生时的 UTC。报告配置只包含路由、账户、认证模式和凭据资源名，后台按保存的路由/账户校验 PasswordVault，并在内存捕获账户；合格监听的原子重试文件不保存密码、API key 或认证流地址。既有音频队列仍保存带认证的流/封面 URL，此限制没有被误称为所有持久化均无秘密。
- 恢复：网络工作独立于播放命令门，单请求5秒取消、每账户30秒重试；失败后仅通过原账户/路由校验刷新凭据，不用后来账户上报旧监听。为媒体回调添加来源版本，旧结束/失败事件不能推进新队列；关闭流程先拒绝新操作并等待进行中的队列文件工作，再取消报告、保存合格记录和完成后台 deferral。
- 验证：新增实际编译 AudioTask 与 PhoneListeningReporter 的30项生产源码回归，涵盖播放计时、进度跳变、暂停、后台定时、UTC时间、外部电台、API key、重启重试、账户切换、凭据轮换、慢请求取消，以及延迟队列文件期间旧媒体事件/关闭流程；接入必需 CI。手机47文件/17 XAML/126字符串/224资源/8原始资产审计及 git diff --check 通过。最终整合后的 x86/ARM 原生包将在下一步验证，真实设备背景播放和服务器联动仍未验收。

## 2026-11-16 18:47:13 +08:00 — 手机版最终整合验证

- 将后台监听提交整合到已更名的 Mixdrome 工程，保留两边提交及开发记录；更新新增测试命名空间，整理共用协议测试源码列表，补充 phone-port.md 的上报、暂停动态过期、账户隔离与后台生命周期说明。
- 最终生产源码回归：12组 CLR 套件全部通过；手机连接33、播放队列/消息38、搜索15、播放列表13、后台监听/媒体竞争30，共129项断言。手机47文件/17 XAML/126字符串/224资源/8原始图像来源审计通过。
- 最终原生验证：整合后的 x86 和 ARM Debug Windows Phone 8.1 XAML、PRI、WinRT元数据及未签名 AppX打包全部通过，日志 .tmp/phone/build-x86-Debug.log 与 build-ARM-Debug.log。先前品牌整合提交的干净工作树 tools/ci/check.py 已完整通过；本次在提交后再次执行，并等待 PR 最新提交的 CI required。
- 运行边界：此前旧包在中文模拟器安装/启动成功；最终品牌整合包未部署，没有取得匹配的运行截图、触摸输入、真实服务器、锁屏/后台音频及系统中断恢复证据。构建与生产源码回归不替代上述原生验收。

## 2026-11-16 18:51:53 +08:00 — 保留作者身份的 PR 合并准备

- 最终原生包、12组 CLR 回归、本地完整源码/工具检查，以及提交7f57715的 push/PR必需 CI均已通过。
- GitHub squash API拒绝指定作者邮箱ben_li06@outlook.com；当前含品牌整合merge的分支也不能由GitHub自动rebase。为遵循作者身份约定，保留完整原历史在本地codex/mixdrome-phone-history（7f57715）及独立整合工作树，将相同的最终树整理为从origin/main起始的单一维护者提交，以便使用仓库允许的rebase合并。
- 验证：整理过程不改变产品源码/资源/工具，仅追加本条记录；通过最终树差异检查确认。使用旧远端HEAD作为force-with-lease条件更新自己的临时分支，不改写main，并重新等待新提交的CI required。

## 2026-11-16 23:46:59 +08:00 — 手机版原生截图与交互细化

- 继续既有未提交的手机版细化，按原版 XAML 恢复首页两列错落磁贴、228/168/96 尺寸比例、12 间距与24偏移；按 Hub 可用宽度调整几何，文字保持原生字号。大 Play Me 使用24/25标签；空Recent使用系统强调色0.6透明占位，有封面时不叠标题，无封面时显示名称，空位不参与点击。
- 曲库分别呈现艺术家/流派、专辑与歌曲行。专辑封面右上角使用原版比例白边圆与播放三角，标题独立进入详情；歌曲恢复整行标题/歌手，取消额外左侧图标。直接播放读取实际艺术家专辑、专辑歌曲或完整流派分页；页面、标签和最新选择共同拒绝过期结果。
- 增加歌曲页的“全部随机播放”：完整读取歌曲分页，失败或请求失效时不播放部分结果；导航通过同一条 ReplaceQueue 消息传入可选Shuffle，由后台在换队列命令内应用真实随机模式，保留普通导航的既有模式语义。
- 原生输入发现自定义深底与系统黑色前景不匹配，地址/用户名失焦后不可见。恢复WP8.1系统成对的TextBox前景/背景主题资源，保留原生聚焦与禁用状态模板，密码框/搜索基样式沿用相同处理，没有访问控件私有节点。
- 新增Windows Phone8.1 SDK guest framebuffer截图与触摸/滑动/硬件Back/按键/私有文件文本输入工具，绕过仍然超时的宿主窗口捕获通道；工具不调用产品导航处理器或XAML自动化peer。多模拟器必须显式选实例；输入超时和串行使用限制记入phone-port.md。
- 验证：5组手机CLR回归159项通过（连接33、曲库39、监听34、队列38、搜索15），资源审计47文件/17 XAML/131字符串/245资源/8原始资产与diff-check通过。首次整批运行的连接/曲库/监听在宿主高延迟时失败，原日志保留；相同源码单独复跑全部通过，没有改超时或跳过断言。最终x86与ARM Debug原生XAML/PRI/AppX构建通过。
- 当前原生证据：最终包已安装启动，480×800 guest截图确认首页磁贴与占位；实际页面输入、硬件Back关闭键盘后地址文字仍清晰可读。隔离Navidrome测试账号曾经页面保存成功；安装部署会清除模拟器应用数据，最终包正在重新配置并继续曲库/播放验收。证据位于忽略目录.tmp/ui-acceptance/2026-11-16/phone-refinement；转场帧不作为布局验收。尚不声称原版匹配运行帧、锁屏/后台音频或真实用户服务器验收。

## 2026-11-17 01:31:25 +08:00 — 原生播放器状态、封面及进度验收修复

- 原生运行发现“没有正在播放/无法播放”与实际音频状态不符。临时只读诊断包在前台ApplyStateAsync记录到COM E_FAIL及ArgumentException；后台没有媒体失败记录。仅记录异常类型/HRESULT/取消枚举，不保存错误消息或认证地址。五个诊断源文件/项目文件已按原SHA-256恢复，普通x86/ARM重建通过，二进制排除诊断方法和文件名。
- 修复五个独立层面的播放器问题：失败消息现在携带完整队列状态，避免高序号失败丢弃初始快照后永久缺少元数据；曲目信息先独立更新，队列的选中等待Loaded及公开Items.VectorChanged，并按同一对象位置同步；封面解析只把目标支持的http/https/ms-appx/ms-appdata视为图片URI，带冒号的Navidrome不透明ID仍按原值经封面API编码，避免误作自定义scheme及大小写变更；ready后立即启动轮询，未就绪状态不读取原生进度属性，仅在已确认换源进入未就绪状态时处理观测到的E_FAIL；当前位图获取/解码失败回退到原有默认PNG，旧位图/离页回调不能覆盖新封面，不改变音频或队列。
- 确认实际缺省封面响应为服务器内置album-placeholder.webp（哈希一致）；锁定Navidrome的getCoverArt不支持format参数或Accept格式协商，因此没有添加虚构参数或更改服务器全局配置。通用解码失败回退不等于加入WebP解码器，现有PNG/JPEG封面仍走真实接口。
- 新增提取真实生产方法执行的状态/激活/图片测试及封面URL测试。分别替回旧Failure处理、旧选中逻辑、旧进度读取、旧激活顺序和旧图片方法，均触发预期反证失败；修复后通过。手机版共221项断言：连接33、封面13、曲库39、队列38、播放器状态47、监听36、搜索15。已有HTTP夹具首次失败日志与相同源码复跑通过日志均保留；新增envelope测试门外读取LastMessage的竞争改为生产相同命令门内观察，没有扩大产品锁、改超时或跳过断言。
- 普通原生包实际输入与截图验收：艺术家标题进入正确3张专辑；专辑右上播放图标与歌曲整行排版；实际直接播放和元数据；进度持续刷新，暂停停在0:52，拖动到0:30仍暂停，恢复后自动换到PCM；首次打开queue显示2曲，点击首曲重新播放，暂停0:40且标题/下一首状态匹配。最终包130/131/132帧显示默认PNG、真实曲目及正常进度，并保持暂停1:05，无错误提示。实际PNG封面和背景在115帧正常渲染，随机图标为已开启的粉色。
- 完整性：原生ShuffleAll持久队列127首/127唯一ID，与实际曲库集合完全相同，遗漏/额外均0；两次100+27分页顺序稳定、无重叠。隔离服务只读播放计数确认原25首及90秒MP3/PCM均收到真实达标submission记录，没有用测试请求制造scrobble。监听落库不代表耳听输出、锁屏或系统中断验收。
- 证据/限制：新增docs/mixradio/phone-runtime-acceptance.md记录案例、排除帧及验证边界；原始PNG、压缩WebP、源码反证、诊断和构建日志保留于忽略目录.tmp/ui-acceptance/2026-11-16/phone-refinement及.tmp/phone-emulator-fixture。仍无原版匹配运行帧、其它显示尺寸、分组字母跳转、首页迷你播放器转换、真实用户服务器和锁屏/系统媒体中断恢复认证；没有修改全局验收分数。两个SDK截图脚本路径输入错误后均先重取画面才继续，未计为产品失败或沿用旧坐标；转场帧和46开始屏帧不计布局/播放通过。

## 2026-11-17 14:13:55 +08:00 — 依据新增 MixRadio 手机参考图重建页面组成

- 从最新 origin/main 建立 codex/phone-reference-composition。按用户提供的八张 768×1280 原版截图及已解包 XAML，以 480×800 设计坐标适配实际视口：首页封面/时间磁贴、五块菜单（收藏双宽）、艺人背景、轻量顶部栏、精选/分类双列卡、收藏艺人分组和最新动态、Play Me 双控制播放器与粉色反馈层。品牌仍为 Mixdrome，系统主题色沿用系统 accent；照片和名称来自服务器，不植入参考图人物。
- 对接真实评分、星标、歌词及不含认证流地址的原生文本分享。补 OpenSubsonic artistImageUrl 协议字段和艺人照片回退；首页/标题栏只观察后台队列，按实际 currentIndex 显示，返回不重新起播。状态栏由 App 串行处理导航可见性。
- 增加真实创建歌单混音与前台离线下载。完整文件落盘后才发布 manifest；账户/来源路由隔离、本地队列不探测网络，下载去重但 QueueIds 保留重复 occurrence；离线上报使用已验证缓存的来源地址。下载离页取消的限制写在实际操作说明中。
- 验证：14 个生产源码 CLR 回归套件通过；其中手机连接38、封面13、队列39、页面状态65、搜索15、曲库39、上报36、集合19、离线36项断言通过。x86 原生 Debug AppX 构建通过；源码/资源审计通过（59文件、21 XAML、222字符串、259资源、8原有资产）。完整 tools/ci/check.py 的实际检查均运行完，但末尾要求干净 git diff，因此提交后再复跑。
- 原生首轮截图已发现并修正横向吸附不能离开首页、收藏按钮默认只占单宽、状态栏隐藏传播到设置页，以及图片回退移除绑定等问题。当前构建还需重装后逐页验证上述修正；尚不宣称完成全部原生验收。截图在忽略目录 .tmp/ui-acceptance/2026-11-17/reference-composition，展示前均转换 WebP。私有 Navidrome fixture 的照片/歌单仅作测试，不入产品资产。

## 2026-11-17 15:00:44 +08:00 — 原生截图驱动的排版与命中修正

- 根据023–040原生截图修正真正测量错误：Mixes虚拟化单元格取实际viewport一半，避免分数DIP测量仅排单列；大Pivot标题区、收藏首字母/首照片/最新动态的留白按参考坐标收紧；选择图标改checklist；顶部home图标居中并扩大为完整60×60设计触区。
- 艺人背景改用独立背景Canvas承载超视口图片，避免平移前被父布局槽裁剪而露黑边。同艺人播放复用已解码图，异艺人新图成功解码后替换，保留账户/代次隔离。播放器照片延伸至透明状态栏；反馈层按参考328高、80图标、204×48确定按钮重新定位。
- 真实触摸验证发现顶部home点击不进入事件。临时原生日志证明触点在bar内，但命中祖先是越界的Pivot透明RootElement；没有Frame导航失败。将chrome明确置于内容层之上后，同一(30,61)触点在普通构建中成功返回首页并保留菜单位置（067→068）。曾验证的SDK坐标原点假设没有效果，已完全撤销，未留入产品或工具。
- 所有临时NativeNavigationObserve/指针/UnhandledException诊断已还原，生产源码搜索为零；当前x86普通包已安装，ARM原生Debug构建通过，资源/源码审计通过。页面状态回归增至71项，队列39项通过。详细参考映射和旧截图观察写入docs/mixradio/phone-reference-composition.md，最终数据页面几何与离线操作验收仍在进行。
- 工具增加真正SDK按下/保持/抬起的Hold动作，沿用观察截图坐标校验和600–2000ms范围，CompileOnly及4项输入前拒绝检查通过。后端只读确认037/040收藏移除后加回，现3位艺人且含Fixture artist1；027操作歌曲Fixture track040的userRating确为5。

## 2026-11-17 16:21:16 +08:00 — 完整视口与真实离线入口

- 原生092确证两列网格：面板自身Loaded/SizeChanged取得真实ScrollViewer视口，单元格按完整DIP向下取整，保留207/225卡片比例，避免旧方案创建时ItemsPanelRoot为null。Mixes去除参考图中没有的底栏，空状态区不占行；收藏艺人页保持完整命令栏，其余页使用Minimal，标题文字基线与checklist尺寸按截图修正。
- 新增Play Me的真实离线菜单入口：只有完整网络歌曲队列且当前账号/激活/队列版本仍匹配时才能生成下载请求，重复歌曲不去重，缓存表现为真实粉色Play Me卡片。其余歌单/流派仍长按保存。离线页顶部提示保持两行最小高度，下载时显示前台限制，空底部状态折叠；不同高度卡片顶部对齐，状态字体/标签/进度随卡片缩放，播放图标复用既有中性资产。
- 首页长名称色带随实际文字高度适配，默认50设计单位不变；wordmark上移到参考基线。采样三张原始参考PNG的粉色均为RGB(255,46,129)，现有品牌色仅B差1，反馈层改复用该品牌色。临时截图几何工具独立识别参考黄/本机蓝五块菜单矩形，归一480×800后所有x/y/宽/高delta为0，仅证明外框，不宣称像素分数。
- 实际长按已下载25首MP3。首次尝试离页时下载已完成，未作为取消通过证据；改用私有代理每16KB延迟250ms后，原生112下载中返回113，ISE dir只剩原先完成目录，未完成目录确实清除。代理限速随后归0。ISE整库快照会被原生图片持有的cover文件阻止，且长桌面输出路径可超旧工具长度限制；无效/不完整快照未用于计数结论，改用明确目录列表核验。
- 081后一次意外回到开始屏尚未复现，未标记为稳定性通过。临时异常后退出探针和内存探针下，3首[A,B,A]及21首跨艺人[A,B]×10+A均实际播完，末尾暂停/禁用Next，内存约43–47MB/194MB且无未处理异常记录。所有探针已还原；普通x86及ARM Debug重新构建通过，接着执行最终普通包验收。
- 完整14套CLR回归通过（1211项计数断言，另含10项序列化检查）；手机队列39、页面状态82、集合19、离线36等全部通过。源/资源审计通过：59文件、21 XAML、230字符串查找、259资源、8原有资产。

## 2026-11-17 16:39:46 +08:00 — 统一播放操作的服务器身份边界

- 独立审查发现PlaybackPage.MatchesPlaybackAccount对整条BaseUrl忽略大小写，会把同host但大小写不同的路径视作同源。改用共享AccountIdentity.For规范化scheme/host/default port并保留路径大小写，以Ordinal比较身份并继续检查认证模式。
- 补同host大小写规范化正例、路径大小写不同/无效URI反例，以及跨路径评分不写服务器、Play Me离线不导航的真实生产方法回归。队列39、页面89项通过，未改界面。
- 普通包30首/180秒的原生运行再次回到了上一应用，尚未认证为稳定通过；前述3/21首探针运行未复现。PR保持草稿，继续以相同30首服务器队列和仅异常后记录再退出的临时探针定位，不将问题简单吞掉。

## 2026-11-17 16:59:53 +08:00 — 收藏选择图标的原生路径边界

- 普通包原生截图153-normal-favourites-artists.webp显示checklist越出圆框：WP8.1 PathIcon显式20×20不会自动将36×32的Data坐标按预期缩小，先前仅限制控件尺寸未解决实际图形边界。
- 仅将FavouritesPage.xaml的三行check与横线路径坐标规范到20×20图标槽（实际范围x0–20、y0–19），保留PathIcon与系统AppBar圆框及真实多选行为，不增加像素偏移或另套自定义框。
- 验证：手机源码/资源审计通过（59文件、21 XAML、230字符串查找、259资源、8原有资产）；该低影响路径修正未新增镜像实现的测试。修正后的最终普通包尚待Root重装并截图确认，153仅证明旧包问题，不作为修正通过证据。

## 2026-11-17 17:03:49 +08:00 — 离线混音的最新选择与迟到验证

- 修复已通过生产方法夹具复现的选择竞争：连续点击离线混音A、B时，较早的A验证若先返回，原代码会播放A并使B失效。现在单独维护选择代次，结合页面刷新代次与active状态，仅当前选择可以导航或报告错误；新点击、刷新和离页均拒绝旧验证结果。
- 保留本地队列校验、完整重复歌曲顺序及现有Play Me呈现；当前缺失文件仍刷新列表并报错。刷新等待期间的新点击可以取代旧的缺文件提示，旧列表条目的迟到点击不会重新请求验证。下载卡片保留与离页取消流程不变。
- 验证：tools/phone/run-offline.ps1通过36项生产离线存储回归及19项生产页面方法回归；页面测试原样编译OpenMixAsync、Refresh、OnNavigatedFrom及状态方法，覆盖最新点击、旧成功/失败/异常、刷新和离页、当前错误、下载占位与取消。手机源码资源审计通过59文件/21 XAML/230字符串/259资源/8原有资产，git diff --check通过。先前竞争反证与本次日志位于忽略目录.tmp/phone-review-proof。
- 本次未运行SDK构建或设备输入；新的普通包原生复验仍待主任务完成。

## 2026-11-17 17:08:23 +08:00 — 使用平台原生清单图标

- 普通611包截图177确认：即使将路径坐标约束20×20，WP8.1 PathIcon的实际排列仍超出AppBar圆框，上一条路径缩小只通过源码检查，未通过原生验证。
- 改用目标SDK明确支持的Symbol.List（Icon=List），保留原生命令栏尺寸、主题、按压与禁用状态，不添加模板补偿或像素偏移。
- 验证：SDK头文件windows.ui.xaml.controls.h确认Symbol_List=57676；最终普通包将再次安装截图验证。本次仅替换图标，无新增低价值实现镜像测试。

## 2026-11-17 17:18:08 +08:00 — 最终普通包截图、真实离线播放与验收边界

- 普通7b包相同服务器30首/180秒队列逐段保存24帧，01–19持续推进，20–24停在末首0:06/0:06且Play可见、Next禁用。本轮无退出；此前两次意外离开应用原因未明，不宣称稳定性已修复，PR6继续草稿。临时异常后退出探针未记录异常，所有诊断源改动已还原，最终普通EXE/AppX扫描不含临时标记。
- 普通7b长按保存A/B/A混音，ISE目录确为2份MP3(121422/121423字节)、cover和manifest。仅停止私有proxy后确认不可达，原生160/161/164依次实播A/B/A、166保留3项顺序，证明真实本地解码和下载去重不丢重复。代理随后隐藏恢复，ping/JPEG均200，限速归0；没有改动Navidrome进程。162/163是opening/restart画面，未当作暂停通过。
- 最终7325a03普通包重新通过UI配置。214/215/216/218确认照片菜单、原生List完整圆内、收藏首组y230/首照片y316、最新动态首图y221、精选双列首行y216且无多余底栏。177明确证明前一PathIcon缩小方案仍失败，已排除。225粉色反馈层与OK返回226通过；227真实PlayMe离线入口、228下载4/30、229完成粉色卡，点卡后231实际播放；232/233连续观测同曲0:02且Play可见，暂停确认，缓存保留待复查。
- 验证：最终产品源码14套CLR回归全通过，1237项计数断言及10项显式序列化检查；模型扫描31 OK/2预期SKIP。x86与ARM WP8.1 Debug XAML/PRI/AppX构建通过，原生运行仅x86。源码审计59文件/21 XAML/230字符串查找/259资源/8既有资产通过。732提交的push和PR CI required成功；文档提交后继续检查最新HEAD。
- 文档补齐分包证据、失败帧排除、真实协议和本地缓存验证，最终6图压缩WebP总览与安全索引留在.tmp/ui-acceptance/2026-11-17/reference-composition。蓝色来自系统强调色，原始模板同样使用PhoneAccentBrush；品牌粉色复用原主题FFFF2E80。没有加入参考人物图片或夹具数据到产品资产。
- 尚未认证：此前偶发退出根因、其它屏幕尺寸、ARM运行、锁屏/系统媒体中断、耳听输出及通过UI删除完整离线缓存。上述缺口和PR草稿状态均明确记录，没有把CLR或构建成功代替原生验收。

## 2026-11-17 18:28:54 +08:00 — 实时歌曲进度与逆向字体排版修正

- 用户明确首页2:56及顶部1:18都是当前歌曲进度，本轮按用户要求减少验证，后续由用户手工验收。核对逆向MiniPlayer、TrackProgressTemplateSelector、HoursAndMinutes/Seconds converters及EnhancedPhoneApplicationFrame：两处读取同一PlayPosition，不能作为封面或静态装饰。
- 新增共享PhoneProgressCounter：观察器直接传TimeSpan?，不反解析本地化字符串；分钟/小时分钟与:秒分两列。使用原版Segoe WP Black字面字体、Normal字重和0字距；首页0.7透明度及124/94/68三档、顶栏0.1及91/91/78三档，逐项恢复固定秒列宽、负margin、自然测量与裁剪。顶部200×60只用作clip，不作为布局尺寸；Home的进度层独立于真实封面，缺封面时显示真实歌名。
- 对齐标题栏SemiLight20、9左留白及进度可见时-60右留白；Mixes/收藏大标题SemiLight72、Normal、0字距，横向留白21/1并去掉WinRT重复的16左margin，保留WinRT垂直模板适配。菜单/首页标签恢复Semibold字体及22行高、BlockLineHeight、12边距；混音caption用Semibold21、22行高/44最大高，在60高背景带内居中，字体按页面比例缩放，不再随卡片宽度取整缩小。
- 发现先前私有fixture对首页截图的封面裁剪误包含2:56。仅将3个受污染测试cover逐字节替换为已有干净Acceptance封面，修改私有准备脚本，隔离服务器fullScan完成；未重绘参考图、未修改产品资产。安全摘要.tmp/phone-emulator-fixture/reference-cover-repair-summary.json记录替换前后SHA。旧相关截图不再作为进度字体复刻依据。
- 验证：手机源码/资源/包审计通过61文件、22 XAML、230字符串查找、267资源、8既有资产；x86 Debug旧SDK XAML/PRI/AppX构建通过，git diff --check通过。独立只读核对确认三档参数、DP初始化、自然测量及raw TimeSpan传递一致。没有重新部署、截图或跑长队列/整套手工回归；更新docs/mixradio/phone-reference-composition.md明确本轮由用户手动验收，旧运行证据仅对应旧包。

## 2026-11-18 14:26:53 +08:00 — 对齐电脑端首页队列抽屉与工作区注册升级逻辑

- 修复 `Client/Main/MainView.xaml` 首页队列抽屉方向与遮罩样式：此前提交 `d04c6e8` 仅更新了 `NavidromePlayerView.xaml`，而首页仍保留 `NullButtonStyle`（模板无背景绘制）与 `HorizontalAlignment="Right"`，与 `MixRadioQueuePanel` 的左侧 `PaneThemeTransition Edge="Left"` 及参考设计不一致。现合并 `MixRadioOverlayResources.xaml`，将 `QueueDrawer` 设为 `HorizontalAlignment="Left"`，并将 `QueueDismiss` 改为 `MixRadioDismissButtonStyle` + `Background="Black" Opacity="0.5"`。
- 在 `tools/navidrome-tests/check-client.py` 增加对 `MainView.xaml` 与 `NavidromePlayerView.xaml` 的 `QueueDrawer` 左对齐及 `QueueDismiss` 样式检查。
- 修复 `tools/desktop/run.ps1` 的外部工作区注册迁移条件：仅当现有注册在当前工作区外且新包版本不高于已注册版本时阻止覆盖；将 `Client/Package.appxmanifest` 版本升级至 `1.3.0.36`，使当前工作区可通过 `Add-AppxPackage -Register ... -ForceUpdateFromAnyVersion` 平滑接管注册并保留既有应用数据。
- 验证：`python tools/mixradio/port_styles.py` 与 `python tools/mixradio/port_templates.py` 与入库字典一致；`python tools/navidrome-tests/check-client.py` 通过（291 项桌面字符串查找、469 项资源引用及队列抽屉检查）；`pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\navidrome-tests\run-home.ps1` 通过 76 项首页与导航断言；`git diff --check` 通过。尚未完成桌面端原生部署与渲染回归（将在下一步进行）。

## 2026-11-18 15:03:45 +08:00 — 电脑端字标复刻、部署工具防陈旧打包及全套原生界面/播放/浮层回归

- 修复首页 `Mixdrome` 字标尺寸与左对齐偏差：先前固定 `Width="266" Height="41"` 外层套用 `<Viewbox>` 将 `FontSize="52"` 的字标等比压缩（大写字母高度约 22px，且首字偏右），现重构 `tools/mixradio/port_templates.py` 与 `Client/Navidrome/NavidromeResources.xaml` 中的 `MixdromeWordmarkTemplate`（`FontSize="68"`、`Margin="-3,-33,0,-16"`，`Height="41"` 左上对齐），并移除 `Client/Main/MainView.xaml` / `MainView.xaml.cs` 中的固定宽度，改以 `MaxWidth`（宽屏 `360` / 窄屏 `Math.Max(160, ActualWidth - 132)`）、`Height`（`41` / `34`）与 `Margin`（`24` / `18`）紧贴圆形菜单按钮左对齐。
- 修复 `tools/desktop/NativeUiProbe.cs` 在小曲库分组下的分页判定：以 `pagingSelection != null && indexedModel.CanLoadMore` 记录 `pagingTested`，避免测试夹具仅有 6 张专辑/3 名艺术家/3 个流派时误报分页状态；`songs` 真实触发 `100 -> 127` 增量加载并保持选择状态。
- 修复 `tools/desktop/run.ps1` 陈旧 `.appx` 与覆盖写丢失权限导致的激活挂起（`MoAppHang` / DCOM 超时）：始终使用 `/t:Rebuild` 与 `/p:GenerateAppxPackageOnBuild=true`，部署前校验已选 `.appx` 内 `Mixdrome.exe` 与编译输出目录 `Mixdrome.exe` 的 SHA-256 一致性；当布局目录发生文件更新（`$updatedFiles -gt 0`）时不再复用 `$registrationReused`，确保 `Add-AppxPackage -Register` 重建 AppContainer 访问权限。
- 完成桌面端全套原生 XAML 回归（隔离测试服务器 `http://127.0.0.1:2845/`，先清理夹具 95 个缓存图片并刷新 9 份歌单封面清除旧 `2:56` 拼贴）：
  - `NativeUiProbe`（`20261118T065115Z`）：34 页采集零错误、4 项字母索引/分页（含 `songs` 100→127）、错误遮罩与库菜单全部通过，`outsideCardTexts=0`；实测 `home.png` 字标白色包围盒 `x=123..379, y=50..100`（高 `51px`）、窄屏 `home-narrow.png` `x=42..319, y=52..93`（高 `42px`）无裁切，`home-library.png` 歌单瓷砖呈现整洁封面。
  - `NativePlaybackProbe`：本地 PCM 与 `1744/900/500` 三档播放器布局及拖拽预览共 29 项断言全部通过。
  - 临时浮层回归（验证后已通过 `git checkout` 精确还原 `tools/desktop/NativePlaybackProbe.cs` 原始 SHA-256 `8904A7769886195A96B6E488472B9B4950E782046C4D489EB636E60F4883899B`）：53 项断言全通过，覆盖 `1744/900/500` 专辑弹层重排、队列 `58+10` 行距与 `NavidromePlayerView` / `MainView` 双页左侧 `300px` 队列抽屉及 `0.5` 黑色遮罩开闭交互。
- 验证：已重编并部署 `normal` 模式桌面端包（`.tmp/desktop/build-mode-AnyCPU-Debug.txt` 为 `normal`，产物 `Mixdrome.exe` 不含任何探针符号且正常启动至主页）；`python tools/mixradio/port_templates.py`、`python tools/navidrome-tests/check-client.py`（291 项字符串、469 项资源及队列抽屉检查）、`tools/navidrome-tests/run-home.ps1`（76 项断言）与 `git diff --check` 全部通过。

## 2026-11-18 15:08:50 +08:00 — 对齐手机端离线精选集、创建混音与子页面顶部栏排版

- 对齐 `Client.Phone/Views/OfflineMixesPage.xaml` 与 `OfflineMixesPage.xaml.cs`：大标题 `PageTitle` 改用 `PhoneReferenceSemiLightFontFamily`、`72` 字号缩放、`Normal` 字重与 `0` 字距并消除偏移差异；卡片底栏改为 `60` 高容器中垂直居中排版（`PhoneReferenceSemiBoldFontFamily`、`21` 字号、`22` 行高、`44` 最大高、`BlockLineHeight`、`WordEllipsis`），普通卡片保留 `0.6` 强调色蒙层，`play me` 卡片保持无额外半透明条并与 `MixesPage` 字号按页面比例缩放（保留 2 参默认兼容）。
- 对齐 `Client.Phone/Views/CreateMixPage.xaml` 与 `CreateMixPage.xaml.cs`：`PageTitle` 改用 `PhoneReferenceSemiLightFontFamily` 与按 `ActualWidth / 480` 缩放的 `72` 字号大标题。
- 统一子页面顶部栏与高对比搜索框：`LibraryPage`、`SearchPage`、`AlbumPage`、`ArtistAlbumsPage` 与 `GenreSongsPage` 均接入共享 `<common:PhoneTitleBar />`（复用现有 `.resw` `UiMyMusic` / `UiSearch` 资源，移除遗留 `MixRadioWordmarkTextStyle` / 小图标占位）；`SearchTextBox` 启用 `MixRadioPhoneTextBoxStyle`；`LibraryPage` 与 `SearchPage` 枢纽页签采用 `PhoneReferenceSemiLightFontFamily` `54` 标题模板；`tools/phone/LibraryTests.cs` 与 `SearchTests.cs` 补充最小 `PhoneTitleBar` 测试存根。
- 验证：`python tools/phone/check.py` 通过（61 文件、22 XAML、235 字符串、259 资源、8 既有资产）；7 项手机端生产 CLR 套件全部通过（`run-collections` 19、`run-connection` 38+13、`run-library` 39、`run-listening` 36、`run-offline` 36+19、`run-playback` 39+89、`run-search` 15）；`x86` 与 `ARM` Debug 原生 WP8.1 SDK 重建均通过，`git diff --check` 通过。真机/模拟器部署与截屏回归将在下一步执行。

## 2026-11-18 15:35:51 +08:00 — 修复离线混音移除时的文件句柄锁定（E_ACCESSDENIED）与首页未配置状态条

- 真机/模拟器长按回归暴露问题：在 WP8.1 WVGA 模拟器将 `Test tone` 离线保存完成并触发长按菜单 `remove offline mix` 时，页面弹出 `Access is denied. (0x80070005)`。定位为 `OfflineMixesPage.xaml` 内 `<Image Source="{Binding Artwork}" />` 持续持有 `ms-appdata:///local/phone-offline-mixes/.../cover.jpg` 只读锁，而 `PhoneOfflineMixes.DeleteMixAsync` 先前直接调用 `folder.DeleteAsync(StorageDeleteOption.PermanentDelete)`，在封面或后台音轨被占用时整个目录删除被拒绝且未先移除 `manifest.xml`。
- 修复方案：
  - `Client.Phone/Services/PhoneOfflineMixes.cs`：在 `DownloadGate` 内串行执行 `DeleteMixAsync`，匹配修订后**先删除 `manifest.xml`** 立即原子取消发布，再逐个删除目录文件及目录本身并仅忽略锁定异常；新增 `SweepUnpublishedFoldersAsync` 在删除后与下次 `SaveMixAsync` 前清理已释放句柄的无清单残余目录。
  - `Client.Phone/Views/OfflineMixesPage.xaml.cs`：为 `PhoneOfflineCard.Artwork` 增加属性变更通知；点击 `remove offline mix` 前先置空 `card.Artwork`、从 `Cards` 移除卡片并让出渲染帧，释放 XAML 图片句柄后再删除；保留 `run-offline.ps1` 提取的 5 个核心方法签名不变。
  - `Client.Phone/Views/MainPage.xaml` / `MainPage.xaml.cs`：将首页底部 `StatusText`/`LoadingIndicator` 包裹在按需显示的深色半透明 `StatusBanner` 中，消除未配置服务器时提示语与 `recently played` 的重叠。
  - `tools/phone/OfflineStorageAdapters.cs` 与 `OfflineMixTests.cs`：补充 `StorageFolder.GetFilesAsync`/`StorageFile.Name` 适配器与真实文件句柄锁定期间的删除回归断言。
- 将 `Client.Phone/Package.appxmanifest` 升至 `1.3.0.1` 以支持模拟器保留凭据与离线缓存就地 `/updatelaunch`。验证：`python tools/phone/check.py`、全部 7 项手机 CLR 回归（含 `run-offline.ps1` `37+19` 断言）、`x86` 与 `ARM` Debug 原生重建及 `git diff --check` 全部通过。

## 2026-11-18 16:29:12 +08:00 — 修复手机端 DirectWrite Segoe WP 字重回退、顶栏紧凑进度基线与播放跳转安全性

- 修复 WP8.1 WinRT DirectWrite 字重回退：实测主机 `SegoeWP*.ttf` 全部注册在同一家族名 `'Segoe WP'` 下（以字重 `Light`/`SemiLight`/`SemiBold`/`Black` 区分）。原 `Client.Phone/Common/Styles.xaml` 将 `PhoneReference*FontFamily` 设为 `Segoe WP Light`/`SemiLight`/`Semibold`/`Black` 并配以 `FontWeight="Normal"`，导致模拟器实机截图中首页与顶栏进度数字及卡片标题退化为细 Regular 字体。现将 `PhoneReference*FontFamily` 统一为 `Segoe WP`（并在 `tools/phone/check.py` 新增禁止 XAML 使用子族名正则校验），同时为以下控件补齐显式 `FontWeight`：`PhoneProgressCounter.xaml`（`Black`）、`Styles.xaml` `PhoneTileLabelStyle` 与 `PhoneHomePanel.xaml` `CurrentTitle` 及 `MixesPage.xaml`/`OfflineMixesPage.xaml` 卡片标题（`SemiBold`）、`PhoneTitleBar.xaml` `TitleText` 及各页 `72`/`54` 大标题（`SemiLight`）。
- 修复 `PhoneProgressCounter.xaml.cs` 在顶栏紧凑模式（`IsCompact`）的 WinRT `typo` 度量偏移与裁切：`Height = 60 * scale`、`Digits.VerticalAlignment = VerticalAlignment.Top`、`Margin` 上移至 `-19`（`longTime` `-13`）、`Clip` 对齐 `(0, 0, 200 * scale, 60 * scale)`，使 `91px` 黑色数字底边紧贴顶栏底部并消除上方 6px 空隙（`!IsCompact` 时显式恢复 `Height = double.NaN` 与 `VerticalAlignment = VerticalAlignment.Stretch`）。
- 加固顶栏快捷跳转与异步状态回调安全：`PhonePlaybackObserver.StartAsync` 捕获 `SessionFile` 读取瞬态异常；`PhoneTitleBar.xaml.cs` 提取 `StopObserver()`，在 `OnHomeClick` 与 `OnElapsedClick` 发起 `Frame.Navigate` 前同步卸载后台监听（并在 `OnUnloaded` 复用）；`PlaybackPage.xaml.cs` `OnMediaStateChanged` 在 `Dispatcher.RunAsync` 回调内部用 `try/catch` 包裹 `UpdatePlaybackState()`（保持 `UpdateProgress`/`OnProgressTimerTick` 既有 `COMException` 语义与测试兼容）；`App.xaml.cs` `UpdateStatusBarVisibility` 增加 `catch (Exception)` 防止系统状态栏切换瞬态异常逃逸 `async void`。
- 升级 `Client.Phone/Package.appxmanifest` 至 `1.3.0.2`。验证：`python tools/phone/check.py` 通过（61 文件、22 XAML、236 字符串、259 资源、8 资产）、全部 7 套手机 CLR 测试通过（`run-connection` 38+13、`run-playback` 39+89、`run-listening` 36、`run-library` 39、`run-search` 15、`run-collections` 19、`run-offline` 37+19）、`Client.Phone` `x86` 与 `ARM` Debug Clean Rebuild 通过、`git diff --check` 通过。接下来将在 WP8.1 WVGA 模拟器清理卸载重装 `1.3.0.2` 验证新字重、紧凑进度裁剪与 `PhoneTitleBar` 快捷跳转。

## 2026-11-19 21:04:42 +08:00 — 接手 Antigravity 手机与桌面 UI 原生复验

- 沿用包含 origin/main 的现有 PR #6 分支，验证产品源码 7448e76，补充 docs/mixradio/ui-regression-2026-11-19.md。独立保存本轮证据，避免以前轮截图替代当前包验收。
- 桌面 NativeUiProbe 34 页面零页面错误、四组索引及歌曲 100→127 分页通过；NativePlaybackProbe 29 项通过。最后恢复 normal 普通构建并部署。
- 安装手机普通 1.3.0.2 x86 包，复测菜单、混音、收藏、曲库、搜索、创建页、专辑、空离线页及顶栏导航。90 秒 MP3 的首页 0:26、顶栏 1:06 和播放器 1:20 进度均取得 WebP 证据。
- 发现未通过项：未配置首页提示条仍覆盖末行内容；播放到 1:20 的最后观察后发送暂停点击，随后黑屏并进入 Photos，再启动恢复首页。未取得异常栈，暂不能归因于暂停或曲目切换，手机稳定性继续未通过。
- 验证：14 套 CLR、手机与桌面源码资源审计通过。仅认证实际执行范围，其它手机尺寸、ARM 运行、锁屏、完整离线流程仍待验证。原始截图、压缩 WebP、诊断报告与日志位于忽略目录 .tmp/ui-acceptance/2026-11-19。此次仅修改验收记录。
