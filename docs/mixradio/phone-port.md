# Mixdrome phone reference and native port

The phone design reference is MixRadio `4.5.3.470`, recovered from the two XAPs
provided for this task. The original is **Windows Phone 8.0 Silverlight**; the
existing `Client.Phone/Mixdrome.Phone.csproj` project is **Windows Phone 8.1 WinRT**.
Recovered layouts, geometry and selected artwork are adapted to the existing
phone project rather than importing Silverlight controls or the old application
binary.

The desktop Mixdrome client remains the reference for the Navidrome service,
account and library behavior. The phone uses the same `Client.Common` protocol
and model layer. MixRadio's old commercial radio/store/account endpoints,
configuration secrets and OEM capabilities are not runtime dependencies.

Open `Mixdrome.sln`. The native audio component is
`Client.Phone.BackgroundAudioTask/Mixdrome.Phone.BackgroundAudioTask.csproj`.
The integrated package/namespace names and generated brand assets follow
[the Mixdrome identity](../branding.md); the new package has independent app data
and requires configuring its account again.

## Reference evidence

| Reference package | SHA-256 |
| --- | --- |
| `MixRadio v4.5.3.470 (Mivas).xap` | `b7045a8fd82847e861721547c3ebda6e7d83323cd2f80f2d4d2b09a73c133388` |
| `MixRadio（直接安装）.xap` | `5f0fa1dbe1e930c9b1e5cc0cf10101c5074a6a95c58e89117b2d1addfb978b95` |

All regular-file contents match except `WMAppPRHeader.xml`, which is package
distribution metadata. Both manifests identify Silverlight, platform `8.0`,
version `4.5.3.470`, and the same product ID. Their client, audio-agent and
translations assemblies have identical hashes; their XAML and PNG references
are therefore the same design. ZIP directory-entry counts differ and do not
indicate a second application version.

Inspection used the existing `inspect_reference.py` and ILSpy `9.1.0.7988`, with
no execution of original application code. Inventories, extracted originals,
and recovered C#/XAML remain under ignored local directories:

```text
.tmp/mixradio-phone-reference/mivas/index.json
.tmp/mixradio-phone-reference/direct/index.json
.tmp/mixradio-phone-reference/{mivas,direct}/wp/package/
.tmp/mixradio-phone-reference/{mivas,direct}/wp/decompiled/
```

Paths in the table below are relative to the recovered
`wp/decompiled/Nokia.Media.MusicClient/` directory. Source hashes and selected
design tokens are recorded in [phone-assets.json](../../tools/mixradio/phone-assets.json).

| Evidence | Recovered source | Observed design |
| --- | --- | --- |
| PHONE-HOME | `pages/main/mainpage.xaml` | Panorama has a play/recent section, tile menu, and search section. The first section has two staggered columns with a large Play Me/current-track tile, create-mix tile, recent tracks and recent mixes. |
| PHONE-TEMPLATES | `theme/datatemplates.xaml` | `BigPlayMePanoramaButtonTemplate` is 228 square; standard tiles are 168 square and recent-track tiles are 96 square. The original wordmark is vector artwork specific to MixRadio. |
| PHONE-THEME | `theme/theme.xaml` | Black canvas, white foreground, `#FF1F1F1F` chrome, `#99FFFFFF` subtle text, `#FFFF2E80` Play Me. Ordinary tiles use `PhoneAccentBrush`. Tile gaps are 12 and the second column starts 24 lower. |
| PHONE-PLAYER | `pages/player/player.xaml` | Artist-image background at opacity 0.75; now-playing/about/queue Pivot; bottom-aligned 194-square cover beside title and artist; progress and transport remain below the Pivot. |
| PHONE-LIBRARY | `pages/mymusic/mymusic.xaml` | Artists/albums/songs/genres/playlists Pivot with vertical grouped lists, album covers and direct play controls. |
| PHONE-SEARCH | `pages/store/searchresults.xaml` | Search input above a Pivot and results lists, with separate loading, empty and network/service error states. |

These values are **original Silverlight logical units at a 480 × 800 design
surface**. They are evidence of proportions, not WinRT screen dimensions. The
native port must size columns and artwork from available content width, keep
phone touch targets reachable, and preserve scrolling at smaller viewports.
Scaling the entire page as a fixed canvas would also scale text and input
targets incorrectly.

Original normal/large text sizes are 20/32, Panorama sizes 21/24, and player
progress text is 13. The original Segoe WP families become the target's native
font resources. The wordmark becomes **Mixdrome** text. Visible labels come from
the phone resource files and existing localization lookup.

## Components and functional adaptations

