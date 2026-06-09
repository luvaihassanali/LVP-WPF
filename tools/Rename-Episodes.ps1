<#
.SYNOPSIS
    Bulk-renames scene-release TV episode files to LVP-WPF's "N % Title.ext"
    convention by pulling episode titles from TMDB.

.DESCRIPTION
    Use this AFTER you've sorted episodes into "Season N" subfolders but
    BEFORE running Validate-Show.ps1 / launching LVP-WPF. It walks every
    "Season N" subdir under -RootPath, finds .mkv files matching the
    "S##E##" pattern in the filename, looks up each episode's real title
    from TMDB, and renames the file to "{NN} % {Title}.mkv".

    Show identity is resolved in this order:
      1. -ShowName 'X'        explicit override (always wins)
      2. "Show % (year) ..."  part before '%'  (your % convention)
      3. "Show (year) ..."    part before '('  (year-suffix convention)
      4. "Show.Foo.Bar..."    dotted scene name; cut at first metadata
                              marker (S##, Complete, 1080p, year, etc.)
      5. Otherwise            folder name unchanged

    Always start with -DryRun. The script makes no backups and Rename-Item
    is not reversible without manually fixing each file.

    Two-pass design:
      Pass 1 (pre-flight)  for every Season N folder, parse each disk
                           file's episode number and compare the SET of
                           disk numbers against the SET of TMDB episode
                           numbers. Three failure modes are distinguished:
                             - missing episodes (disk subset of TMDB):
                               fatal by default, allow with -AllowMissing
                             - duplicate episode number on disk: always fatal
                             - disk has number TMDB doesn't (wrong-season
                               file or wrong show id): always fatal
                           If ANY season fails, print all problems and exit
                           with code 1 - NO files are renamed. Prevents the
                           "Season 1 got renamed before Season 3's missing-
                           episode problem was detected" failure mode.
      Pass 2 (rename)      only runs if every season passed pass 1.
                           Reuses the TMDB data fetched during pass 1 -
                           one network call per season total.

    Output naming:
      - Title is sanitized: Windows-illegal chars (\ / : * ? " < > |)
        replaced with a space, runs collapsed, trimmed.
      - Format: "{0:D2} % {1}.mkv" -> "04 % Social Psychology.mkv"
      - This is the format Validate-Show.ps1 expects, so a clean rename
        round-trips through the validator with no title-mismatch warnings.

    Supported video extensions: see $VideoExtensions below. Each file
    keeps its original extension on rename (Show.S01E04.foo.mp4 ->
    "04 % Title.mp4", .mkv -> "04 % Title.mkv", etc.).

    Folders ignored during pre-flight: see $IgnoredFolders below. "Extras"
    is silently skipped because it never follows the S##E## naming and
    LVP-WPF's scanner has its own non-counted handling for it.

    Limitations:
      - No recursion: -RootPath must directly contain Season N subfolders
      - Top TMDB search result is used blindly; pin ambiguous shows via
        -ShowName 'Exact TMDB Title'
      - Idempotent: re-running on already-renamed files is a no-op (they
        won't match S##E## and are skipped with a warning)

.PARAMETER RootPath
    Required. The folder containing "Season 1", "Season 2", ... subfolders
    of .mkv files to rename. Show name is derived from this folder name
    unless -ShowName is passed.

.PARAMETER ShowName
    Optional. Override the show name used for TMDB lookup. Use this when
    auto-derivation from -RootPath picks the wrong title or when the
    folder name is too noisy to parse.

.PARAMETER ApiKey
    TMDB v3 API key. Defaults to the hardcoded key (same one used by
    Validate-Show.ps1 and the LVP-WPF app).

.PARAMETER DryRun
    Print what would be renamed without touching disk. Recommended for
    every first run on a new show.

.PARAMETER AllowMissing
    Opt-in: allow pre-flight to pass when disk has FEWER episodes than
    TMDB (e.g. you know S08E18 doesn't exist in your library and you're
    OK with that). Each missing episode number is logged. Disk files
    that map to numbers NOT on TMDB, or duplicate disk numbers, still
    abort pre-flight - those indicate real corruption, not just gaps.

.EXAMPLE
    .\Rename-Episodes.ps1 -RootPath 'E:\Downloads\Community...' -DryRun
    Auto-derives "Community" from the folder name, fetches titles, prints
    every rename it would do.

.EXAMPLE
    .\Rename-Episodes.ps1 -RootPath 'E:\Downloads\Community...'
    Same but actually performs the renames.

.EXAMPLE
    .\Rename-Episodes.ps1 -RootPath 'E:\Downloads\Community...' -ShowName 'Community' -DryRun
    Force-pin the show name when auto-derivation picks the wrong TMDB hit
    (e.g. a documentary with the same name ranks above the sitcom).

.NOTES
    Source format auto-detection: episode range is extracted by
    Get-EpisodeRange, which tries FIVE patterns in order. Pattern 0 fires
    on already-LVP-formatted files (the script's own output convention),
    which makes the renamer idempotent and lets re-runs on a clean library
    parse correctly instead of mis-reading "01#02 % T1#T2.mkv" as just E01:

      0. "N % Title" / "N#N2 % T1#T2" / "N#N2#N3 % T1#T2#T3" - LVP format
         already in place. The file's '%' marker triggers this branch.
         Episode count is taken from title.Split('#').Length to match
         MediaEnricher.EnrichMultiEpisode's title-is-authoritative semantics
         (so "01 % T1#T2#T3" reads as E01-E03, even though the prefix says
         only E01). Examples:
         e.g. 01 % The Awakening.mkv                                       (single)
         e.g. 10#11 % The Day of Black Sun (1)#The Day of Black Sun (2).mkv (double)
         e.g. 18#19#20#21 % Sozin's Comet (1)#(2)#(3)#(4).mkv               (quad)

      1. "S##E##" (with optional separator and range marker)
         Modern scene releases. Recognized separators between S## and E##:
         space, dot, dash, underscore, or nothing. Range markers: E##E##,
         E##-E##, E##-##. Underscore-everywhere filenames also work.
         e.g. Community.S01E04.Social.Psychology.1080p.x265.mkv         (single)
         e.g. Saturday Night Live - S01E01 - George Carlin (10-11-75).mkv  (single)
         e.g. The WILD Thornberrys - S01 E01 - Flood Warning.mp4         (single, spaced)
         e.g. Show.S01E04E05.Title.mkv                                   (multi)
         e.g. Show.S01E04-E05.Title.mkv                                  (multi)
         e.g. Show.S01E04-05.Title.mkv                                   (multi)
      2. "##x##" with optional "-##" range marker - older releases
         e.g. Forensic Files - 14x11 HDTV - Water Logged.mp4             (single)
         e.g. Show 1x01-02 Title.mkv                                     (multi)
      3. "SS.NN " - season-dot-episode at the start (SNL season 5+)
         e.g. 05.04 - Buck Henry, Tom Petty and the Heartbreakers.avi
              (SS is informational; NN is taken as the episode number)
      4. "NN..."  - bare leading number, any separator follows
         e.g. 04. Steve Martin, Van Morrison (11-04-1978).avi
         e.g. 01 Thompson Twins.avi
         e.g. 09.avi                            (index-only filename)
    Patterns 3/4 use the parent "Season N" folder name as the season -
    that's how SNL S4+ files work where only the episode index is in the
    filename. \b word-boundary anchors keep things like 1920x1080
    (resolution) and 1980 Special (year) from false-matching.

    Multi-episode output format mirrors what the LVP scanner expects:
      single  -> "01 % Title.ext"
      double  -> "01#02 % Title1#Title2.ext"
      triple  -> "01#02#03 % Title1#Title2#Title3.ext"
    The scanner reads the first number from the prefix as the start
    episode and uses the count of '#'-separated titles to figure out how
    many consecutive episodes the file covers. Including the consecutive
    numbers in the prefix makes the filename human-readable and round-
    trips cleanly through Validate-Show.ps1.

    NB: cast/guest info present in disk filenames (e.g. "Andy Kaufman" in
    the SNL example) is DROPPED on rename - only the canonical TMDB title
    is kept. If you want to preserve disk titles instead of fetching from
    TMDB, this script is the wrong tool.
#>
param (
    [Parameter(Mandatory = $true)]
    [string]$RootPath,
    # Optional override. If not set, derived from $RootPath's folder name -
    # see Resolve-ShowName below for the parsing rules.
    [string]$ShowName,
    [string]$ApiKey = "c69c4effc7beb9c473d22b8f85d59e4c",
    [switch]$DryRun,# = $true
    [switch]$AllowMissing
)

# File extensions treated as episode video files. Lowercased; compared
# case-insensitively against $file.Extension. Add anything else your sources
# ship (e.g. '.divx', '.mpg') if you encounter them.
$VideoExtensions = @('.mkv', '.mp4', '.avi', '.m4v', '.mov', '.wmv', '.ts', '.webm', '.flv')

# Subfolders of -RootPath that should be silently skipped (no warning, no
# pre-flight check). Anything else that isn't "Season N" still emits a
# warning so genuine typos (e.g. "Seaosn 3", "S1") don't slip through.
$IgnoredFolders = @('Extras', 'Specials', 'Featurettes')

# Pull a clean show name out of a scene-release folder.
#   1. Explicit -ShowName always wins.
#   2. "Show % (year) ..."  -> part before '%'  (your % convention)
#   3. "Show (year) ..."    -> part before '('  (year-suffix convention)
#   4. "Show.Foo.Bar..."    -> dotted scene name; cut at the first segment
#      that looks like a season/quality/year marker (S01, Complete, 2009, etc.)
#   5. Otherwise return the folder name unchanged.
function Resolve-ShowName([string]$rootPath, [string]$override) {
    if ($override) { return $override.Trim() }
    $folder = [IO.Path]::GetFileName($rootPath.TrimEnd('\', '/'))

    if ($folder.Contains('%')) {
        return ($folder -split '%', 2)[0].Trim()
    }
    if ($folder.Contains('(')) {
        return ($folder -split '\(', 2)[0].Trim()
    }
    if (-not $folder.Contains(' ') -and $folder.Contains('.')) {
        # Dotted scene name. Walk segments and stop at the first metadata marker.
        $segments = $folder.Split('.')
        $stop = $segments.Length
        for ($i = 1; $i -lt $segments.Length; $i++) {
            if ($segments[$i] -match '^(S\d{2}(-S\d{2})?|Season|Complete|Series|\d{4}|1080p|2160p|720p|480p|BluRay|BDRip|DVDRip|WEB|WEBRip|WEB-DL|HDTV|HDRip|x264|x265|HEVC|REMUX)$') {
                $stop = $i
                break
            }
        }
        return ($segments[0..($stop - 1)] -join ' ').Trim()
    }
    return $folder.Trim()
}

function Get-TvShowId {
    param ([string]$showName)
    $url = "https://api.themoviedb.org/3/search/tv?api_key=$ApiKey&query=$([uri]::EscapeDataString($showName))"
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        return $response.results[0].id
    } catch {
        Write-Warning "Failed to fetch TV show ID: $_"
        return $null
    }
}

function Get-EpisodeTitles {
    param (
        [int]$tvId,
        [int]$seasonNumber
    )
    $url = "https://api.themoviedb.org/3/tv/$tvId/season/$seasonNumber" + "?api_key=$ApiKey"
    Write-Host $url
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        $titles = @{}
        foreach ($episode in $response.episodes) {
            $titles[$episode.episode_number] = $episode.name
        }
        return $titles
    } catch {
        Write-Warning "Failed to fetch episode titles for Season $seasonNumber`: $_"
        return @{}
    }
}

# Extract the episode number from a filename. Tries (in order):
#   1. Modern "S##E##"             "Show.S01E04.foo.mkv"           -> 4
#   2. Older  "##x##"              "Forensic Files - 14x11 HDTV"   -> 11
#   3. Leading "SS.NN" notation    "05.04 - Buck Henry, Tom Petty" -> 4
#                                  (the SS is the season; we use NN as episode)
#   4. Leading "NN" with separator "04. Steve Martin (date).avi"   -> 4
#                                  "01 Thompson Twins.avi"          -> 1
#                                  "09.avi"  (index-only)           -> 9
# Returns $null when nothing matches. The season number is NOT derived here -
# it's already known from the parent "Season N" folder name and gets paired
# with this episode number by the caller.
#
# \b anchors on the S##E## / ##x## variants keep resolution markers like
# 1920x1080 from false-matching. \b on the leading-number variants stops
# years like "1980 Special.avi" from matching as a 3-digit episode (since
# \d{1,3} would otherwise greedily eat 198 then fail at the boundary check).
function Get-EpisodeRange {
    param ([string]$fileName)

    # Returns @{ Start = <int>; End = <int> } for a parsable filename, or
    # $null when no pattern matches. Single-episode files have End == Start.
    # Multi-episode files have End > Start (e.g. S01E01-E02 -> Start=1, End=2),
    # matching the LVP convention where one file spans consecutive episodes
    # and MediaEnricher's EnrichMultiEpisode walks startEpNum..startEpNum+n-1.
    #
    # Pattern 0 (CHECKED FIRST): already-LVP-formatted filenames.
    # Triggers when the stem contains '%'. Matches anything the C# scanner
    # would accept:
    #   "01 % Title"                 -> 1..1
    #   "01#02 % T1#T2"              -> 1..2
    #   "01#02#03 % T1#T2#T3"        -> 1..3
    #   "01 % T1#T2#T3"              -> 1..3   (title-only multi - matches
    #                                           MediaEnricher.EnrichMultiEpisode,
    #                                           which uses title.Split('#').Length
    #                                           as the episode count regardless
    #                                           of what the prefix says)
    # This MUST come before patterns 1-4 below: an LVP filename with "1x9"
    # or similar in its title would otherwise false-match the ##x## pattern,
    # and "01#02 % ..." would false-match the leading-NN pattern as just E01.
    # It also makes the renamer idempotent: re-running on an already-renamed
    # library round-trips file names instead of corrupting them.
    $stem = [IO.Path]::GetFileNameWithoutExtension($fileName)
    $pct  = $stem.IndexOf('%')
    if ($pct -gt 0) {
        $prefix = $stem.Substring(0, $pct).Trim()
        $title  = $stem.Substring($pct + 1).Trim()
        # Prefix must be a digit OR a '#'-separated chain of digits.
        # Anything else (letters, spaces, etc.) bails out and falls through
        # to the scene-release patterns below.
        if ($prefix -match '^\d+(#\d+)*$') {
            $start = [int]($prefix.Split('#')[0])
            $count = $title.Split('#').Count
            return @{ Start = $start; End = $start + $count - 1 }
        }
    }

    # Pattern 1: "S##E##" + optional second episode marker (multi-aware)
    #   S01E04            -> 4..4    (single)
    #   S01E04E05         -> 4..5    (E##E## variant)
    #   S01E04-E05        -> 4..5    (E##-E## variant)
    #   S01E04-05         -> 4..5    (E##-## variant)
    #   S01 E01           -> 1..1    (space separator, see anchor notes below)
    #   S01.E01           -> 1..1    (dot separator)
    #   Show_S04E03_Title -> 3..3    (underscore around)
    #
    # Anchors: (?<![A-Za-z]) and (?![A-Za-z0-9]) instead of \b on either side.
    # \b treats underscore as a "word character", so "Show_S04E03_Title" has
    # no \b between "_" and "S" - the match would silently fail on
    # underscore-separated scene releases. The lookbehind / lookahead pair
    # uses explicit character classes that allow underscore as a delimiter
    # while still rejecting partial matches like "FooS04E03Bar" or "E045".
    if ($fileName -match '(?<![A-Za-z])S(\d{2})[ ._-]?E(\d{2})(?:[-E]?E?(\d{2}))?(?![A-Za-z0-9])') {
        $start = [int]$matches[2]
        $end   = if ($matches[3]) { [int]$matches[3] } else { $start }
        return @{ Start = $start; End = $end }
    }

    # Pattern 2: older "##x##" / "#x##" + optional range marker
    #   14x11             -> 11..11  (single)
    #   14x11-12          -> 11..12  (range)
    #   14x11x12          -> 11..12  (less common but seen in some sources)
    #   1x01-02           -> 1..2    (single-digit season, common in older
    #                                 DVD-era rips like "Show 1x01-02 Title")
    # Season is \d{1,2} so we catch single-digit seasons too. The two-digit
    # cap keeps "1920x1080" (resolution) from matching - 1920 is 4 digits
    # with no \b break, so the regex backtracks to no match.
    if ($fileName -match '\b(\d{1,2})x(\d{1,2})(?:[-x]?(\d{1,2}))?\b') {
        $start = [int]$matches[2]
        $end   = if ($matches[3]) { [int]$matches[3] } else { $start }
        return @{ Start = $start; End = $end }
    }

    $stem = [IO.Path]::GetFileNameWithoutExtension($fileName)
    # SS.NN must come BEFORE the bare-NN check; otherwise "05.04 - Title"
    # would match the bare-NN regex and return 5 (season) instead of 4
    # (episode). No multi-episode form for these - they're variety-show
    # one-episode-per-file conventions.
    if ($stem -match '^(\d{1,3})\.(\d{1,3})\b') {
        $n = [int]$matches[2]
        return @{ Start = $n; End = $n }
    }
    if ($stem -match '^(\d{1,3})\b') {
        $n = [int]$matches[1]
        return @{ Start = $n; End = $n }
    }
    return $null
}

function Sanitize-FileName {
    param ([string]$name)
    # Replace Windows-illegal filename chars AND '#' with space, then collapse
    # any whitespace runs the substitution created. "Mission: Accomplished"
    # would otherwise become "Mission  Accomplished" (two spaces - original
    # plus colon's replacement); collapsed it reads "Mission Accomplished".
    #
    # '#' is in the strip set because LVP's multi-episode convention uses '#'
    # as the separator inside titles (e.g. "Pilot Part 1#Pilot Part 2"). A
    # TMDB title that contains '#' (rare but possible) would corrupt the
    # multi-title joining downstream, so we kill it here.
    #
    # This also makes round-tripping through Validate-Show.ps1 clean - that
    # script's title normalizer strips illegal chars + collapses whitespace,
    # so disk and TMDB sides compare equal.
    $clean = [RegEx]::Replace($name, '[\\\/:*?"<>|#]', ' ')
    $clean = [RegEx]::Replace($clean, '\s+', ' ').Trim()
    return $clean
}

# Main script
# Sanity-check the API key BEFORE the first network call. TMDB v3 keys are
# 32 hex chars; anything else means the caller probably typo'd a parameter
# (e.g. "--DryRun" with two dashes lands here as a positional value bound
# to $ApiKey, because PowerShell sees "--" as a non-name token).
if ($ApiKey -notmatch '^[0-9a-f]{32}$') {
    Write-Error "ApiKey '$ApiKey' is not a valid TMDB v3 key (expected 32 hex chars). Did you typo a parameter, e.g. '--DryRun' instead of '-DryRun'? All switches take a single dash."
    exit 1
}

$tvShowName = Resolve-ShowName -rootPath $RootPath -override $ShowName
Write-Host "Show name resolved to: '$tvShowName'" -ForegroundColor Cyan
$tvId = Get-TvShowId -showName $tvShowName
if (-not $tvId) {
    Write-Error "TV Show ID not found for '$tvShowName'. Override with -ShowName '<actual name>'. Exiting."
    exit
}
Write-Host "TMDB id: $tvId  ->  https://www.themoviedb.org/tv/$tvId" -ForegroundColor Cyan
if (-not $DryRun) {
    Write-Host "WARNING: live rename mode. Re-run with -DryRun to preview first." -ForegroundColor Yellow
}

# ----- Pass 1: pre-flight episode-count validation ---------------------------
# Walk every Season N folder and compare disk .mkv count vs TMDB's reported
# episode count for that season. If ANY season disagrees we abort BEFORE
# touching disk - blind continuing past a mismatch leaves you with a
# half-renamed library (missing files won't reveal themselves until the
# rename pass tries to apply a title to a number that isn't on disk, and
# any extra/misplaced files get blank titles or wrong-season titles).
#
# We cache each season's $episodeTitles hashtable so pass 2 doesn't re-hit
# TMDB - one network round trip per season total.
Write-Host "`nPre-flight validation:" -ForegroundColor Cyan
$validationErrors = @()
$seasonsToProcess = @()

Get-ChildItem -Path $RootPath -Directory | Sort-Object Name | ForEach-Object {
    if ($IgnoredFolders -contains $_.Name) {
        # Silent skip - Extras / Specials / etc. don't follow S##E## naming
        # and have their own handling in LVP-WPF's scanner. Suppressing the
        # warning here keeps pre-flight output focused on real problems.
        return
    }
    if ($_.Name -notmatch 'Season (\d+)') {
        Write-Warning "Folder name does not match 'Season X': $($_.Name)"
        return
    }
    $seasonNumber  = [int]$matches[1]
    $seasonFolder  = $_.FullName
    # No -Filter - one wildcard can only target one extension. Enumerate all
    # files, then keep only the ones with a video extension.
    $diskFiles     = @(Get-ChildItem -Path $seasonFolder -File | Where-Object { $VideoExtensions -contains $_.Extension.ToLower() })
    $episodeTitles = Get-EpisodeTitles -tvId $tvId -seasonNumber $seasonNumber

    if ($episodeTitles.Count -eq 0) {
        $validationErrors += "  Season $seasonNumber : TMDB returned 0 episodes (season may not exist on TMDB - check show id $tvId)"
        return
    }

    # Parse each disk filename to extract its episode range (single or
    # multi-episode), then do set comparison against TMDB. This is stronger
    # than count-vs-count - it distinguishes between three failure modes:
    #   - missing episode(s)         disk numbers are a strict subset
    #                                of TMDB numbers (OK with -AllowMissing)
    #   - duplicate episode number   same number on two disk files (always fail)
    #   - wrong-season file mixed in disk has a number TMDB doesn't (always fail)
    #
    # Multi-episode handling: a file like "S01E01-E02.foo.mkv" expands to
    # disk numbers [1, 2] - both episodes are claimed by that one file. If
    # another file also claims episode 2 (single or as part of a range),
    # the dupes check fires.
    $diskNumbers = @()
    $unparseable = @()
    foreach ($f in $diskFiles) {
        $range = Get-EpisodeRange $f.Name
        if ($null -ne $range) {
            for ($n = $range.Start; $n -le $range.End; $n++) {
                $diskNumbers += $n
            }
        } else {
            $unparseable += $f.Name
        }
    }
    $tmdbNumbers = @($episodeTitles.Keys | ForEach-Object { [int]$_ })

    $diskOnly = @($diskNumbers | Where-Object { $_ -notin $tmdbNumbers })       # disk has, TMDB doesn't
    $tmdbOnly = @($tmdbNumbers | Where-Object { $_ -notin $diskNumbers })       # TMDB has, disk doesn't (the "missing" case)
    $dupes    = @($diskNumbers | Group-Object | Where-Object { $_.Count -gt 1 })

    $seasonBad = $false

    if ($unparseable.Count -gt 0) {
        $validationErrors += ("  Season {0} : {1} file(s) the regexes couldn't parse (tried S##E##, ##x##, SS.NN, leading NN): {2}" -f $seasonNumber, $unparseable.Count, ($unparseable -join ', '))
        $seasonBad = $true
    }
    if ($dupes.Count -gt 0) {
        $dupeList = ($dupes | ForEach-Object { "E$('{0:D2}' -f [int]$_.Name) x$($_.Count)" }) -join ', '
        $validationErrors += ("  Season {0} : duplicate episode numbers on disk: {1}" -f $seasonNumber, $dupeList)
        $seasonBad = $true
    }
    if ($diskOnly.Count -gt 0) {
        $extraList = ($diskOnly | Sort-Object | ForEach-Object { "E$('{0:D2}' -f $_)" }) -join ', '
        $validationErrors += ("  Season {0} : disk has episode(s) TMDB doesn't: {1} (wrong-season file mixed in? wrong show id $tvId?)" -f $seasonNumber, $extraList)
        $seasonBad = $true
    }

    if ($seasonBad) { return }

    # Missing episodes - the legitimate gap case. Fatal without -AllowMissing.
    if ($tmdbOnly.Count -gt 0) {
        $missingList = ($tmdbOnly | Sort-Object | ForEach-Object {
            "E$('{0:D2}' -f $_) `"$($episodeTitles[$_])`""
        }) -join '; '
        if (-not $AllowMissing) {
            $validationErrors += ("  Season {0} : missing {1} episode(s) - {2}. Re-run with -AllowMissing to proceed anyway." -f $seasonNumber, $tmdbOnly.Count, $missingList)
            return
        }
        Write-Host ("  Season {0,2} : {1,2}/{2,-2} on disk - MISSING {3} (allowed via -AllowMissing): {4}" -f $seasonNumber, $diskNumbers.Count, $tmdbNumbers.Count, $tmdbOnly.Count, $missingList) -ForegroundColor Yellow
    } else {
        Write-Host ("  Season {0,2} : {1,2}/{2,-2} video file(s) match TMDB" -f $seasonNumber, $diskNumbers.Count, $tmdbNumbers.Count) -ForegroundColor Green
    }

    $seasonsToProcess += [pscustomobject]@{
        Number = $seasonNumber
        Folder = $seasonFolder
        Titles = $episodeTitles
    }
}

if ($validationErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "Aborting before any rename - episode count mismatch:" -ForegroundColor Red
    foreach ($e in $validationErrors) { Write-Host $e -ForegroundColor Red }
    Write-Host ""
    Write-Host "Investigate: missing/duplicate downloads? wrong show id? TMDB season numbering?" -ForegroundColor Yellow
    Write-Host "Fix the source files (or pin the right show with -ShowName) and re-run." -ForegroundColor Yellow
    exit 1
}

if ($seasonsToProcess.Count -eq 0) {
    Write-Warning "No 'Season N' folders found under $RootPath - nothing to rename."
    exit 0
}

# ----- Pass 2: rename (only reached if every season passed pre-flight) -------
foreach ($s in $seasonsToProcess) {
    Write-Host "`nProcessing Season $($s.Number) at $($s.Folder)"

    # Materialize the file list BEFORE the loop. Get-ChildItem | ForEach-Object
    # is a streaming pipeline - lazy enumeration that re-reads the directory
    # as it goes. When Rename-Item lands a new "01 % Title.mkv" in the same
    # directory the enumerator was scanning, the new file gets picked up
    # later in the same pass and emitted as a "doesn't match S##E##" warning.
    # Wrapping in @() forces full enumeration up front, so the foreach
    # iterates a fixed snapshot and renames don't re-enter the loop.
    $files = @(Get-ChildItem -Path $s.Folder -File | Where-Object { $VideoExtensions -contains $_.Extension.ToLower() })
    foreach ($file in $files) {
        # All format-parsing logic lives in Get-EpisodeRange so pre-flight
        # and rename stay in sync. Anything pre-flight accepted will parse
        # here too; if a brand-new file slipped in between the two passes
        # and doesn't parse, we skip with a warning rather than crashing.
        $range = Get-EpisodeRange $file.Name
        if ($null -eq $range) {
            Write-Warning "Filename does not match expected format: $($file.Name)"
            continue
        }

        # Collect TMDB titles for every episode in the range. Bail (skip
        # this file) if ANY episode in the range has no TMDB title - half-
        # renaming would produce a multi-episode filename with one part
        # blank, which the scanner / enricher would then choke on.
        $titleParts = @()
        $missingTitle = $false
        for ($n = $range.Start; $n -le $range.End; $n++) {
            $t = $s.Titles[$n]
            if (-not $t) {
                Write-Warning "No title found for S$($s.Number) E$n"
                $missingTitle = $true
                break
            }
            $titleParts += (Sanitize-FileName $t)
        }
        if ($missingTitle) { continue }

        # LVP multi-episode convention:
        #   single  -> "01 % Title.ext"
        #   double  -> "01#02 % Title1#Title2.ext"
        #   triple  -> "01#02#03 % Title1#Title2#Title3.ext"
        # The scanner only reads the FIRST number from the prefix and uses
        # the count of '#'-separated titles to know how many episodes the
        # file spans, but including the consecutive numbers in the prefix
        # makes the filename human-readable and round-trips cleanly through
        # Validate-Show.ps1.
        $prefix = if ($range.End -gt $range.Start) {
            (($range.Start)..($range.End) | ForEach-Object { '{0:D2}' -f $_ }) -join '#'
        } else {
            '{0:D2}' -f $range.Start
        }
        $sanitizedTitle = $titleParts -join '#'
        # Preserve the original extension - sources mix .mkv / .mp4 / .avi
        # and we don't want to silently change the container.
        $newFileName = "$prefix % $sanitizedTitle$($file.Extension.ToLower())"

        # No-op detection: case-insensitive compare because NTFS is case-
        # insensitive (renaming "Foo.mkv" to "foo.mkv" doesn't actually do
        # anything on disk). Treats those files as already-matching too.
        $isNoOp = [string]::Equals($file.Name, $newFileName, [StringComparison]::OrdinalIgnoreCase)

        if ($DryRun) {
            if ($isNoOp) {
                Write-Host "[DryRun] Would NOT rename '$($file.Name)' (already matches TMDB)" -ForegroundColor Green
            } else {
                Write-Host "[DryRun] Would rename '$($file.Name)' to '$newFileName'"
            }
        } else {
            if ($isNoOp) {
                # Skip the Rename-Item entirely - it would either silently
                # succeed (modern PowerShell) or throw "destination path
                # already exists" (older); not worth either branch when
                # there's nothing to change.
                Write-Host "Already matches TMDB: '$($file.Name)'" -ForegroundColor Green
            } else {
                try {
                    Rename-Item -Path $file.FullName -NewName $newFileName
                    Write-Host "Renamed '$($file.Name)' to '$newFileName'"
                } catch {
                    Write-Warning "Failed to rename file: $_"
                }
            }
        }
    }
}
