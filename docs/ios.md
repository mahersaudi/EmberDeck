# iPhone and iPad

The same game as everywhere else, with the touch interface Android already uses
(`docs/touch-and-android.md`) and one change of its own: the canvas now fits a 4:3 screen.

## What Unity can build, and what it cannot

Unity does not build an iOS app. It writes an **Xcode project**, and Xcode compiles, signs and
installs it. That split is Apple's, not Unity's, and it is why this platform needs something from
you that Windows and Android did not:

```bash
./tools/build.sh BuildIOS      # writes Build/Release/iOS/Xcode
```

Then, on this Mac:

1. **Install Xcode** (about 12 GB). The command-line tools already here are not enough — they have no
   iOS SDK and no way to sign anything.

   Xcode 27 needs macOS 26.6 or newer, and this machine was on 26.5.1, so the OS update comes first.
   From the terminal, with `mas` (`brew install mas`) and the App Store already signed in:

   ```bash
   softwareupdate --list                      # what is available
   softwareupdate --download 'macOS 27-26A428'
   softwareupdate --install 'macOS 27-26A428' --restart    # reboots
   mas get 497799835                          # Xcode, once the OS is new enough
   sudo xcode-select -s /Applications/Xcode.app
   sudo xcodebuild -license accept
   ```
2. Open `Build/Release/iOS/Xcode/Unity-iPhone.xcodeproj`.
3. In **Signing & Capabilities**, choose a team. A free Apple ID works: it signs the app for seven
   days at a time, which is enough to play it on your own iPhone or iPad. An Apple Developer
   membership (US$99 a year) is only needed for TestFlight, for friends' devices, and for the App
   Store.
4. Plug the device in, pick it at the top of the window, and press Run.

`ProjectSetup.ApplyIOS` sets the rest: `com.mahersaudi.emberdeck`, landscape either way up, iOS 15 and up (this editor's floor),
ARM64 with IL2CPP, iPhone and iPad from the same build, the status bar hidden, automatic signing on
and **no team id** — a team id in a repository is someone's account, and Xcode fills it in from
whoever opens the project.

## The iPad's shape

A phone in landscape is far wider than the 1920×1080 the interface is designed at, so the canvas
matched the design's height and the extra width became margin. An iPad is the other way round: 4:3
is *narrower* than 16:9, and matched to height the design's 1920 units of width do not fit in the
1440 the screen has — the End Turn button and the player's panel are cut off the sides.

`ScalerFit` picks the axis from the device: wider than 16:9 matches height, narrower matches width,
and the whole design always fits. It re-checks when the screen changes, because a tablet rotates and
an iPad can put the game in a split view. Captured at 1024×768 to see it: nothing is cut, and the
board has more vertical room than a phone gives it.

## What is not done

- **No build has been run through Xcode**, because Xcode is not installed on this machine. Everything
  up to the Xcode project is verified; everything after it is the four steps above.
- **No iOS device has ever run the game.** Sizes, safe areas and the two-tap rules were reasoned
  from the Android work and photographed at iPad and iPhone proportions on a Mac.
- **Nothing iOS-specific is handled**: no Game Center, no iCloud save, no App Store metadata, and no
  privacy manifest — Apple requires one (`PrivacyInfo.xcprivacy`) for App Store submission, and the
  answers are all "none": the game collects nothing and contacts nothing.
