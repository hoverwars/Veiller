# Health sync API: backend notes

Backend review and implementation notes for [`health_sync_api.md`](https://github.com/hoverwars/Veiller/blob/feat/health-connect/docs/dev/health_sync_api.md)
(branch `feat/health-connect`, mobile owner: Fabio). That file is the contract; this one lists
what the backend implemented where the contract was silent or contradicted itself, and the
points that still need a decision. Fabio, please fold what you agree with into the contract.

Status: **implemented** in `Veiller.Api/Health/` (`POST /api/health/sync`, `GET /api/health/me/status`),
with integration tests in `Veiller.Api.Tests/Health/`.

## Where the contract needed a decision

These are implemented as described. Each one is cheap to change if you disagree.

1. **Future timestamps: only some fields are compared with the clock.** The contract says any
   `time`, `start`, `end` later than server time + `FutureTolerance` is `TIME_IN_FUTURE`. Applied literally it
   rejects traffic the contract itself describes as normal:
   - The steps bucket of the current hour ends in the future by construction (`end = start + 1 h`), and the client
     re-sends it on every sync "up to now". The status example also shows `lastSample.steps` (`23:00`) after
     `lastUploadAt` (`22:30`).
   - The contract says some watch apps write heart rate blocks whose end is still ahead.

   Implemented: the check applies to heart rate sample `time`, heart rate record `start`, steps bucket `start`,
   sleep session `start`/`end` and `watchLastSeenAt`. It does not apply to a heart rate record `end` or a steps
   bucket `end`.
2. **The role comes from the stored profile, not from the JWT.** The contract says "claim `role == "elder"`".
   Every endpoint of the API (consents, circles, profile) reads `users.role`, and a token issued right after
   onboarding may carry stale claims. Same result for a normal account, one rule everywhere.
3. **Two reason codes added:** `INVALID_FORMAT` (a `zoneOffset` that is not `±HH:MM` within ±18:00) and
   `COUNT_OUT_OF_RANGE` (steps `count` below 0 or above 1 000 000, the Health Connect maximum). The contract
   had no code for them.
4. **`bpm` and `count` are read as 64-bit integers** so that `5000000000` is a `BPM_OUT_OF_RANGE` item and not a `400`
   for the whole request. A real wrong type (`"72"`, `72.5`) is still a `400`: numbers are not read from strings.
5. **`rejected.deleted`:** `index` points into the concatenation of the two lists (as written in the contract) and
   `block` is `heartRateRecords` or `sleepSessions`. `block` is absent in the other lists, `sampleIndex` is absent
   unless a single sample was dropped. This is redundant (the client could use an index per list); kept as written.
6. **Duplicates inside one request: the last occurrence wins.** Same `hcRecordId` twice, same steps `start` twice, same
   sleep `hcRecordId` twice, or two heart rate samples with the same `time` in one record. Without a rule the
   primary key would reject the whole request. `accepted` counts what is stored.
7. **A heart rate record without a `samples` array is rejected** (`MISSING_FIELD`): replace semantics would
   otherwise wipe the stored samples because of a client bug. An explicit empty array is valid and clears them.
8. **`zoneOffset` is optional** (Health Connect allows a session without an offset); `origin` is required for sleep like for
   heart rate. A missing or unknown `stage` is stored as `UNKNOWN`; stage names are case-insensitive.
9. **Instants must carry a `Z` or an offset.** They are stored in UTC. Anything that is not an ISO-8601 instant is a `400`.
   A body that is not `application/json` is a `415`.
10. **Item counting:** `MaxItemsPerRequest` counts every sample, bucket, session and deleted id sent, valid or not
    (the 413 is decided before validation). Stages do not count, so a session with thousands of stages is only bounded
    by `MaxBodyBytes`.
11. **`serverTime`** is the time the request was processed; it is the value stored as `lastUploadAt`.
12. **Order of checks:** `401`, `403`, then `415`/`413` (body size)/`400` (JSON, `schemaVersion`), then `413` (item count),
    then per-item validation. So a non-elder never learns anything about the body limits.

## Points that needed a decision

Points 1, 2, 5 and 6 are now resolved (see below); the others are unchanged.

1. **A heart rate record longer than `MaxItemsPerRequest` can never be uploaded.** *Resolved: option (c). A record with more than
   `HealthSync:MaxSamplesPerRecord` (5000) samples is rejected as an item with `TOO_MANY_SAMPLES` (200) and does not count toward
   the 413, so the token can move. The client should still downsample (a).* Records are never split (replace
   semantics) and each sample counts as an item. Mi Fitness writes workout heart rate; a workout sampled once per second
   is 5 000 samples after 83 minutes. That request is a permanent `413`, the client keeps the token, and heart rate
   stops syncing for good. Options: (a) the client downsamples long records to at most N samples, (b) the limit applies
   per request but a single record may exceed it, (c) the server rejects such a record as an item (`TOO_MANY_SAMPLES`,
   `200`) so the token can move. Recommendation: (c) plus (a). Not implemented, to keep the contract as agreed.
2. **No lower bound on timestamps.** *Resolved: `HealthSync:MaxAge` (35 days). A heart rate record start or sample time, a steps
   bucket start or a sleep session start older than that is rejected with `TIME_TOO_OLD`.* A record from 1970 was stored. The client reads at most 30 days back, so rejecting
   anything older than about 35 days would be safe, but the contract does not say it. Needs a reason code if wanted.
3. **Overlapping records from different apps.** The sample key is `(elder, record, time)`, so two apps writing the same
   minute are both stored. The caregiver history (feature 5) has to deduplicate by time (or prefer one `origin`) when
   it averages, otherwise the same minute counts twice.
4. **Consents.** Heart rate and sleep are *required* consents (cannot be refused), and steps is not a consent type, so
   ingestion does not filter anything today. If a type ever becomes optional, filtering belongs at ingestion *and* at
   read time (feature 5 must hide types that are not granted).
5. **No rate limiting on `/api/health/sync`.** *Resolved: fixed window per user, `RateLimiting:HealthSyncPerMinute` (30), answers `429`.* The contract lists `429` as a temporary error, but there is no limiter yet.
   The foreground-only trigger makes it low risk; add one with the background sync.
6. **Retention.** *Resolved: a daily background job deletes heart rate records, steps buckets and sleep sessions that ended more than
   `Retention:HealthDataDays` (365) days ago. Aggregation is not done.* Nothing was deleted over time and `GET /api/profile/export` returns the whole history, which grows
   without bound (about 1 440 samples per elder and day). Retention and aggregation belong to feature 9.
7. **Concurrent requests of the same elder** (two syncs overlapping): a unique-key conflict is retried once inside the
   server; if it still fails the client gets a `500` and retries, which is safe because the endpoint is idempotent.

## Corrections to the contract

- **Storage recommendation:** PostgreSQL is already a service in `infra/compose.yml` (named volume, no published port,
  password from `.env`) and the API already uses EF Core migrations on it. Nothing to add to compose. A backup
  routine is still missing.
- **Open point 1 (account deletion):** done by feature 9. `DELETE /api/profile` deletes the elder's health rows (foreign
  key cascade) and `GET /api/profile/export` now includes them (`health` section, `null` for a caregiver).