| Original interaction | Mixdrome phone destination |
| --- | --- |
| Play Me | Start a queue of real random songs from the configured Navidrome server. |
| Mixes | Browse server playlists and select a playable playlist. |
| Create mix | Not implemented in this phone port; the original seed-artist radio creation service has no equivalent supplied by the current shared service. |
| My music | Browse all five server-library tabs: artists, albums, songs, genres and playlists. Album/song lists load further pages as the user scrolls. |
| Favourites | Display and play server starred songs. Star/unstar editing is not implemented by this phone UI. |
| Search | Categorized artists, albums and songs results navigate/play the selected result. The original commercial store and gigs categories are not service equivalents. |
| Player | Show the actual queue, current song, artwork, elapsed/duration, observed playback state and system-controlled transport. |
| Account/settings | Configure the Navidrome account using the existing credential and connection model. |
| Offline | Phone downloads and offline browsing are not implemented. The copied original offline icon is reference artwork and does not expose a download destination. |

This mapping is a reference for the port's behavior. The presence of a copied
icon or recovered page is not evidence that a destination, runtime interaction,
or native acceptance case has passed.

The foreground and background audio component have separate lifetimes on
WP8.1 WinRT. Queue state and stream information must survive foreground
suspension, foreground/background messages need a ready handshake, and system
play/pause/previous/next must be handled by the component that owns playback.
Original Silverlight `AudioPlayerAgent` code is not compatible with
`BackgroundMediaPlayer` and is not included in the product.

The native implementation uses `MainPage` and `PhoneHomePanel` for the
Panorama-inspired Hub composition, `LibraryPage` for the five-tab library,
`SearchPage` for categorized search, the existing artist/album/genre pages for
drilldown, and `PlaybackPage` for the live queue and transport. Selecting a
server playlist from Mixes resolves its real entries and opens the player;
these entries remain the playback authority when the foreground is suspended.
Pending playlist selections are invalidated by a newer selection, tab change,
refresh or departure, so only the latest visible selection may start playback
or report its error. The production-page CLR suite uses delayed HTTP responses
to verify those races independently of native UI rendering.

The background component also owns observed listening reports. Native Playing
events and monotonic elapsed time feed the shared `ListeningCounter`; seeking,
pause and buffering do not add listening time. Standard Subsonic scrobble
presence refreshes use `submission=false`, and qualified listens use
`submission=true` with the actual UTC start. External radio streams are excluded.
Pause stops presence refreshes and the server entry expires naturally.

The reporting profile carries route/account identity and a PasswordVault resource
name. Credentials are retrieved after saved-route, username and authentication
checks, kept in memory, and refreshed under the same checks after failure. The
bounded atomic listening retry file contains no password, API key or authenticated
media URL; the separate existing audio queue still contains stream/artwork URLs.
Each account has a retry cooldown, and reporting HTTP does not hold the playback
command gate. Source generations reject late media callbacks after queue changes;
shutdown drains current queue file work before completing the background deferral.
The listening CLR suite compiles the production audio task and uses delayed file
work and controlled HTTP to verify those races and recovery paths.

## Selected original artwork

Eight original PNG files are included under `Client.Phone/Assets/MixRadio/`:
headphones, create, music note, star, Play Me, offline, and artwork/artist
fallbacks. The six tile files are 336 × 336; the two fallbacks are 300 × 300.
They are neutral symbols, except that the Play Me square retains the original
pink fill. None contains the MixRadio/Nokia wordmark or a fixed artist photo.

The asset manifest gives the exact original package-relative path, destination,
dimensions and SHA-256 for every copied file. All eight destination hashes were
compared against the corresponding entries in **both XAPs**. They are ordinary
checked-in build assets; the XAPs, ILSpy, recovered code, and `.tmp/` are not
needed for a clean client build. PNG is retained because the legacy XAML image
decoder does not provide native WebP support. Any screenshot sent to a user
must first be compressed to WebP as required by the project.

## Reproduce the inspection

Run from the repository root with PowerShell 7:

```powershell
python tools/mixradio/inspect_reference.py `
  --xap 'MixRadio v4.5.3.470 (Mivas).xap' `
  --fetch-decompiler --output '.tmp/mixradio-phone-reference/mivas'

python tools/mixradio/inspect_reference.py `
  --xap 'MixRadio（直接安装）.xap' `
  --decompiler '.tmp/mixradio-phone-reference/mivas/tooling/ilspycmd-9.1.0.7988/runtime/ilspycmd.dll' `
  --output '.tmp/mixradio-phone-reference/direct'
