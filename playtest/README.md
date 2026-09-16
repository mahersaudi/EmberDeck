# Playtest packages

The notes that ship inside the playtest builds, kept here so they are versioned with the game they
describe. `Build/` is not in the repository, so the copies the testers get are the only other place
they exist.

| file | goes to |
|---|---|
| `README-windows.txt` | beside `EmberDeck.exe` in the Windows zip |
| `README-mac.txt` | beside `EmberDeck.app` in the Mac zip, as `اقرأني - README.txt` |
| `README-android.txt` | with the APK, as `README-Android.txt` |

Each one covers, in Arabic and English: how to get past Gatekeeper, SmartScreen or Android's
unknown-sources block; what the controls are; what to play; the questions to answer; and how to send
`playtest-log.txt` back.

Rebuild and repackage together — a package whose player is newer than its notes has caused a tester
to look for a button that was not there yet.
