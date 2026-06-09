<#
.SYNOPSIS
    Validates a manually-renamed TV show folder against TMDB and against the
    naming conventions that LVP-WPF's LibraryScanner expects.

.DESCRIPTION
    Run this AFTER moving files into Season folders and renaming them to the
    "N%Episode Name.ext" convention, but BEFORE launching LVP-WPF for the
    first time on the new show. It collects every problem in one pass and
    prints a colored report so you can fix all of them before relaunching.

    Checks performed:

      Local (no TMDB call):
        - Season folders match "Season N" naming
        - Season numbers have no gaps
        - Every episode file uses "N%Title.ext" (or the multi-episode
          variant "N#N2%Title1#Title2.ext", "N#N2#N3%Title1#Title2#Title3.ext",
          etc. - one file covering consecutive episodes)
        - Episode number prefixes parse as integers
        - Episode numbers have no gaps, no duplicates (a multi-episode
          file covering E01-E02 contributes both 1 AND 2 to the coverage
          set; a separate file claiming E02 would be a duplicate)

      Against TMDB:
        - Show resolves on TMDB (by %ID suffix if present, else by name search)
        - Season count matches (excluding TMDB "Specials" = season 0)
        - Per-season episode count matches
        - Per-episode title matches (normalized: case-folded, illegal
          filename chars + punctuation replaced with spaces, whitespace
          collapsed - same shape on both disk and TMDB sides)

    Reads the TMDB v3 key from appsettings.local.config (same file the app
    reads) so you don't have to duplicate it.

.PARAMETER Path
    Either a single show folder (contains Season N subfolders) OR a parent
    directory containing many show folders. Auto-detected.

.PARAMETER ConfigPath
    Path to appsettings.local.config. Defaults to the LVP-WPF project copy.

.PARAMETER Only
    Optional substring filter when scanning a parent dir - only shows whose
    folder name contains this string are validated.

.PARAMETER Quiet
    Suppress per-OK lines. Only WARN/FAIL/summary are printed. Useful when
    re-validating a clean library.

.EXAMPLE
    .\Validate-Show.ps1 'E:\Media\TV\Breaking Bad'
    Validates a single show.

.EXAMPLE
    .\Validate-Show.ps1 'E:\Media\TV'
    Validates every show under the TV root.

.EXAMPLE
    .\Validate-Show.ps1 'E:\Media\TV' -Only 'Breaking' -Quiet
    Validates only shows whose name contains "Breaking", suppresses OK noise.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Path,

    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\LVP-WPF\appsettings.local.config'),

    [string]$Only,

    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- helpers ----

function Write-Banner([string]$s) { Write-Host $s -ForegroundColor Cyan }
function Write-Info  ([string]$s) { Write-Host $s -ForegroundColor Gray }
function Write-Ok    ([string]$s) { if (-not $Quiet) { Write-Host ('[OK]   ' + $s) -ForegroundColor Green } }
function Write-Warn  ([string]$s) { Write-Host ('[WARN] ' + $s) -ForegroundColor Yellow }
function Write-Fail  ([string]$s) { Write-Host ('[FAIL] ' + $s) -ForegroundColor Red }
function Write-Indent([string]$s) { Write-Host ('       ' + $s) -ForegroundColor DarkGray }

function Get-TmdbApiKey {
    return "c69c4effc7beb9c473d22b8f85d59e4c"
    <#if (-not (Test-Path $ConfigPath)) {
        throw "Config not found: $ConfigPath`nPass -ConfigPath or place appsettings.local.config under LVP-WPF\."
    }
    [xml]$cfg = Get-Content -LiteralPath $ConfigPath
    $node = $cfg.appSettings.add | Where-Object { $_.key -eq 'TmdbApiKey' }
    if (-not $node -or -not $node.value -or $node.value -eq 'your-tmdb-v3-api-key-here') {
        throw "TmdbApiKey not set in $ConfigPath"
    }
    return $node.value#>
}

