# Run the Android app

The Android app lives in `src/mobile/`. Authentication (register, login, session
refresh, logout) calls the dev backend at `https://veiller-dev.adrien-roho.fr/api/`
(`API_BASE_URL` in `app/build.gradle.kts`). The other screens (circles, patients,
consents, elder home) still read simulated data from `data/fake/`.

## Prerequisites

- Android Studio with the Android SDK (API 37 platform).
- A physical Android phone (8.0+) with USB debugging enabled. We do not use the
  emulator for this project.

## Open and run

1. In Android Studio, open the `src/mobile/` folder (not the repo root).
2. Connect the phone by USB and accept the debugging prompt.
3. Run the `app` configuration.

From a terminal:

```bash
cd src/mobile
./gradlew assembleDebug        # build the APK
./gradlew testDebugUnitTest    # unit tests, no device needed
./gradlew installDebug         # install on the connected phone
```

## Accounts

The demo accounts no longer exist: create one from the app (Register). It is created
in the Firebase project behind the dev API, so it needs an internet connection and a
password of 8+ characters.

The circle code from the mockups, `VLR-7K2Q`, still finds Rosa's circle (simulated data).
Circle screens do not recognize the real signed-in user as admin or member yet,
because they still use simulated members.

## CI

`.github/workflows/android.yml` runs the unit tests and builds the debug APK.
It only runs when something under `src/mobile/` (or the workflow itself)
changes, so backend-only changes do not trigger it:

- On every pull request.
- On every push to `dev` or `main`. The APK is uploaded as the
  `veiller-debug-<sha>` artifact of the run (kept 30 days): download it from
  the run page in the Actions tab and install it on your phone.
- Manually from the Actions tab (`workflow_dispatch`), once the workflow is on
  the default branch.

## Connecting the backend later

`di/AppContainer.kt` wires each repository interface (`data/repository/`) to a
`Fake*` implementation. Replace each one with a Retrofit-based implementation
once the endpoints exist; the screens do not need to change.
