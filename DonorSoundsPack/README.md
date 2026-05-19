# DonorSoundsPack

Workshop companion mod for the Sounds mod. Ships per-donor goal songs and goal
horns as `.ogg` files that get hot-swapped in when a donor scores.

Loading mechanism is identical to `SoundsPack/` — at runtime the pack tells the
Sounds mod to read its `sounds/` folder. The Sounds mod loads every `.ogg`,
categorizes them by filename substring, and dispatches them per-goal based on
the scorer's saved donor pick.

## Adding a new clip

1. Trim the source audio to **15 seconds** (intermission clips are 30s, but
   those don't belong here). Trim points are **baked into the `.ogg`** — no
   runtime trimming.
2. Export as `.ogg`.
3. Name the file using the contract below.
4. Drop it in `DonorSoundsPack/sounds/`.
5. Build and publish a workshop update.
6. On the website, add a matching entry to `Public_html/data/song_catalog.json`.

## File-naming contract

Every donor pick is a single id (e.g. `rocky_theme`). For each id, ship **both**
team variants:

| Donor picks (`id` in song_catalog.json)    | Required files in `sounds/`                                 |
|--------------------------------------------|-------------------------------------------------------------|
| song id `<id>`                             | `<id>_bluegoalmusic.ogg` + `<id>_redgoalmusic.ogg`          |
| horn id `<id>`                             | `<id>_bluegoalhorn.ogg` + `<id>_redgoalhorn.ogg`            |

Why two files per id: the Sounds mod constructs a team-specific clip name at
goal time. If a donor scores for Blue, it sends `<id>_bluegoalmusic`; for Red,
`<id>_redgoalmusic`. Missing the opposite-team variant means the clip silently
falls back to the default rotation when that donor scores for that team.

The substring suffix is load-bearing: it's how the Sounds mod's
`AddClipNameToCorrectList` decides which list (`BlueGoalMusicList`,
`RedGoalMusicList`, etc.) the clip goes into. Horns aren't categorized into a
list — they're looked up by exact name from the full clip pool — but they still
need the suffix so admins can tell from the filename which team variant they're
looking at.

### Allowed id characters

Lowercase letters, digits, and underscores. No spaces, no dashes (dashes
collide with how filenames are sometimes parsed elsewhere). The id is what
donors see referenced in the URL on the rewards page if they share their pick.

## Audio production rules

- Bitrate: match the existing SoundsPack clips.
- Length: 15s, hard. The 2.25s music-after-horn delay is baked into the Sounds
  mod, so clips that bury the hook past ~3s will sound flat in-game.
- Volume: normalize against the existing curated music pack so donor clips
  don't blow out non-donor goals.
- Legal: pre-curated list only. No donor uploads, no YouTube-link submissions.

## End-to-end flow

1. Admin trims approved audio, names per the contract, drops in `sounds/`,
   publishes workshop update.
2. Admin adds `{id, name, artist?}` to `Public_html/data/song_catalog.json` on
   the website.
3. Donor opens `/donate/rewards/`, picks the new entry, saves.
4. Site writes `data/tag_preferences.json` and POSTs the pick to every game
   server's TagMod bridge via `pp_push_tags_to_servers()`.
5. TagMod stores it in `<gameRoot>/config/poncepuck_user_data.json`.
6. When the donor scores, the Sounds mod reads the pick from that JSON
   (mtime-cached, no parse on the hot path), constructs the team-specific clip
   name, and broadcasts it.
7. Clients subscribed to DonorSoundsPack swap in the matching clip. Clients
   without the pack silently get the default rotation.

Servers offline at step 4 catch up within ~5 minutes via `RemoteTagPrefsSync`
polling `/api/tag_prefs.php`.

## Tier gates (enforced on the site)

- **Songs**: any donor, any tier.
- **Horns**: 60+ donor days, admin, or owner.

## Building

`dotnet build DonorSoundsPack.csproj -c Release` produces `DonorSoundsPack.dll`.
The DLL plus the `sounds/` folder is what ships to the workshop.
