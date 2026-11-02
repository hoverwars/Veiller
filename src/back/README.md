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
- Tests : base SQLite en mémoire, isolée par `VeillerApiFactory` (`factory.CreateDbScope(out var db)` pour semer ou vérifier des données).

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