# Session-scoped cache so re-running on the same library is cheap and we don't
# hammer TMDB. Keyed by full URL.
$script:TmdbCache = @{}
function Invoke-Tmdb([string]$url) {
    if ($script:TmdbCache.ContainsKey($url)) { return $script:TmdbCache[$url] }
    try {
        $r = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
    } catch {
        # 404 on tv/{id}/season/{n} is a real "no such season" signal - bubble null.
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) {
            $script:TmdbCache[$url] = $null
            return $null
        }
        throw
    }
    $script:TmdbCache[$url] = $r
    return $r
}

function Search-Tv ([string]$name, [string]$key) {
    Invoke-Tmdb ("https://api.themoviedb.org/3/search/tv?api_key={0}&query={1}" -f $key, [uri]::EscapeDataString($name))
}
function Get-Tv     ([int]$id, [string]$key) { Invoke-Tmdb ("https://api.themoviedb.org/3/tv/{0}?api_key={1}" -f $id, $key) }
function Get-Season ([int]$id, [int]$n, [string]$key) { Invoke-Tmdb ("https://api.themoviedb.org/3/tv/{0}/season/{1}?api_key={2}" -f $id, $n, $key) }

# Normalize disk title vs TMDB title for comparison. TMDB ships colons,
# question marks, slashes, etc. that you can't put in a filename, so we
# need the disk side and the TMDB side to converge under the same set of
# replacements before comparing.
#
# Illegal chars get replaced with SPACE (not stripped). This matters when
# the illegal char was acting as a word separator on the TMDB side - e.g.
# TMDB "Norman Lear/Boz Scaggs" must normalize to the same thing the disk
# file does after LVP_Episode_Renamer.Sanitize-FileName turned it into
# "Norman Lear Boz Scaggs". Stripping would produce "Norman LearBoz Scaggs"
# on the TMDB side, which wouldn't match.
function Normalize-Title([string]$s) {
    if (-not $s) { return '' }
    $t = $s.ToLowerInvariant()
    $t = $t -replace '[\\/:*?"<>|]', ' '    # Windows-illegal chars -> space (mirrors the renamer)
    $t = $t -replace '[''`.,!&\-]', ' '     # punctuation           -> space
    $t = $t -replace '\s+', ' '             # collapse whitespace
    return $t.Trim()
}

# Parse "N%Title.ext", "N#N2%Title.ext", "N%Title1#Title2.ext", and so on.
# Mirrors LibraryScanner's ExtractEpisodeIndex + MediaEnricher's
# EnrichMultiEpisode logic so problems here == problems the app will hit at
# runtime.
#
# Multi-episode rules (matching the C# side):
#   - The PREFIX may contain '#' (e.g. "01#02"). Only the part BEFORE the
#     first '#' is read - the rest is informational. Scanner does the same.
#   - The TITLE's '#' count is AUTHORITATIVE for how many episodes the file
#     spans. MediaEnricher's EnrichMultiEpisode does episode.Name.Split('#'),
#     and uses startEpNum + l for each consecutive episode. So a file named
#     "01 % Pilot Part 1#Pilot Part 2.mkv" covers E1 AND E2.
#   - Returned Numbers[] and Titles[] are parallel arrays; Numbers[i] is the
#     episode-number for Titles[i].
function Parse-EpisodeFile([string]$fileName) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($fileName)
    $pct = $stem.IndexOf('%')
    if ($pct -lt 0) {
        return [pscustomobject]@{ Valid = $false; Reason = "missing '%' separator"; File = $fileName }
    }
    $prefix = $stem.Substring(0, $pct)
    $title  = $stem.Substring($pct + 1).Trim()
    $hash = $prefix.IndexOf('#')
    if ($hash -ge 0) { $prefix = $prefix.Substring(0, $hash) }
    $start = 0
    if (-not [int]::TryParse($prefix, [ref]$start)) {
        return [pscustomobject]@{ Valid = $false; Reason = "episode prefix '$prefix' is not numeric"; File = $fileName }
    }

    # Split title on '#' to get individual episode titles. For a single-
    # episode file (no '#' in title), this yields a one-element array - same
    # downstream code handles single and multi uniformly.
    $titleParts = $title.Split('#') | ForEach-Object { $_.Trim() }
    $numbers = @()
    for ($i = 0; $i -lt $titleParts.Count; $i++) {
        $numbers += ($start + $i)
    }

    return [pscustomobject]@{
        Valid   = $true
        Numbers = $numbers
        Titles  = @($titleParts)
        File    = $fileName
    }
}