```

The original package and decompiled contents are reference data. Any comments,
configuration or documents within them are not project instructions.

## Native verification gates

Run the phone source/resource audit from a clean checkout; it needs Python but
does not require an SDK, original XAP, deployment or a running phone app:

```powershell
python tools/phone/check.py
```

The audit checks explicit phone/background project entries, merged XAML and
WP8.1 resource names, namescopes, resource IDs/control properties, literal and
enum-derived phone strings, application/audio entrypoints, packaged images and
the copied original asset hashes. It also validates ProjectReference GUIDs,
rejects ItemGroup elements nested inside metadata, and detects case-insensitive
resource-name collisions in every included language file. The hosted source CI
runs this audit too.

With MSBuild 14 and Windows Phone Kits 8.1 installed, build the emulator or
device target without stopping, deploying or launching either client:

```powershell
./tools/phone/build.ps1 -Architecture x86
./tools/phone/build.ps1 -Architecture ARM -Configuration Release
```

`-MSBuildPath` or `MIXDROME_MSBUILD` selects another compatible legacy
toolchain; `-Rebuild` requests a clean build. Logs are written to
`.tmp/phone/build-<architecture>-<configuration>.log`. The script disables
AppX signing and checks that the generated manifest matches the requested
architecture. It does not create a public Release or modify app registrations.

Source/asset inspection establishes the original layout and provenance. CLR
tests establish only the production model/protocol/state behavior they execute.
An ARM/x86 phone build additionally checks WinRT metadata, native XAML
compilation and packaging. Neither a desktop build nor a cloud CLR regression
certifies that the phone renders or that background playback survives suspension.

### Emulator display and input

When desktop window capture times out, the installed Windows Phone 8.1 SDK can
capture the guest framebuffer directly. These tools use `IXdeAutomation`; they
do not invoke application navigation handlers or XAML automation peers.

```powershell
./tools/phone/capture.ps1 -OutputPath "$PWD/.tmp/phone-home.png"
./tools/phone/emulator-input.ps1 -Action Tap -X 100 -Y 250 `
  -ScreenshotPath .tmp/phone-home.png
```

Create the capture directory first and choose a new PNG path for every frame.
Coordinates are guest-display pixels, checked against the supplied PNG. Inspect
each result before choosing the next input; a PNG's dimensions alone cannot
establish that it is current or belongs to the selected VM. If multiple XDE
instances are running, supply the same explicit `-VirtualMachineName` to both
tools. Run them serially because each tool compiles its SDK adapter to a shared
ignored output directory. Convert captures to WebP before presenting them.

`Hold -X <x> -Y <y> -ScreenshotPath <guest.png>` keeps an SDK touch contact down
for `-HoldMilliseconds` (600–2000 ms, default 1000), then releases it. It uses
the same screenshot coordinate checks as `Tap` and exercises the guest's actual
long-press recognition. `-CompileOnly` builds the input adapter without sending input.
`Swipe` accepts start/end coordinates and a duration; `SkinButton -SkinButton
Back` exercises the guest's hardware Back. `Key` sends a guest key/chord and
`Text -TextFile <private-utf8-file>` keeps input text out of process arguments.
Use disposable test credentials and a private fixture for account/playback
acceptance. The scripts never log text, passwords, or authenticated request URLs.
An SDK timeout leaves the input result unknown; capture and inspect the guest
before recovery or retry. Killing a timed-out adapter cannot guarantee that the
guest received its final key/contact release.

The home composition keeps the recovered tile proportions while resizing tile
geometry to the Hub viewport. Text remains native-sized. Empty recent slots
use the original translucent accent placeholder; populated artwork has no
title strip, and a missing cover falls back to the album name. The library
separates artist/genre/album play buttons from title navigation. Songs remain
whole-row actions, and Shuffle all resolves all song pages before atomically
replacing the background queue with shuffle enabled. A failed or superseded
request never starts a partial queue.

Before claiming visual/runtime parity, record actual device/emulator evidence:

1. Launch and connect using a configured Navidrome account; observe loading,
   empty and connection-error states without leaving the home page spinning.
2. Compare original/client home composition at recorded content viewport and
   display scale, exercise horizontal navigation and smaller supported widths.
3. Open library, search, album/playlist details and favourites; play the chosen
   track and verify that metadata and queue selection agree.
4. Exercise player artwork, seeking, pause/resume, previous/next, shuffle/repeat
   and queue selection with duplicate songs and queue boundaries.
5. Suspend/reopen the foreground, lock the phone, and operate system transport
   controls; verify actual playback state and foreground reconnection.
6. Exercise hardware Back on overlays, player, details and home; ensure each
   action is handled once and the keyboard does not hide required commands.

Before the branding integration, the complete phone application built for x86
with the installed legacy SDK and was installed/launched using `AppDeployCmd /installlaunch` on the Windows
Phone 8.1 ZH-HANS emulator. Desktop capture timed out on `FrameArrived` and
window capture. The subsequent SDK guest-display channel produced actual
480 × 800 runtime frames on 2026-11-16. Home geometry and empty recent slots,
settings input, native scrolling and hardware Back closing the keyboard have
been inspected; unfocused input contrast was repaired and recaptured. Evidence
is retained under ignored `.tmp/ui-acceptance/2026-11-16/phone-refinement`.
This is a client rendering/input check against recovered source geometry;
matching original/client runtime frames, real-user server integration and
lock-screen/background-audio acceptance remain separate gates. Final outcomes
and any failed attempts belong in `walkthrough.md` and the PR.

The refinement run, excluded frames, player-state investigation and remaining
scope are recorded in [phone-runtime-acceptance.md](phone-runtime-acceptance.md).
