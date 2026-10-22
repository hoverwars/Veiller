# Run the Android app

The Android app lives in `src/mobile/`. It does not call the backend yet: every
screen reads simulated data from `data/fake/`.

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

## Demo accounts

Without a backend, login accepts any 8+ character password for:

| Email              | Role          |
|--------------------|---------------|
| laura@correo.com   | Caregiver     |
| rosa@correo.com    | Older adult   |

Registering a new account also works and is kept on the device.
The circle code from the mockups, `VLR-7K2Q`, finds Rosa's circle.

## Connecting the backend later

`di/AppContainer.kt` wires each repository interface (`data/repository/`) to a
`Fake*` implementation. Replace each one with a Retrofit-based implementation
once the endpoints exist; the screens do not need to change.