- **Open point 2 (who reads the data):** circles and memberships exist now (features 2 and 3); reading is feature 5.
- Timestamps in health tables are UTC `DateTime` (`timestamptz`), not `DateTimeOffset` like the rest of the schema, so the
  "latest sample" aggregates run in SQL on PostgreSQL and on the SQLite used by tests.

## Reason codes added after v1

`TOO_MANY_SAMPLES` (heart rate record over `MaxSamplesPerRecord`) and `TIME_TOO_OLD` (see above), on top of `INVALID_FORMAT` and
`COUNT_OUT_OF_RANGE`.

## Operations

- Limits are server configuration, section `HealthSync` of `appsettings.json` (`MaxItemsPerRequest` 5000,
  `MaxBodyBytes` 1048576, `FutureTolerance` `00:05:00`, `MaxSamplesPerRecord` 5000, `MaxAge` 35 days); override with `HealthSync__MaxItemsPerRequest=...`.
- Logs carry counts only. EF Core's SQL command log is set to `Warning`: a sync writes up to thousands of rows and the
  statements are noise (parameter values are already masked).
- Migration: `AddHealthSync` (tables `heart_rate_records`, `heart_rate_samples`, `steps_hourly`, `sleep_sessions`,
  `sleep_stages`, `health_sync_status`). Applied at startup like the others.
