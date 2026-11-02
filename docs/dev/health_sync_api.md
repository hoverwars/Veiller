# Health sync API (v1)

Status: **reviewed by architecture; to agree with the backend before implementation.**
Mobile owner: Fabio. Backend owner: Adrien.
Source: design notes `veiller-healthconnect-sync.md`, sections 7.3 (sync engine) and 8 (API).

The elder's phone reads Health Connect (HC) and uploads to the API. Caregiver phones
never upload; they will read from the API through circle endpoints (out of scope here).

## Conventions

- Paths are relative to `API_BASE_URL` (`https://veiller-dev.adrien-roho.fr/api/`).
- Every request carries `Authorization: Bearer <Firebase ID token>`, validated like the
  existing `/api/auth/me` endpoints.
- JSON, camelCase. Timestamps are ISO-8601 UTC instants (`2026-11-01T14:03:00Z`).
  Offsets are separate strings (`-05:00`).
- The server ignores unknown fields, so the client can add optional fields without a
  new `schemaVersion`.
- Errors use the same `ProblemDetails` body (RFC 7807) as the auth endpoints
  (`Results.Problem`).
- Never log health values, on the client or on the server. Logs carry counts only.

## Delivery guarantees

The client sends **at least once** and the server is **idempotent**, so every item is
stored effectively once.

- The client advances its HC change token only after a `2xx`. After a timeout, a `5xx`
  or a crash, it sends the same items again, possibly mixed with new ones.
