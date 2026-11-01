# Run the Android app

The Android app lives in `src/mobile/`. Authentication (register, login, session
refresh, logout) calls the dev backend at `https://veiller-dev.adrien-roho.fr/api/`
(`API_BASE_URL` in `app/build.gradle.kts`). The elder home reads real heart rate,
steps and sleep from Health Connect (see below). The other screens (circles,
patients, consents, the elder's caregiver list) still read simulated data from
`data/fake/`.

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

## Health data (Health Connect)

Veiller does not talk to the watch. The watch syncs with its vendor app, which
writes to Health Connect, and Veiller reads Health Connect on the elder's phone
(`data/health/`). Nothing is uploaded to the backend yet.

Tested with a Redmi Watch 5 Lite and Mi Fitness:

1. On the watch, open the heart rate app and turn on continuous monitoring.
2. In Mi Fitness, enable automatic heart rate measurement (shortest interval) and
   sleep monitoring.
3. In Mi Fitness, go to Profile > Third-party data > Health Connect and allow it to
   write heart rate, steps, active calories and sleep.
4. Remove battery optimization for Mi Fitness (and Veiller) and allow Mi Fitness to
   auto-start. Otherwise the phone kills the sync.
5. Check without any app: Settings > Health Connect > Data and access should list
   steps and active calories from Mi Fitness. If it is empty, Veiller will see nothing.

**Mi Fitness only writes workout heart rate to Health Connect.** On the test phone
(2026-11-01), Mi Fitness showed continuous heart rate every minute, but Health
Connect only received the heart rate recorded during workouts. Steps and active
calories (30 min blocks) arrive almost live. So with this watch the heart rate card
usually shows the last workout ("Última medición registrada"). Whether Mi Fitness
writes a nightly batch is still being checked.

**To do: test with a Garmin watch** (Garmin Connect is expected to write
continuous heart rate to Health Connect).

Health Connect is part of the system on Android 14+. On Android 13 or lower it is
a separate app from the Play Store; Veiller offers the Play Store link when it is
missing.

The app asks for the Health Connect read permissions when an elder accepts the
consent screen (onboarding or "Mis permisos"). An elder who logs in on a new phone
sees a "Conceder acceso a tus datos de salud" card on the home screen instead.

Data is read when the home screen opens and with "Actualizar"; there is no
background sync. Mi Fitness only writes when the watch syncs, so values can be
minutes or hours old: each metric shows its real measurement time.

"Sin datos recientes del reloj" depends only on the watch liveness signal: the latest
end time among heart rate, steps and active calories records written by the watch
app (`WATCH_LIVENESS_TYPES`, `WATCH_APP_PACKAGES` and `WATCH_LIVENESS_STALE_AFTER` in
`data/health/HealthConfig.kt`). The header and the cards use that same rule. An old
value on its own does not mean the watch is off.

To test without the watch, insert sample records with Google's Health Connect
Toolbox app.

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
