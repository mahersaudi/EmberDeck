# Touch and Android

The game was built for a mouse on a 16:9 desktop window. A phone differs in four ways that the
interface has to answer, and nothing else about the game changes: same rules, same content, same
save format, same balance.

## What a touch screen changes

**No hover.** On a desktop, resting the pointer on a card, an enemy, a relic or a map node opens a
tooltip, and that is how a player reads the game. A finger has no resting state — it either touches
or it does not — so on touch the tooltip opens on the press (`TooltipTrigger.OnPointerDown`) and
closes itself five seconds later, since nothing takes a pointer away from it.

Two things then become dangerous, because their tooltip and their action would fire on the same tap:

- **A card in hand** plays on one tap on a desktop when it needs no target. On touch the first tap
  reads it and the second plays it (`CombatView.OnCardClicked`). Cards that need a target already
  worked this way: tap the card, tap the enemy.
- **A map node** enters a fight. On touch the first tap reads it and a second tap on the same node
  goes in (`MapView`). Entering an elite by mistake can end a run, and nothing can undo it.

Everything else — buying, upgrading, event choices — is a single tap, as it is a single click on a
desktop. Those screens print what they offer on the face of the card, and the choice is confirmed
by a separate button or costs only gold.

**Fingers are wider than a cursor.** A phone's 1080 rows of pixels are about 70mm of glass, so a
56-unit control is a 3.5mm target. The potion belt is drawn half again as large on touch, and each
reachable map node gets an invisible pad out to its neighbours so a thumb that lands near a node
still hits it — the map itself is not redrawn larger, because its rows barely fit as they are.

**No Escape key.** Android's Back button arrives as Escape, which already opens the pause menu, but
a player cannot see that. Touch adds a Menu button in the top corner.

**A phone is much wider than 16:9.** The canvas matches the design's height only on touch
(`UiFactory.ConfigureScaler`), so the 1080-tall layout always fits and the extra width of a long
screen becomes margin instead of squeezing the board. The stage is also inset to `Screen.safeArea`
(`SafeArea`), which keeps the interface clear of a notch, a rounded corner and the gesture bar.

**Shaking and flashing are optional.** Settings has a switch for screen shake and impact flashes
(`Settings.ReducedMotion`), and it is the phone that makes it necessary: a screen held a foot from
the face is where a shaking board and a ring of light at every hit stop being a thrill. Off, the
decoration stops and every piece of motion that carries information stays — the damage numbers, the
cards dealing in, the portraits, the fades.

**Not every touch screen is a phone.** An iPad is narrower than 16:9, where a phone is wider, so
matching the canvas to the design's height — right for a phone — cuts the sides off a tablet.
`ScalerFit` picks the axis from the device; see `docs/ios.md`.

`TouchMode.Active` is the one switch behind all of it: `Application.isMobilePlatform`, or forced on
by `-emberdeck-touch` so the phone layout can be captured and reviewed on a Mac.

## The Android build

    ./tools/build.sh BuildAndroid          # Build/Android/EmberDeck.apk, development
    ./tools/build.sh BuildAndroidRelease   # Build/Release/Android/EmberDeck.apk

`ProjectSetup.ApplyAndroid` runs first and sets: landscape only (the board is a wide row of enemies
above a wide hand, and no column arrangement of it works), `com.mahersaudi.emberdeck`, minimum API
26 — Android 8.0, this editor's own floor — ARM64 with IL2CPP, and an APK rather than an app bundle
so a tester can install the file directly. It is signed with Unity's debug
keystore: this is a playtest build, not a store upload.

**The art is compressed for Android only.** Everything under `Art/` imports uncompressed, which is
right for a desktop build and made a 985 MB APK. `ArtImportSettings` adds an Android override —
ASTC 6x6, 1024 max — and the release APK is 53 MB. The 128px UI icons keep their override-free
import; block compression shows most on small flat shapes, and they cost a third of a megabyte in
total.

Two things about the Android toolchain are worth knowing before the next build:

- The first release build failed with `Could not download intellij-core-32.0.0.jar ... Read timed
  out`. Gradle fetches that jar for `extractReleaseAnnotations` on the release path only; it is a
  network failure, not a project error, and the same command succeeded on the next run.
- A development APK is 985 MB and a release APK is 53 MB. The difference is IL2CPP debug symbols;
  build `BuildAndroid` only when the capture harness is actually needed.
- The APK asks for `android.permission.INTERNET` even though nothing in the game connects to
  anything, and `PlayerSettings.Android.forceInternetPermission = false` does not remove it: the
  permission is declared by Unity's own `unityLibrary` manifest and merged in. Removing it means
  supplying a custom main manifest that overrides Unity's template — a store-release job, not a
  playtest one.

Installing it takes one thing from the tester: Android blocks apps from outside the Play Store
until "install unknown apps" is allowed for whatever app they downloaded the file with.

## What is not done

- **iOS.** Building for iPhone needs Xcode on this machine and an Apple Developer account
  ($99/year) to run on anything but a simulator. Nothing in the game is Android-specific, so the
  touch work above is the whole port; the remaining step is a Mac with Xcode.
- **A phone has been no part of testing.** Every screenshot of the touch layout was taken on a Mac
  at phone proportions with `-emberdeck-touch`. Sizes, safe areas and the two-tap rules are
  reasoned, not felt, and the APK has never been launched: this machine has no Android device, no
  emulator and no system image. What is verified is the package itself — `aapt2 dump badging` reads
  `com.mahersaudi.emberdeck`, 0.1.0, minSdk 26, `arm64-v8a`, landscape.
  `adb` for a real device ships with the editor, at
  `PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`.
