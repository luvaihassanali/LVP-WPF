Rebuild of [Local Video Player](https://github.com/luvaihassanali/LocalVideoPlayer) using Windows Presentation Foundation
- LibVLCSharp used to render media
- The Movie Database (TMDB) API used to download images (movie posters, episode backdrops, season posters, etc.) and media descriptions
- Custom 72x72 animated cursor for large screen sizes

https://user-images.githubusercontent.com/35501080/221096811-8aea3390-9389-44d9-a633-7b54f032359d.mp4

## Keyboard

Every keypress is routed through the same `IrSerialReader.OnCommand`
pipeline the IR remote uses, so keyboard input behaves identically to
remote input - same debounce, same threading marshalling for the player,
same log lines.

| Key | IR-equivalent | Effect |
|---|---|---|
| Up / Down / Left / Right | up/down/left/right | navigation (arrow nav) |
| Enter | enter | activate focused control |
| Esc | return | back / close current window |
| Space | play | toggle play/pause in player |
| F | fastforward | +30s |
| R | rewind | -30s |
| End | forward | jump to end |
| Home | backward | jump to start |

Held arrow keys auto-repeat (matches IR held-button behavior). Held
action keys are filtered so a held Enter doesn't fire as a stream of
one-shots. Modifier-combos (Ctrl/Shift/Alt+key) are ignored.

Active in both Debug and Release builds.

## TODO

In-code `//To-do` markers (`grep -ri 'to-do' --include='*.cs'`). All four
are in the multi-language TV-show pipeline.

- [ ] **Detect file extension changes and episode deletions** during
      cache-rebuild planning.
      `LVP-WPF/MediaLibrary.cs:73` — inside `Initialize`, in the
      `if (needsRebuild)` branch.

- [ ] **Don't assume English is the first language folder** when picking
      the default `show.Seasons` for a multi-lang show.
      `LVP-WPF/Services/LibraryScanner.cs:101` — inside
      `ProcessTvDirectory`, the `folderName.Length == 2` branch that
      drills into the first language folder.

- [ ] **Don't assume `en` is at index 0** in the language-folder array.
      The loop currently starts at `i = 1`, implicitly skipping
      whatever sorts first alphabetically (usually but not always `en`).
      `LVP-WPF/Services/LibraryScanner.cs:124` — inside
      `ProcessMultiLangTvDirectory`.

- [ ] **Preserve build-cache ordering for shows with 3+ languages**
      when switching languages via the dropdown. Current logic finds by
      `Contains(lang)` which can match the wrong index when the language
      list isn't alphabetical.
      `LVP-WPF/Windows/TvShowWindow.xaml.cs:456` — above
      `SwitchMultiLangTvIndex`.