- Repeated items are expected in normal operation: retries, the steps overlap window,
  and the re-read after an expired HC token (see [Client behavior](#client-behavior)).
- Every item is written by its natural key (upsert, or replace for heart rate records).
  Repeating a request must leave the stored data exactly as after the first time.

## `POST /api/health/sync`

Uploads new, changed and deleted health records of the signed-in elder.

### Authorization

- Valid Bearer token, otherwise `401`.
- Claim `role == "elder"`, otherwise `403`.
- **The owner is `elderId = user_id` from the JWT, never from the body.** The body has no
  owner field. This prevents one elder from writing another elder's data (IDOR).

### Request body

```json
{
  "schemaVersion": 1,
  "client": { "appVersion": "0.1.0", "deviceManufacturer": "samsung", "deviceModel": "SM-S931B" },
  "watchLastSeenAt": "2026-11-01T22:31:00Z",
  "heartRateRecords": [
    {
      "hcRecordId": "5b1d…",
      "origin": "com.example.watchapp",
      "start": "2026-11-01T14:00:00Z",
      "end": "2026-11-01T14:10:00Z",
      "samples": [
        { "time": "2026-11-01T14:03:00Z", "bpm": 72 }
      ]
    }
  ],
  "stepsHourly": [
    { "start": "2026-11-01T14:00:00Z", "end": "2026-11-01T15:00:00Z", "count": 312 }
  ],
  "sleepSessions": [
    {
      "hcRecordId": "9e0a…",
      "start": "2026-10-31T04:10:00Z",
      "end": "2026-10-31T11:02:00Z",
      "zoneOffset": "-05:00",
      "origin": "com.example.watchapp",
      "stages": [
        { "start": "2026-10-31T04:10:00Z", "end": "2026-10-31T04:40:00Z", "stage": "LIGHT" }
      ]
    }
  ],
  "deleted": { "heartRateRecords": ["5b1d…"], "sleepSessions": ["9e0a…"] }
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `schemaVersion` | int | yes | `1`. Any other value: `400`. |
| `client` | object | no | `appVersion`, `deviceManufacturer`, `deviceModel`. Informational only. |
| `watchLastSeenAt` | instant | no | Watch liveness signal computed on the phone (see below). `null` or missing: no signal, the stored value is kept. |
| `heartRateRecords` | array | no | One item per HC heart rate record, with all its samples. |
| `stepsHourly` | array | no | One item per hour bucket. |
| `sleepSessions` | array | no | One item per HC sleep session. |
| `deleted` | object | no | `heartRateRecords` and `sleepSessions`: lists of `hcRecordId`. |

Every field except `schemaVersion` may be missing or empty. A request with nothing in it
is valid and returns `200` with zero counts.

#### `watchLastSeenAt`

The phone computes it from HC: the latest effective end among the heart rate, steps and
active calories records written by the watch app, where the effective end of a record is
`min(endTime, metadata.lastModifiedTime, now)`. Some watch apps write blocks whose end
is still in the future, so the end alone would overstate liveness.

- The server stores the **maximum** of the stored value and the received one. A late
  retry of an older request cannot move it back, and repeating a request changes nothing.
- A value later than server time plus `FutureTolerance` is rejected (reported in
  `rejected.watchLastSeenAt`) and the stored value is kept.
- It is returned by [`GET /api/health/me/status`](#get-apihealthmestatus). Without it a
  caregiver cannot tell whether the elder's watch is still sending data, because a
  silent watch and a quiet elder look the same in the stored samples.

#### `heartRateRecords[]`

One HC `HeartRateRecord` holds many samples. The client always sends the **whole record
with all its samples**, as HC returns it.

| Field | Type | Notes |
|---|---|---|
| `hcRecordId` | string | HC `metadata.id` of the record. ≤ 128 chars. |
| `origin` | string | `dataOrigin.packageName` of the writing app. ≤ 255 chars. |
| `start`, `end` | instant | Record interval, `end >= start`. |
| `samples` | array | Each item has `time` (inside `[start, end]`) and `bpm`. |
| `samples[].bpm` | int | 1–300, the same range HC accepts. Judging plausibility (e.g. an alert at 40) is the rules engine's job, not ingestion's. |

**Key: `(elderId, hcRecordId)`. Semantics: replace.** In the request transaction the
server deletes every stored sample of `(elderId, hcRecordId)` and inserts the samples
received. If an app edits an HC record and drops or moves samples, the old samples do
not survive. Repeating the request yields the same samples.

#### `stepsHourly[]`

Steps are not sent as raw records. The client asks HC for hourly aggregates
(`aggregateGroupByDuration`, `StepsRecord.COUNT_TOTAL`), which HC already deduplicates
across apps. So a bucket has no `origin`.

| Field | Type | Notes |
|---|---|---|
| `start` | instant | Aligned to a whole UTC hour. |
| `end` | instant | Exactly `start + 1 h`. |
| `count` | int | ≥ 0. **`0` is valid** and overwrites an earlier value (it fixes deletions inside the overlap window). |

**Upsert key: `(elderId, start)`.** The last value received wins: the client re-sends
recent buckets with updated counts.

#### `sleepSessions[]`

| Field | Type | Notes |
|---|---|---|
| `hcRecordId` | string | HC `metadata.id`. ≤ 128 chars. |
| `start`, `end` | instant | `end > start`. |
| `zoneOffset` | string | Offset at `start` (`±HH:MM`), so the server can work out which local night the session belongs to. |
| `origin` | string | Package of the writing app. ≤ 255 chars. |
| `stages` | array | Optional. Each item has `start`, `end` (`end > start`, inside the session) and `stage`. |

`stage` is one of `AWAKE`, `SLEEPING`, `OUT_OF_BED`, `LIGHT`, `DEEP`, `REM`,
`AWAKE_IN_BED`, `UNKNOWN`. The client maps them from `SleepSessionRecord.STAGE_TYPE_*`.
The server stores any other value as `UNKNOWN`.

**Upsert key: `(elderId, hcRecordId)`.** An upsert replaces the whole session,
including all its stages.

#### `deleted`

- `heartRateRecords`: deletes the record and all its samples.
- `sleepSessions`: deletes the session and its stages.
- An id that does not exist is not an error, which keeps deletions idempotent.
- Within one request, deletions are applied **after** the upserts. HC ids are never
  reused, so "upserted and deleted in the same request" means "deleted".
- Steps have no deletions: the overlap window re-sends corrected buckets.

### Validation

Validation is per item. Requests are well formed but individual items can be invalid,
for example a sample with a future time because of a wrong phone clock. Those items are
**skipped and listed in `rejected`**, and the response is still `200`. If one bad item
failed the whole request, the client would retry it forever and that data type would
stop syncing.

The unit of rejection:

- A heart rate **sample** with a bad `bpm` or `time` is dropped on its own; the rest of
  the record is stored. A problem with the **record** itself (id, origin, interval)
  rejects the whole record, and its stored samples are left untouched.
- A steps bucket, a sleep session or a deleted id is rejected as a whole. A bad stage
  rejects its session.

Reason codes:

| Code | When |
|---|---|
| `BPM_OUT_OF_RANGE` | `bpm` outside 1–300. |
| `TIME_IN_FUTURE` | A `time`, `start`, `end` or `watchLastSeenAt` later than server time plus `FutureTolerance`. |
| `INVALID_INTERVAL` | `end` before `start` (or equal, where `end > start` is required). |
| `OUTSIDE_PARENT` | A sample outside its record, or a stage outside its session. |
| `BUCKET_NOT_ALIGNED` | A steps bucket that is not exactly one whole UTC hour. |
| `MISSING_FIELD` | A required field is missing or `null`. |
| `FIELD_TOO_LONG` | An id or `origin` over its maximum length. |

The whole request is rejected (`400`) only when the body is not valid JSON, has the
wrong types, or has an unsupported `schemaVersion`.

### Limits

Limits are **server configuration** (`appsettings`, section `HealthSync`), not constants
in code:

| Setting | Meaning | Value |
|---|---|---|
| `HealthSync:MaxItemsPerRequest` | Heart rate samples + steps buckets + sleep sessions + deleted ids. Records and stages do not count. | 5000 |
| `HealthSync:MaxBodyBytes` | Request body size. | 1 MiB |
| `HealthSync:FutureTolerance` | How far past server time a timestamp may be. | 5 min |

Over `MaxItemsPerRequest` or `MaxBodyBytes`: `413` and nothing is stored.

The client's batch size lives in its `HealthConfig` and stays below
`MaxItemsPerRequest`. A heart rate record is never split across requests, because of the
replace semantics.

### Transaction

Apply the whole request in one database transaction: either all accepted items are
stored or none are. The replace of a heart rate record needs it, and it keeps
`GET /status` consistent.

### Response `200`

```json
{
  "accepted": { "heartRateSamples": 1440, "stepsHourly": 24, "sleepSessions": 1, "deleted": 0 },
  "rejected": {
    "heartRateRecords": [ { "index": 3, "sampleIndex": 12, "reason": "TIME_IN_FUTURE" } ],
    "stepsHourly": [],
    "sleepSessions": [],
    "deleted": [],
    "watchLastSeenAt": null
  },
  "serverTime": "2026-11-01T22:30:01Z"
}
```

- `accepted` counts items stored. A repeated item counts as accepted.
  `accepted.deleted` counts ids processed, whether or not they existed.
- `rejected` has one list per block. Each entry carries the item's zero-based `index` in
  its request array and a `reason` code, plus `sampleIndex` when only one heart rate
  sample was dropped. For `deleted`, `index` points into the concatenation of
  `heartRateRecords` then `sleepSessions` ids and `block` says which. `watchLastSeenAt`
  is `null` or a reason code.
- **Rejections never echo values**, only positions and codes. They can end up in logs.
- `serverTime` is when the server committed. The client may compare it with its own
  clock for diagnostics.

On `200` the client logs only the number of rejected items and **advances its HC token**:
an invalid item will never become valid by sending it again.

### Errors

All errors use a `ProblemDetails` body. The client's reaction decides whether its HC
token moves:

| Status | When | Client reaction |
|---|---|---|
| `400` | Malformed JSON, wrong types, unsupported `schemaVersion`. | Permanent failure for that data type, log counts only. Token stays. Means a client bug. |
| `401` | Missing, invalid or expired token. | Refresh the ID token once and retry. If it is still `401`, stop and clear the session. |
| `403` | Not an elder. | Permanent failure, do not retry. |
| `413` | Over a limit. | Permanent failure: means a configuration bug (client batch larger than the server limit). |
| `429`, `5xx`, timeout, no network | Temporary. | Retry later with backoff. Token stays. |

## `GET /api/health/me/status`

What the server has stored for the signed-in elder. It feeds "Último envío" and
"Últimos datos del reloj" on the elder home, and lets the client check what reached
the server.

- Valid Bearer token, otherwise `401`. Claim `role == "elder"`, otherwise `403`.
  The elder is taken from `user_id`.

```json
{
  "lastUploadAt": "2026-11-01T22:30:01Z",
  "watchLastSeenAt": "2026-11-01T22:31:00Z",
  "lastSample": {
    "heartRate": "2026-11-01T22:21:00Z",
    "steps": "2026-11-01T23:00:00Z",
    "sleep": "2026-11-01T11:02:00Z"
  }
}
```

| Field | Meaning |
|---|---|
| `lastUploadAt` | Server time of the last `POST /health/sync` that returned `200` for this elder, empty or not. |
| `watchLastSeenAt` | Stored watch liveness signal (the maximum received). |
| `lastSample.heartRate` | Latest stored sample `time`. |
| `lastSample.steps` | `end` of the latest bucket with `count > 0`. |
| `lastSample.sleep` | Latest stored session `end`. |

Every field is `null` when there is no data yet. The response is still `200`.

## Client behavior

This is not part of the contract. It describes the traffic the server will see.

- **Initial load:** the client first gets an HC change token per type, then reads up to
  `initialWindow` back (at most 30 days, the HC history limit) and uploads it in batches.
  It saves the token only when every batch got a `2xx`.
- **Incremental:** for heart rate and sleep it reads HC changes per type. An upserted
  heart rate record is sent whole; a deleted one goes to `deleted.heartRateRecords`.
  It saves the next token after a `2xx`, and drops records written by Veiller itself.
- **Expired HC token** (unused for 30 days): the client re-reads from its last success
  minus an overlap. Expect repeated items.
- **Steps:** on every sync it re-aggregates from the last uploaded hour minus
  `stepsOverlap` up to now. Expect the same buckets several times with updated counts.
- **Liveness:** `watchLastSeenAt` goes in every request, so it reaches the server even
  when no new heart rate or sleep data exists.
- **Trigger:** foreground only for now, when the elder home opens and on "Actualizar".
  Background sync (WorkManager, at least 15 min apart) comes later.
- **Volume per elder and day:** about 1440 heart rate samples if the watch app writes
  continuous heart rate (one per minute), 24 steps buckets plus overlap, and 1–2 sleep
  sessions. On the test watch (Mi Fitness), only workout heart rate reaches HC today,
  so real volume is lower.
- **Latency:** there is no real time. Data arrives when the watch syncs with its app
  (minutes to hours), plus the sync trigger.

## Storage recommendation

**PostgreSQL as a new service in `infra/compose.yml`**, with EF Core migrations.

- It fits the current VM and has no per-write cost: continuous heart rate is thousands
  of rows per elder per day.
- SQL aggregates make the caregiver history (day / 7 days / 30 days) straightforward.
- Upserts map to `INSERT … ON CONFLICT (…) DO UPDATE`; the heart rate replace is a
  `DELETE` plus `INSERT` inside the request transaction.

Firestore also works, since the Firebase project already exists. In that case, store one
document per elder and day, never one per sample: Firestore bills per document read and
written, and history aggregates are awkward.

Suggested tables:

| Table | Primary key | Columns | Index |
|---|---|---|---|
| `heart_rate_records` | `(elder_id, hc_record_id)` | `start`, `end`, `origin`, `updated_at` | |
| `heart_rate_samples` | `(elder_id, hc_record_id, time)` | `bpm` | `(elder_id, time)` |
| `steps_hourly` | `(elder_id, start)` | `end`, `count`, `updated_at` | |
| `sleep_sessions` | `(elder_id, hc_record_id)` | `start`, `end`, `zone_offset`, `origin`, `updated_at` | `(elder_id, end)` |
| `sleep_stages` | | `elder_id`, `hc_record_id`, `start`, `end`, `stage` | |
| `health_sync_status` | `elder_id` | `last_upload_at`, `watch_last_seen_at` | |

- `heart_rate_samples` and `sleep_stages` cascade from their parent and are replaced on
  each parent write.
- In compose, Postgres should be reachable only by `api`: no published port, credentials
  from `.env`/secrets, a named volume, and a backup routine.

## Decisions

Agreed with architecture on 2026-11-01:

1. Per-item validation: `200` with `rejected` per block (index and reason code, never
   values). The client logs the count and advances its token.
2. Deletions are applied after the upserts of the same request.
3. Limits 5000 items / 1 MiB / 5 min, as server configuration. Client batch size in
   `HealthConfig`, below the server maximum.
4. Heart rate is grouped by HC record with replace semantics, which covers edited
   records.
5. `watchLastSeenAt` is sent by the client, stored by the server and returned by
   `GET /me/status`.

## Open points

1. Account deletion: Law 1581 of 2012 lets the elder ask for their data to be deleted.
   An endpoint for that is not part of v1.
2. Who reads the data (caregivers) and how is blocked until the backend has circles and
   memberships.
