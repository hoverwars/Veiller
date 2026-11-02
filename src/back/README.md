# Veiller backend

API ASP.NET Core (.NET 10, minimal API) authentifiée via Firebase.

```
src/back/
├── Veiller.slnx              # solution
├── global.json               # active le runner Microsoft.Testing.Platform pour `dotnet test`
├── Veiller.Api/              # l'API
└── Veiller.Api.Tests/        # tests d'intégration HTTP (xUnit v3)
```

## Persistance et conventions

- EF Core + PostgreSQL, code dans `Veiller.Api/Data/` (entités, configurations, migrations). Une feature ajoute ses entités dans `Data/Entities` et sa configuration dans `Data/Configurations`.
- Migrations appliquées au démarrage de l'API (PostgreSQL uniquement).
- JSON en camelCase, enums en string, dates en UTC ISO 8601 (`DateTimeOffset`, horloge via `TimeProvider`).
- Erreurs au format `ProblemDetails`.
- Tests : base SQLite en mémoire, isolée par `VeillerApiFactory` (`factory.CreateDbScope(out var db)` pour semer ou vérifier des données). `new VeillerApiFactory(settings)` surcharge des clés de configuration pour une classe de tests (ex. des limites `HealthSync` minuscules).
- Synchro santé (`Health/`) : limites dans la section `HealthSync` de `appsettings.json`. Les horodatages des tables de santé sont des `DateTime` UTC (agrégats SQL de la dernière mesure). Détails et décisions : `docs/dev/health_sync_backend_notes.md`.
- Suivi des patients (`Patients/`) : `GET /api/patients`, `/api/patients/{circleId}` et `/history?range=day|week|month&tzOffset=%2B02:00` (cuidadores uniquement, 404 pour les autres). Lecture seule sur les tables de santé ; les champs sans donnée, périmés (> 24 h) ou sans consentement valent `null`.
- Accueil de l'adulto mayor (`Elder/`) : `GET /api/elder/home` (elder uniquement, 403 sinon) : `lastSendMinutesAgo` (`null` si jamais synchronisé), `watch.connected` (`watchLastSeenAt` plus récent que `HealthSync:WatchConnectedWindow`, 6 h par défaut), `watch.batteryPercent` (toujours `null`, non ingérée) et les cuidadores de son cercle (admin inclus, titulaire exclu).
- Durcissement de la synchro : `HealthSync:MaxSamplesPerRecord` (un enregistrement FC plus gros est rejeté en `TOO_MANY_SAMPLES`, pas en 413), `HealthSync:MaxAge` (`TIME_TOO_OLD`, 35 j), rate limiting `RateLimiting:HealthSyncPerMinute` (429, 30/min par elder).
- Rétention (`Retention/`) : tâche de fond quotidienne qui supprime FC, pas et sommeil plus vieux que `Retention:HealthDataDays` (365 j par défaut). `Retention:Enabled=false` la désactive (tests).
- Alertes (`Alerts/`) : règles par cercle (`HeartRateHigh`, `HeartRateLow`, `SleepLow`, `NoSync`, défauts dans la section `Alerts`), évaluées après chaque synchro et toutes les 15 min (silence de la montre). Une alerte par épisode ; elle se résout seule quand la condition disparaît, et une alerte résolue à la main ne revient pas tant que la condition dure. Routes : `GET /api/circles/{id}/alerts?status=&limit=`, `POST .../alerts/{alertId}/seen|resolve` (cuidadores, 404 pour les autres, titulaire compris), `GET|PUT .../alert-rules` (PUT : admin ou titulaire), `PUT|DELETE /api/devices/token`. Le push FCM est « data only » (type, ids, aucune valeur de santé) derrière `IPushSender`, avec les credentials Firebase déjà utilisés pour l'auth (`GOOGLE_APPLICATION_CREDENTIALS`).
- Premium (`Billing/`) : `POST /api/circles/{id}/plan/subscribe` (corps `purchaseToken`) et `/plan/cancel`, réservés au sponsor. La validation du reçu passe par `IPurchaseVerifier` ; l'implémentation Google Play n'est pas branchée (501 tant que c'est le cas). La rétrogradation garde l'admin et le sponsor puis les plus anciens, et renvoie les membres retirés. Pas d'expiration automatique d'abonnement pour l'instant.
- Identifiants (`Profile/CredentialsEndpoints.cs`) : `PUT /api/profile/email` et `PUT /api/profile/password`, qui redemandent le mot de passe actuel (403 s'il est faux). Un changement de mot de passe révoque toutes les sessions.

## Lancer les tests

Prérequis : SDK .NET 10. Aucun credential Firebase ni accès réseau n'est nécessaire.

```bash
cd src/back
dotnet test --solution Veiller.slnx
```

Sur le SDK .NET 10, `dotnet test` sans `--solution` (ou `--project`) échoue : c'est voulu par le nouveau runner de tests, activé par `global.json`.

Variantes utiles :

```bash
# un seul projet
dotnet test --project Veiller.Api.Tests

# filtrer par nom de test
dotnet test --solution Veiller.slnx --filter-method "*Login_with_bad_credentials*"

# filtrer par classe
dotnet test --solution Veiller.slnx --filter-class "*LoginEndpointTests"
```

Les tests tournent aussi en CI : l'étape `.NET Test` de `.github/workflows/build.yml` s'exécute avant le publish, donc un test en échec bloque le déploiement.

## Comment fonctionnent les tests

Les tests démarrent la vraie API en mémoire (`WebApplicationFactory<Program>`) et lui envoient de vraies requêtes HTTP. Seul Firebase est remplacé par des fakes :

| Production | Test |
|---|---|
| `IFirebaseIdentityClient` (REST Identity Toolkit : sign-in, refresh) | `FakeFirebaseIdentityClient` |
| `IFirebaseAdmin` (façade du SDK Admin : lecture/création d'utilisateurs, claims) | `FakeFirebaseAdmin` |

`VeillerApiFactory` (`Veiller.Api.Tests/Infrastructure/`) assemble le tout et fournit une configuration `Firebase` factice.

### Ajouter un test

1. Créer une classe dans `Veiller.Api.Tests/<Feature>/`, sur le modèle de `Auth/LoginEndpointTests.cs`.
2. Instancier `VeillerApiFactory`, appeler `CreateClient()`.
3. Configurer les fakes (`factory.Identity.OnSignIn`, `factory.Admin.Users`), envoyer la requête, vérifier le statut et le corps.

Si un endpoint utilise une nouvelle méthode du SDK Firebase, l'ajouter à `IFirebaseAdmin`, à `FirebaseAdminFacade` et à `FakeFirebaseAdmin` plutôt que d'utiliser `FirebaseAuth` directement dans l'endpoint.

## Lancer l'API

```bash
cd src/back/Veiller.Api
dotnet run
```

Nécessite `Firebase:ProjectId`, `Firebase:WebApiKey` (user-secrets ou variables d'environnement) et `GOOGLE_APPLICATION_CREDENTIALS` pointant vers le JSON du service account. Swagger : `/swagger`.
