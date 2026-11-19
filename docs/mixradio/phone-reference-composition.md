# Phone reference composition and acceptance

Recorded: **2026-11-17T14:37:38+08:00**.

This records the second composition pass against the eight screenshots supplied
on 17 November. It supplements [the original phone-port investigation](phone-port.md)
and supersedes that document's initial create-mix, favourites and offline feature
mapping. The reference is MixRadio; the implemented product remains Mixdrome,
using the configured Navidrome account and the existing WP8.1 WinRT project.

The supplied Store reference is [Microsoft Store product 9wzdncrfhwt4](https://apps.microsoft.com/detail/9wzdncrfhwt4?hl=en-US&gl=US).
Measurements below come from the supplied screenshots and recovered original
XAML. The Store link alone does not establish runtime behavior.

## Progress and typography correction after manual feedback

The large `2:56` on home and the subdued `1:18` in the title bar are the
**current song's playback position**. Both now receive the same raw native
`TimeSpan` from the read-only playback observer. Artwork remains an independent
image; elapsed time is rendered as two live text blocks: minutes / hours-and-
minutes on the left and `:ss` on the right. This supersedes the earlier single
text-block approximation and its typography acceptance.

The shared counter reproduces the recovered `uicontrols/miniplayer.xaml` and
`themes/generic.xaml` three-way selection. All values below scale with design
width; `200×60` is a clip rectangle, never a forced counter layout size.

| Position | Home: size / seconds column / margin | Title bar: size / seconds column / margin |
| --- | --- | --- |
| Under 10 minutes | 124 / 174 / `-100,0,0,-34` | 91 / 174 / `0,-42,-50,-34` |
| 10 minutes or more, under an hour | 94 / 132 / `-60,0,0,-26` | 91 / 132 / `0,-42,-8,-34` |
| An hour or more | 68 / 96 / `-20,0,0,-18` | 78 / 108 / `0,-42,0,-34` |

Both use the actual **Segoe WP Black** family with normal font weight and zero
character spacing. Home text opacity is 0.7 and clips to the full tile; title-bar
opacity is 0.1, the counter is vertically centered, and its clip is
`0,-7,200,60`. The title uses Segoe WP SemiLight 20 with a left inset of 9.
Mixes headers use SemiLight 72 with normal weight and zero character spacing;
caption text uses Semibold 21, block line height 22, maximum height 44 and
horizontal inset 12, vertically centered inside the 60-high band. Caption type
scales with the page rather than being shrunk by whole-DIP card-width rounding.

The disposable fixture's former home-screenshot crop had accidentally included
the reference `2:56` in three JPEG covers. Those private covers have been
replaced with an existing clean artwork file, and the private preparation script
no longer crops artwork from that screenshot. They were never product assets.
Earlier screenshots using those covers cannot establish elapsed-text fidelity.

Per the user's request, this correction is limited to source/resource checks and
an x86 SDK build. It is **not deployed or newly screenshot-certified**; visual
comparison and native interaction are left for the user's manual verification.

## Measurement method

All eight reference images are **768 × 1280**. Divide their pixel coordinates by
**1.6** to compare them on the original **480 × 800** design surface. Values below
are rounded design coordinates, not assumptions about WinRT device-independent
pixels. For example, a 207-unit card becomes 207 physical pixels on a 480-pixel
wide emulator screenshot even when that emulator's XAML content width is 400 DIPs.

Page geometry derives its scale from actual page width / 480. Native item-panel
cell sizing also uses the rendered content width; source arithmetic alone cannot
establish that two columns fit after native measurement and layout rounding.
Lists retain native scrolling and virtualization rather than placing the entire
app inside a scaled fixed canvas.

The recovered reference templates use `PhoneAccentBrush` / `PhoneAccentColor`
for normal tiles and translucent captions. Final captures therefore show the
emulator's blue accent; the supplied reference device uses yellow. The fixed
Play Me / feedback pink reuses the recovered theme's `#FFFF2E80` value.

## Reference measurements

| Image | Surface | Key geometry at 480 × 800 |
| --- | --- | --- |
| 1 | Home with recently played content | Wordmark starts about `(24, 31)`. Large current-album tile starts `(24, 168)` and is 228 square. The 168-square Play Me tile starts about `(84, 408)`. Second column starts near `x264`; recent covers are 96 square, with staggered vertical positions. Normal gaps are 12. Recently played link sits near `(24, 750)`. |
| 2 | Home menu | First row starts `(24, 168)`, second column `x204`. Normal tiles are 168 square with 12 gaps. Favourites spans 348 × 168 at `(24, 348)`. My music/offline row starts near `y528`. Full-page artist photograph remains behind the tiles. |
| 3 | Mixes / featured | System status area is about 32 high, then the application title bar is 60 high. Large Pivot text starts near `y115`. Photo cards start `(24, 216)` and `(249, 216)`, are about 207 square, and have an 18 gap. Caption band is 60 high at 0.6 accent opacity; label text is about 20–21. Second card row starts near `y441`. |
| 4 | Play Me player | Same 32 + 60 top chrome. Artist photograph fills the page. Cover starts about `(24, 454)` and is 194 square; track metadata starts near `x230`. Feedback circles are about 44 diameter, centered near `x252/342/432`, `y618`. Progress starts near `(24, 654)` and spans 432. Pause/next circles are about 58 diameter, centered around `(180, 725)` and `(300, 725)`. |
| 5 | Offline mixes | Large title starts near `y115`; explanatory text near `(24, 223)`. First cards start `(24, 282)` / `(249, 282)` and use the same 207-square / 60-caption geometry. A downloading card adds a thin progress line around `y493` and status text below it; that added height affects the next row. |
| 6 | Like feedback | Pink panel is about 328 high. Bold title starts near `(24, 23)`; body starts near `(24, 93)`. Circular thumbs graphic is about 80 diameter at `(376, 68)`. Outlined OK button is about 204 × 48 at `(26, 256)`. The player beneath is dimmed. |
| 7 | Favourites / artists | Large Pivot heading starts near `y115`. First letter block starts `(24, 230)` and is 68 square. First artist photo starts `(24, 315)` and is 90 square. Name starts near `x126`, uses about 32 bold text and wraps. Photo rows retain about 12 spacing. Bottom bar has add/select commands and overflow. |
| 8 | Favourites / what's new | First image starts `(24, 221)` and is 120 square. Title/subtitle start near `x156`, using about 32 bold / 20 text. Normal image-row gap is about 12; a long title increases the row height rather than clipping its text. Original calendar/gig artwork describes an original service capability, not data that can be invented for Navidrome. |

Recovered source corroborates the 228/168/96 home tiles, 207-square mix cards,
60-high caption band, 68-square letter headers, 90-square artist pictures and
20/32 metadata typography. The reference has both a special Play Me presentation
and ordinary library/queue navigation; the special presentation is selected by
actual playback context rather than replacing all library behavior.

## Visual and data rules

- Ordinary menu tiles, favourite letter blocks, selected feedback circles and mix
  caption bands use the **system phone accent**. Yellow in the supplied reference
  is the reference device's theme. The acceptance emulator's blue accent is valid.
- Play Me and the feedback panel use the separate MixRadio pink reference color
  (`#FFFF2E80`). Pink is not the application's substitute system accent.
- Backgrounds use actual artist images returned by the configured server. With
  no usable artist photograph, the backdrop remains black. An album cover or a
  packaged music-note placeholder does not become a full-screen artist portrait.
- Cover/artist image failures use packaged foreground fallbacks. Collection image
  fallbacks update the bound item so later real artist metadata can still replace
  them. A failed initial request must not remove the image binding.
- Original names, concerts, notification counts and recommendation claims are not
  copied into the product. Labels use resources; metadata and identities come from
  real protocol responses.

The live fixture exposed a useful protocol distinction: `getArtistInfo2` returned
empty image values while `getArtist` supplied a usable OpenSubsonic
`artistImageUrl`. `ExpandedArtist` now retains that attribute. The phone adapter
uses it when artist information succeeds with an empty image URL. Unsupported
endpoints, account failures and network failures remain explicit errors; the
client does not infer support from English exception messages.

## Functional mapping

| Original interaction | Current Navidrome behavior |
| --- | --- |
| Home / recently played | Show actual current playback metadata, recent albums and server playlists. The menu keeps the reference's mixes/create/favourites/my music/offline arrangement. |
| Play Me | Request real random songs from the current server and start an actual mix queue. This does not imply OEM recommendation learning. |
| Mixes / featured | Show actual server playlists. A server without playlists can show actual recent albums. Opening a playlist preserves its complete entries, order and duplicate occurrences. A home playlist tile can pass its stable ID directly. Returning from the player does not automatically start it again. |
| Mixes / categories | Show real server genres with a cover from an actual genre song. Playback loads all genre-song pages; a later-page error does not start a partial mix. |
| Create a mix | Select a real genre, obtain up to 30 server-random library songs, save a named server playlist with `createPlaylist`, then play it. It does not reproduce the original commercial seed-artist service. |
| Favourites / artists | Show actual starred artists, grouped from their Unicode names and culture-aware ordering. Add performs a real artist `star`; multi-select removal performs real `unstar` requests. |
| Favourites / what's new | Filter server `newest` album pages by stable favourite artist IDs, including multiple-artist metadata. Entries retain server order. They represent additions to this library, not fabricated release dates, gigs or ticket notifications. |
| Favourites / albums and songs | Keep access to real starred albums and songs. Song play-all/shuffle uses the actual songs and the queue's shuffle state. |
| Like / dislike / favourite | Like writes rating 5, dislike writes rating 1 and can advance the actual queue, and favourite toggles the server song's starred state. Feedback copy describes that server operation and makes no personalization-learning claim. |
| Offline mixes | Long-press a real mix, resolve its actual songs, then enter the offline page. The service verifies account identity and download/stream roles, re-fetches song metadata, downloads a compatible MP3 copy and artwork, and atomically publishes a completed manifest. Queue occurrence order is retained while repeated files are stored once. |

Offline downloads are currently **foreground-page operations**. Leaving the
offline page or cancelling stops the current request and cleans incomplete
storage. This is not a background download service. Only complete manifests with
available local playback files appear as playable downloads. Storage is account
isolated, local queue files are validated before playback, and removing a saved
mix deletes its own local files. The PC offline service is not presented as a
phone capability merely because both clients share download primitives.

## Native evidence before the final layout pass

Local evidence directory:

```text
.tmp/ui-acceptance/2026-11-17/reference-composition/
```

These are emulator captures from the native WP8.1 XAML build. They establish the
observed flows below and also record defects that triggered further edits. They
are **not acceptance of the later layout/background/feedback changes**.

| Captures | What was observed | Remaining/revised interpretation |
| --- | --- | --- |
| `023-mixes-featured`, `024-mixes-categories` | Real playlist/genre metadata and photo cards render in the native page. | `023` exposed one column with unused right-hand space, heading text near `y125`, and first card near `y231`. The native panel now receives an explicit cell width from actual content width / 2; title spacing was tightened toward text `y115` and first card `y216`. Those edits require a new capture. |
| `025-mix-player`, `026-mix-player-ready` | A real mix opens the dedicated player presentation and resolves playable metadata/artwork. | Subsequent artist-image transition and feedback-composition refinements require the final package's own evidence. |
| `027-like-feedback`, `028-feedback-dismissed`, `029-paused` | Like feedback opens, OK dismisses it, and the pause interaction produces the corresponding native presentation. | A separate backend read confirms the rating mutation; a screenshot alone does not establish a server write. Final feedback styling is still to be recaptured. |
| `030-returned-menu`, `033-home-menu-paused`, `034-home-mini-player` | Navigation returns to the menu and current playback appears in the home/mini-player composition. | Final artwork transition and status-bar changes are subject to the last capture pass. |
| `031-favourites-artists` | Real artist JPEGs, a letter group, large names and elapsed shortcut (`0:05`) render successfully. | First group was at `y255` and first photo near `y341`, against targets `y230`/`y315`. Common heading spacing was reduced about 15, then group top spacing another 10. Photo-row spacing was retained. New positions are not yet verified here. |
| `032-favourites-updates` | Actual newest-library albums for the starred artists appear, with real covers and wrapped titles. | First photo was near `y231` against target `y221`. After the common heading reduction, this list receives 5 additional top units. New geometry is pending capture. |
| `035-favourites-selection`, `036-favourite-selected`, `037-favourite-removed` | Enter selection, select Fixture artist 1 and remove it from the visible favourites list. | The selection icon was subsequently replaced by a neutral three-line checklist path to match the reference. No backend count was sampled during the intermediate removed state. |
| `038-add-artist-dialog`, `039-add-artist-selected`, `040-artist-added-back` | The real candidate list opens, Fixture artist 1 is selected, and it is added back. | The read-only backend check below confirms the restored server state. |

### Backend and photo checks

All fixture records and media below are local acceptance data, not shipped
application assets or hardcoded product metadata. Reference photo/artwork regions
were extracted into the disposable media library, followed by a real full scan.
The server supplied the resulting IDs and image URLs.

Safe local summaries:

```text
.tmp/phone-emulator-fixture/reference-photo-fixture.json
.tmp/phone-emulator-fixture/reference-photo-artists.json
.tmp/phone-emulator-fixture/reference-collection-fixture.json
.tmp/phone-emulator-fixture/native-feedback-collection-backend-040.json
```

- Photo verification fetched each of the three actual artist image URLs and
  decoded JPEG responses: two at 600 × 519 and one at 331 × 235. Artist cover-art
  requests also returned JPEGs. The summary records format/dimensions and stable
  metadata without authenticated URLs or credentials.
- Fixture preparation created six real server playlists from actual album song
  entries: five with 25 songs and one with the two 90-second acceptance tracks.
  They all received server cover-art IDs. Three artists, two albums and two songs
  were starred for the native collection flows.
- The **read-only check at `2026-11-17T14:33:42.564080+08:00`**, after capture 040,
  found three starred artists including Fixture artist 1. Scanning all 127 songs
  found Fixture track 040 with `userRating = 5`, supporting the real like operation.
  This verifies the restored state, not an unobserved intermediate count of two.

Production-source checks passed for collection grouping/identity/full queues
(19 assertions), the phone connection/adapter path (38), artwork URI handling
(13), and serialization (33 model types plus explicit artist-image metadata).
The phone source/resource/package audit also passed. These checks supplement
native evidence and do not prove screenshot geometry, native image decoding or
offline playback on the emulator.

## Native acceptance status across successive packages

Updated: **2026-11-17T17:18:00+08:00**. Capture numbers identify the local evidence
sequence, not a single unchanged binary. Earlier ordinary packages, the temporary
exception-probe package and the latest ordinary package must remain distinct.

| Stage / evidence | Confirmed scope | Qualification |
| --- | --- | --- |
| Earlier ordinary package: `074-final-menu-background` and `layout-measurement.json` | All five complete menu tile rectangles match the normalized reference: each measured x/y/width/height delta is zero. | This measures tile exteriors only. It is not a whole-screen similarity score and excludes typography, icons, background and animation. |
| Subsequent layout captures: `077-final-favourites-artists`, `078-final-favourites-updates` | Artist letter block is at `y230`, first photo at `y316`; what's new first photo is at `y221`. | The one-pixel photo difference from the approximate `y315` target is recorded. Later title top-margin, 20-square checklist icon and bottom-bar/status-panel changes still need final ordinary captures. |
| Native panel correction: `092-native-grid-panel-loaded` | Mixes renders two columns, first row at `y216`, with approximately 207-square cards. | `075` had proved the previous fractional-width/`ItemsPanelRoot` attempt still produced one column. The replacement uses the panel's own Loaded/SizeChanged events, actual scroll viewport and whole-DIP `floor(viewport / 2)` cells. The later removal of the Mixes bottom bar and empty status space needs final recapture. |
| Short queue: `095-transition-end-state`, `097-duplicate-queue` | The short native queue reaches its end, and the duplicate-entry case retains three queue occurrences. | This validates the exercised short queue, not overall stability of every 30-song run. Fixture order summaries are `transition-playlist-acceptance.json` and `artist-transition-playlist-acceptance.json`. |
| Throttled native download: `112-throttled-download-progress`, `113-throttled-cancel-return` | Deliberately slowed MP3 transfer exposes real native progress; cancel/return and incomplete-download cleanup were exercised. | Throttling belongs to the private fixture proxy. This is cancellation/cleanup evidence, not acceptance of completed offline playback or of a background download service. |
| Ordinary create-mix flow: `126-created-mix-playback` plus `native-create-mix-backend.json` | Native creation saves a real playlist. Backend readback finds 30 songs / 30 unique IDs, 180 seconds total, matching listing/detail counts, stable repeated ordered readback and the current fixture owner. | Creation and server persistence are verified. Two earlier runs exited unexpectedly; their causes remain unresolved. This row does not certify uninterrupted 30-song playback. |
| Temporary exception-probe package: `137-exact-30-queue-start` and `exact-queue-trace/frame-01` … `frame-24` | The exact saved 30-song queue progresses to its normal end in this run; no exception-probe log was produced. | This is an instrumented package's result. It neither explains the two earlier exits nor proves the final ordinary package stable. |

The backend summaries named above live under
`.tmp/phone-emulator-fixture/`; trace frames and numbered captures live in the
native evidence directory. They remain local acceptance artifacts and omit
credentials/authenticated request URLs from the safe summaries.

### Ordinary-package results and final recapture

The uninstrumented `7b504eb` package completed the same real saved queue.
`ordinary-queue-trace/` has 24 full frames: 01–19 show track progression;
20–24 remain on Fixture track 108 at `0:06 / 0:06`, with Play visible and Next
disabled. No exit occurred in this run. Its EXE/AppX hashes are preserved in
`ordinary-queue-7b-binary-identity.json`, separate from the later binary identity.
**The earlier intermittent exits are not explained or accepted as fixed.**

The final ordinary source is `7325a03`, including offline selection ownership and
the native `Symbol.List` icon. The 20-square custom PathIcon still rendered
incorrectly in `177`; it is excluded as a passing icon result. No diagnostic
handler, timer or trace remains in product source or the installed final binary.

| Check | Observed scope / evidence |
| --- | --- |
| Ordinary 30-song queue | `149`, `ordinary-queue-trace/frame-01` … `frame-24`: this `7b504eb` run completes normally. This is not a claim about all runs. |
| Mixes viewport | `156-normal-mixes-featured`: two columns start at `y216`; third row continues below the former bottom bar/status gap. |
| Favourites geometry | `153` / `154`: first letter `y230`, first photo `y316`, updates photo `y221`, Compact artists bar and Minimal updates bar. Custom checklist rendering failed in `153` and `177`; native List recapture follows separately. |
| Home / artist transitions | `151` shows real current artwork and elapsed shortcut; `152` shows all five menu rectangles over the parallax artist image. Ordinary trace contains actual transitions between the fixture artists' images. |
| Status bar | `138`, `151`, `152`, `167`, `176` hide it on home; `140`, `153`, `154`, `156`, `169`, `177` show it on their respective destination pages. |
| Completed local playback with server unreachable | `158` / `159`: true long-press download and ready card. With only the private proxy stopped and confirmed unreachable, `160` starts A, `161` plays B at `0:01`, `164` reaches final A at `0:06`, and `166` shows A/B/A. ISE lists exactly two MP3 files (121,422 / 121,423 bytes), JPEG and manifest. Native local decoding and duplicate occurrence preservation are verified on `7b504eb`; this was proxy unreachability, not airplane mode or ear-listening verification. |
| Offline cancellation | `112` / `113` plus directory readback establish incomplete-cache cleanup. Completed-cache removal is still not native-verified. |
| Final native List | `215-final-favourites-native-list`: the platform List glyph is fully inside the native circle, with no clipping or overflow. `214-final-config-saved` confirms the final ordinary menu with the actual server photo after UI configuration. |
| Final ordinary composition | `214-final-config-saved`, `215-final-favourites-native-list`, `216-final-favourites-updates`, `218-final-mixes-two-columns`: final menu, native icon, artists/updates geometry and full two-column viewport. Blue is the current system accent. |
| Final player / feedback | `223` reaches actual Play Me playback; `224` pauses. `225-final-pink-feedback` opens the 328-high pink panel, with 80-square icon and 204×48 OK button; OK returns to `226-final-feedback-dismissed-paused`. The rating message describes the real server rating operation rather than claiming unavailable personal-mix recommendation training. Earlier backend evidence separately confirms the rating write. |
| Final Play Me offline entry and playback | `227` exposes the eligible queue's offline command; `228` shows real `4/30` transfer progress; `229` shows the completed pink Play Me card. Opening the card reaches actual playback (`231`, `0:05 / 0:06`). `232` and repeated `233` stay on Fixture track 024 at `0:02` with Play visible, confirming pause. The proxy is restored in this final run; server-unreachable decoding is separately established by the `7b504eb` run above. |

`162` / `163` were intended pause checks but captured opening/restart states;
their filenames do not establish a successful pause. `128` records the prior
unexpected departure to Photos. Neither is promoted into a passed result.
Private fixture photos, media, traces and credentials are not shipped assets.

Final ordinary source validation: all 14 CLR suites passed, with 1,237 counted
assertions plus 10 explicit serialization checks. The model scan reports 31 OK
and two intentional SKIP entries. Both x86 and ARM WP8.1 Debug builds passed;
only x86 was run in the emulator. The source/resource audit passed 59 source
files, 21 XAML files, 230 string lookups, 259 resources and eight existing assets.
`final-binary-identity.json` binds the installed EXE/AppX to `7325a03` and finds
none of the temporary diagnostic identifiers. Subsequent documentation-only
commits do not change that installed product source.

The local final overview is `final-overview-732.webp`; its index records the six
complete native frames and their hashes. These remain local review artifacts.
PR #6 remains a draft until the earlier intermittent exit is explained; this
pass does not certify ARM execution, other screen sizes, lock-screen/system
interrupt recovery, ear-listening output or completed-cache removal through UI.