# --------------------------------------------------------------- per show ----

function Test-Show {
    param(
        [string]$ShowDir,
        [string]$ApiKey,
        [hashtable]$Totals
    )

    Write-Host ''
    Write-Banner ('=' * 72)
    Write-Banner ("  {0}" -f [IO.Path]::GetFileName($ShowDir))
    Write-Banner ('=' * 72)
    Write-Info   ("Path: {0}" -f $ShowDir)

    # ----- resolve TMDB id ------------------------------------------------
    $folderName  = [IO.Path]::GetFileName($ShowDir)
    $split       = $folderName -split '%', 2
    $displayName = $split[0].Trim()
    $tmdbId      = $null

    if ($split.Length -eq 2 -and ($split[1] -match '^\d+$')) {
        $tmdbId = [int]$split[1]
        Write-Info ("TMDB: id {0} (pinned via folder %suffix)" -f $tmdbId)
    } else {
        $search = Search-Tv $displayName $ApiKey
        if (-not $search.results -or $search.results.Count -eq 0) {
            Write-Fail "Show '$displayName' not found on TMDB"
            $Totals.Fail++
            return
        }
        if ($search.results.Count -gt 1) {
            Write-Warn ("Multiple TMDB matches ({0}) for '{1}' - using top hit '{2}'. Pin by renaming folder to '{1}%<id>'." -f $search.results.Count, $displayName, $search.results[0].name)
            $Totals.Warn++
        }
        $tmdbId = [int]$search.results[0].id
        Write-Info ("TMDB: id {0} (top search result)" -f $tmdbId)
    }

    $show = Get-Tv $tmdbId $ApiKey
    if (-not $show) {
        Write-Fail "TMDB returned no record for id $tmdbId"
        $Totals.Fail++
        return
    }
    $year = if ($show.first_air_date) { ($show.first_air_date -split '-')[0] } else { '?' }
    Write-Info ('TMDB: "{0}" ({1})  https://www.themoviedb.org/tv/{2}' -f $show.name, $year, $tmdbId)

    # Display-name sanity check (after normalization).
    if ((Normalize-Title $displayName) -ne (Normalize-Title $show.name)) {
        Write-Warn ("Folder name doesn't match TMDB show name")
        Write-Indent ("Folder: `"{0}`"" -f $displayName)
        Write-Indent ("TMDB:   `"{0}`"" -f $show.name)
        $Totals.Warn++
    }

    # ----- season folder enumeration --------------------------------------
    $childDirs = @(Get-ChildItem -LiteralPath $ShowDir -Directory)
    $seasonDirs = @($childDirs | Where-Object { $_.Name -like 'Season *' } |
                    Sort-Object { [int]($_.Name -split ' ')[-1] })
    $unknownDirs = @($childDirs | Where-Object { $_.Name -notlike 'Season *' -and $_.Name -ne 'Extras' -and $_.Name.Length -ne 2 })
    foreach ($u in $unknownDirs) {
        Write-Warn ("Unknown top-level folder: '{0}' (expected 'Season N', 'Extras', or a 2-letter lang code)" -f $u.Name)
        $Totals.Warn++
    }

    if ($seasonDirs.Count -eq 0) {
        # Multi-lang layout? Recurse into the first 2-letter folder if present.
        $lang = $childDirs | Where-Object { $_.Name.Length -eq 2 } | Select-Object -First 1
        if ($lang) {
            Write-Info ("Multi-lang layout detected, validating '{0}' subtree" -f $lang.Name)
            $seasonDirs = @(Get-ChildItem -LiteralPath $lang.FullName -Directory |
                            Where-Object { $_.Name -like 'Season *' } |
                            Sort-Object { [int]($_.Name -split ' ')[-1] })
        }
    }

    if ($seasonDirs.Count -eq 0) {
        Write-Fail 'No "Season N" folders found'
        $Totals.Fail++
        return
    }

    # ----- season count + gap check ---------------------------------------
    $diskSeasonNums = @($seasonDirs | ForEach-Object { [int]($_.Name -split ' ')[-1] })
    $tmdbSeasonNums = @($show.seasons | Where-Object { $_.season_number -gt 0 } | ForEach-Object { $_.season_number })

    if ($diskSeasonNums.Count -eq $tmdbSeasonNums.Count) {
        Write-Ok ("Season count: {0} (matches TMDB)" -f $diskSeasonNums.Count)
        $Totals.Ok++
    } else {
        $missing = @($tmdbSeasonNums | Where-Object { $_ -notin $diskSeasonNums })
        $extra   = @($diskSeasonNums | Where-Object { $_ -notin $tmdbSeasonNums })

        # If disk is a strict subset of TMDB (only $missing, no $extra), this
        # is an incomplete library - same shape as the renamer's "missing
        # episode" case. WARN rather than FAIL: nothing is wrong with the
        # present seasons. Only flip back to FAIL when the disk has seasons
        # TMDB doesn't, since that points at wrong-show / wrong-id / bad
        # data, not just an unfinished collection.
        if ($extra.Count -eq 0 -and $missing.Count -gt 0) {
            Write-Warn ("Season count: {0} on disk, TMDB has {1} (incomplete - {2} season(s) not collected)" -f $diskSeasonNums.Count, $tmdbSeasonNums.Count, $missing.Count)
            Write-Indent ("Missing on disk: Season {0}" -f ($missing -join ', Season '))
            $Totals.Warn++
        } else {
            Write-Fail ("Season count: {0} on disk, TMDB has {1}" -f $diskSeasonNums.Count, $tmdbSeasonNums.Count)
            if ($missing) { Write-Indent ("Missing on disk: Season {0}" -f ($missing -join ', Season ')) }
            if ($extra)   { Write-Indent ("Extra on disk:   Season {0} (wrong show id?)" -f ($extra -join ', Season ')) }
            $Totals.Fail++
        }
    }

    # Sequential gap check (disk has 1,2,4 -> flag 3 even if TMDB also missing 3).
    for ($i = 0; $i -lt $diskSeasonNums.Count - 1; $i++) {
        if ($diskSeasonNums[$i + 1] - $diskSeasonNums[$i] -ne 1) {
            Write-Warn ("Season number gap: jumps from {0} to {1}" -f $diskSeasonNums[$i], $diskSeasonNums[$i + 1])
            $Totals.Warn++
        }
    }

    # ----- per-season validation ------------------------------------------
    foreach ($sd in $seasonDirs) {
        $sn = [int]($sd.Name -split ' ')[-1]
        $tmdbSeason = Get-Season $tmdbId $sn $ApiKey

        if (-not $tmdbSeason) {
            Write-Fail ("Season {0}: TMDB has no season {0}" -f $sn)
            $Totals.Fail++
            continue
        }

        # Episode files: drop .srt (matches scanner) and other sidecar files.
        $epFiles = @(Get-ChildItem -LiteralPath $sd.FullName -File |
                     Where-Object { $_.Extension -notin '.srt', '.nfo', '.jpg', '.jpeg', '.png', '.txt' })

        $parsed = @($epFiles | ForEach-Object { Parse-EpisodeFile $_.Name })

        $bad = @($parsed | Where-Object { -not $_.Valid })
        foreach ($b in $bad) {
            Write-Fail ("Season {0}: '{1}' - {2}" -f $sn, $b.File, $b.Reason)
            $Totals.Fail++
        }

        $good = @($parsed | Where-Object { $_.Valid })

        # Flatten Numbers across all good parses. A multi-episode file like
        # "01#02 % Pilot Part 1#Pilot Part 2.mkv" contributes BOTH 1 and 2
        # here - they're treated as two distinct claims for the purpose of
        # count / dupe / set comparison. The (Number, File) pair pseudo-list
        # below is the same data shape but keeps the source file alongside
        # each number, used when we need to point at "which file claims this
        # episode" in failure messages.
        $diskNums = @()
        $diskClaims = @()  # array of @{Number=N; File=...; Title=...}
        foreach ($g in $good) {
            for ($i = 0; $i -lt $g.Numbers.Count; $i++) {
                $diskNums += $g.Numbers[$i]
                $diskClaims += [pscustomobject]@{
                    Number = $g.Numbers[$i]
                    Title  = $g.Titles[$i]
                    File   = $g.File
                }
            }
        }

        # Duplicates (any number claimed by more than one file - including a
        # number that's covered by one file's multi-episode range AND
        # another file's single-episode entry).
        $dupes = $diskClaims | Group-Object Number | Where-Object Count -gt 1
        foreach ($d in $dupes) {
            $files = ($d.Group | ForEach-Object File | Sort-Object -Unique) -join ', '
            Write-Fail ("Season {0}: episode #{1} claimed by {2} file(s) - {3}" -f $sn, $d.Name, $d.Count, $files)
            $Totals.Fail++
        }

        # Episode count - mirrors the season-count logic above. Note we
        # compare the COVERAGE COUNT (sum of all episode numbers claimed
        # across all files) against TMDB's episode count, not the raw file
        # count. A library with one double-episode file covering E01-E02
        # has good.Count==1 but $diskNums.Count==2 - the latter is what
        # should match TMDB.
        $tmdbEpCount = $tmdbSeason.episodes.Count
        $countOk = $diskNums.Count -eq $tmdbEpCount
        if (-not $countOk) {
            $tmdbNums = @($tmdbSeason.episodes | ForEach-Object episode_number)
            $missing  = @($tmdbNums | Where-Object { $_ -notin $diskNums })
            $extra    = @($diskNums | Where-Object { $_ -notin $tmdbNums })

            if ($extra.Count -eq 0 -and $missing.Count -gt 0) {
                Write-Warn ("Season {0}: {1} episodes covered on disk, TMDB has {2} (incomplete - {3} episode(s) not collected)" -f $sn, $diskNums.Count, $tmdbEpCount, $missing.Count)
                foreach ($m in $missing) {
                    $name = ($tmdbSeason.episodes | Where-Object episode_number -eq $m | Select-Object -First 1).name
                    Write-Indent ("Missing: E{0:D2} `"{1}`"" -f $m, $name)
                }
                $Totals.Warn++
            } else {
                Write-Fail ("Season {0}: {1} episodes covered on disk, TMDB has {2}" -f $sn, $diskNums.Count, $tmdbEpCount)
                foreach ($m in $missing) {
                    $name = ($tmdbSeason.episodes | Where-Object episode_number -eq $m | Select-Object -First 1).name
                    Write-Indent ("Missing: E{0:D2} `"{1}`"" -f $m, $name)
                }
                foreach ($e in $extra) {
                    $f = ($diskClaims | Where-Object Number -eq $e | Select-Object -First 1).File
                    Write-Indent ("Extra:   E{0:D2}  ({1}) - wrong season file? wrong show id?" -f $e, $f)
                }
                $Totals.Fail++
            }
        }

        # Gap detection within season. SKIPPED when the count check above
        # already enumerated the missing episodes - the gap line is just a
        # re-statement of the same fact in that case. Still runs when
        # counts match: catches the case where a dupe + a missing balance
        # out to the right total but the sequence is broken (disk
        # [1,2,3,5,5] vs TMDB [1..5]).
        if ($countOk) {
            $sortedNums = @($diskNums | Sort-Object -Unique)
            for ($i = 0; $i -lt $sortedNums.Count - 1; $i++) {
                if ($sortedNums[$i + 1] - $sortedNums[$i] -ne 1) {
                    Write-Warn ("Season {0}: episode # gap between E{1:D2} and E{2:D2}" -f $sn, $sortedNums[$i], $sortedNums[$i + 1])
                    $Totals.Warn++
                }
            }
        }

        # Title comparison on episodes both sides have. For multi-episode
        # files each Numbers[i]<->Titles[i] pair is compared independently
        # against the corresponding TMDB episode, matching MediaEnricher's
        # EnrichMultiEpisode per-part rename-mismatch logic.
        $tmdbByNum = @{}
        foreach ($e in $tmdbSeason.episodes) { $tmdbByNum[[int]$e.episode_number] = $e }

        $nameOkCount  = 0
        $nameCmpCount = 0
        foreach ($c in $diskClaims) {
            if (-not $tmdbByNum.ContainsKey($c.Number)) { continue }
            $nameCmpCount++
            $tmdbEp = $tmdbByNum[$c.Number]
            if ((Normalize-Title $c.Title) -eq (Normalize-Title $tmdbEp.name)) {
                $nameOkCount++
            } else {
                Write-Warn ("Season {0} / E{1:D2}: title mismatch" -f $sn, $c.Number)
                Write-Indent ("Disk: `"{0}`"  (file: {1})" -f $c.Title, $c.File)
                Write-Indent ("TMDB: `"{0}`"" -f $tmdbEp.name)
                $Totals.Warn++
            }
        }

        if ($countOk -and ($nameOkCount -eq $nameCmpCount) -and -not $bad -and -not $dupes) {
            Write-Ok ("Season {0}: {1}/{2} episodes, all titles match" -f $sn, $diskNums.Count, $tmdbEpCount)
            $Totals.Ok++
        }
    }
}

# --------------------------------------------------------------------- main ----

$apiKey = Get-TmdbApiKey

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Path not found: $Path"
}
$target = (Resolve-Path -LiteralPath $Path).Path
$totals = @{ Ok = 0; Warn = 0; Fail = 0 }

# A path is "a show" if it contains any 'Season N' subfolder; otherwise treat
# as a parent dir of many shows.
$looksLikeShow = @(Get-ChildItem -LiteralPath $target -Directory -ErrorAction SilentlyContinue |
                   Where-Object { $_.Name -like 'Season *' }).Count -gt 0

$shows = if ($looksLikeShow) {
    @(Get-Item -LiteralPath $target)
} else {
    $list = @(Get-ChildItem -LiteralPath $target -Directory)
    if ($Only) { $list = $list | Where-Object { $_.Name -like "*$Only*" } }
    $list
}

if ($shows.Count -eq 0) {
    Write-Warn "No shows to validate under $target (filter '$Only')"
    return
}

$swTotal = [Diagnostics.Stopwatch]::StartNew()
foreach ($s in $shows) {
    Test-Show -ShowDir $s.FullName -ApiKey $apiKey -Totals $totals
}
$swTotal.Stop()

Write-Host ''
Write-Banner ('=' * 72)
$line = "Summary: {0} OK, {1} FAIL, {2} WARN  -  {3} shows in {4:N1}s" -f `
    $totals.Ok, $totals.Fail, $totals.Warn, $shows.Count, ($swTotal.Elapsed.TotalSeconds)
if     ($totals.Fail -gt 0) { Write-Host $line -ForegroundColor Red }
elseif ($totals.Warn -gt 0) { Write-Host $line -ForegroundColor Yellow }
else                        { Write-Host $line -ForegroundColor Green }
Write-Banner ('=' * 72)

# Exit code lets you chain into a build script: 0 = clean, 1 = warnings only,
# 2 = failures present.
if     ($totals.Fail -gt 0) { exit 2 }
elseif ($totals.Warn -gt 0) { exit 1 }
else                        { exit 0 }
