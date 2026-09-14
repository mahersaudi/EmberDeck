# Shipping to Steam

Everything in this folder is ready except the parts that need a Steamworks account, which only the
owner of the game can create. Placeholders are written `__LIKE_THIS__`.

## 1. What only you can do

1. **Join Steamworks** at partner.steamgames.com and pay the Steam Direct fee (US$100 per game).
   It is recouped once the game earns US$1,000. Steam requires tax and banking details before a
   game can be sold.
2. **Create the app.** Steam gives it an **App ID**.
3. **Create two depots** in the app's SteamPipe settings, one for Windows and one for macOS, and
   note both **depot IDs**.
4. **Set the launch options** in Steamworks (Installation → General):
   - Windows: executable `EmberDeck.exe`
   - macOS: executable `EmberDeck.app`

Never put a Steam password or login token in this repository.

## 2. Build

From the project root:

```bash
~/.unity/bin/unity run . --editor-version 6000.5.1f1 -- -executeMethod EmberDeck.EditorTools.BuildScript.BuildAllRelease
```

This writes `Build/Release/Windows/` and `Build/Release/macOS/`, the two folders the depot scripts
upload. Release builds have no development watermark and none of the capture harness's debug hooks.
The version is `BuildScript.Version`.

## 3. Upload

1. Download the **Steamworks SDK** from the Steamworks site; the uploader is
   `tools/ContentBuilder/builder_osx/steamcmd.sh` inside it.
2. Replace `__APP_ID__`, `__WINDOWS_DEPOT_ID__`, `__MACOS_DEPOT_ID__` and `__VERSION__` in the three
   `.vdf` files.
3. Run, from this folder:

```bash
path/to/steamcmd.sh +login YOUR_STEAM_USERNAME +run_app_build "$PWD/app_build.vdf" +quit
```

`steamcmd` asks for the password and Steam Guard code itself. The build then appears under
SteamPipe → Builds, where it is set live on a branch; `SetLive` is left empty on purpose so an
upload never goes to players by accident.

## 4. The store page

Steam requires the page to be public as "Coming Soon" for at least two weeks, and a first release
cannot happen until 30 days after the fee is paid. Start the page early.

| asset | size (px) |
|---|---|
| Header capsule | 920 × 430 |
| Small capsule | 462 × 174 |
| Main capsule | 1232 × 706 |
| Vertical capsule | 748 × 896 |
| Screenshots | at least 5, 1920 × 1080 |
| Library capsule | 600 × 900 |
| Library hero | 3840 × 1240 |
| Library logo | 1280 × 720, transparent |

Sizes change occasionally; check Steamworks' current "Graphical Assets" page before producing final
art. Capsules must show the game's name and art only — no review quotes or discount text.
