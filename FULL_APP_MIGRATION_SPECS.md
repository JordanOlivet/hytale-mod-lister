# Spécifications de Migration - HytaleModLister

## 1. Vue d'ensemble

### Objectif
Transformer l'application console `HytaleModLister` en une stack applicative web complète composée de :
- **Backend** : API REST en .NET 10
- **Frontend** : Application web Svelte
- **Déploiement** : Docker Compose

### État actuel
L'application console actuelle :
- Extrait les métadonnées des fichiers mods (`.jar`/`.zip`) via leur `manifest.json`
- Recherche les URLs CurseForge correspondantes via l'API
- Utilise plusieurs stratégies de matching (exact, slug, substring, fuzzy Levenshtein)
- Génère un fichier `mods_list.md` avec les résultats
- Maintient un cache de 7 jours dans `.mods_cache.json`

---

## 2. Architecture Cible

```
┌─────────────────────────────────────────────────────────────────┐
│                        Docker Compose                           │
├─────────────────────────────────┬───────────────────────────────┤
│           Frontend              │            Backend            │
│         (Svelte + Vite)         │           (.NET 10)           │
│                                 │                               │
│  ┌───────────────────────────┐  │  ┌─────────────────────────┐  │
│  │   Table des mods          │  │  │   API REST              │  │
│  │   - URLs cliquables       │  │  │   - GET /api/mods       │  │
│  │   - Infos secondaires     │  │  │   - POST /api/refresh   │  │
│  │   - Thème clair/sombre    │  │  │   - GET /api/status     │  │
│  └───────────────────────────┘  │  └─────────────────────────┘  │
│                                 │                               │
│  Port: 3000                     │  Port: 5000                   │
└─────────────────────────────────┴───────────────────────────────┘
                                  │
                                  ▼
                    ┌─────────────────────────┐
                    │   Volume Docker         │
                    │   /app/mods             │
                    │   (fichiers .jar/.zip)  │
                    └─────────────────────────┘
```

---

## 3. Backend (.NET 10)

### 3.1 Responsabilités
- Extraction des métadonnées des mods depuis le dossier monté
- Recherche des URLs CurseForge via l'API
- Gestion du cache (persistance et invalidation)
- Exposition d'une API REST pour le frontend
- Tâche planifiée de rafraîchissement automatique (CRON)

### 3.2 Endpoints API

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| `GET` | `/api/mods` | Retourne la liste des mods avec leurs informations |
| `POST` | `/api/mods/refresh` | Déclenche un rafraîchissement manuel de la liste |
| `POST` | `/api/mods/refresh?force=true` | Rafraîchissement en ignorant le cache |
| `GET` | `/api/status` | Retourne le statut du service (dernière MAJ, nombre de mods, refresh en cours) |
| `GET` | `/health` | Health check pour Docker/monitoring |

### 3.3 Modèles de données

```csharp
// Réponse GET /api/mods
public record ModListResponse
{
    public DateTime LastUpdated { get; init; }
    public int TotalCount { get; init; }
    public List<ModDto> Mods { get; init; }
}

public record ModDto
{
    public string Name { get; init; }
    public string FileName { get; init; }        // Nom du fichier source (.jar/.zip)
    public string Version { get; init; }
    public string? Description { get; init; }
    public List<string> Authors { get; init; }
    public string? Website { get; init; }        // URL générique du manifest (fallback)
    public string? CurseForgeUrl { get; init; }
    public string? FoundVia { get; init; }       // "manifest", "cache", "exact", "slug", "substring", "fuzzy:XX%"
}

// Réponse GET /api/status
public record StatusResponse
{
    public DateTime? LastUpdated { get; init; }
    public int ModCount { get; init; }
    public bool IsRefreshing { get; init; }
    public RefreshProgress? Progress { get; init; }  // Progression en cours
    public DateTime? NextScheduledRefresh { get; init; }
}

public record RefreshProgress
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public string? CurrentMod { get; init; }
}

// Structure du cache (.mods_cache.json)
public record ModCache
{
    public DateTime LastUpdated { get; init; }
    public Dictionary<string, CachedMod> Mods { get; init; }
}

public record CachedMod
{
    public string? CurseForgeUrl { get; init; }
    public bool NotFound { get; init; }          // true si recherche effectuée mais non trouvé
    public DateTime CachedAt { get; init; }
}
```

### 3.4 Configuration

```json
// appsettings.json
{
  "ModsPath": "/app/mods",
  "CachePath": "/app/data/mods_cache.json",
  "Cache": {
    "ValidityDays": 7
  },
  "CurseForge": {
    "ApiKey": "",  // Peut être vide, l'API fonctionne sans
    "GameId": 70216,
    "RateLimitMs": 350
  },
  "Scheduler": {
    "RefreshCron": "0 0 * * *",  // Tous les jours à minuit
    "Timezone": "UTC"            // Timezone pour le CRON
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]  // Pour dev local
  }
}
```

### 3.5 Structure du projet

```
backend/
├── HytaleModLister.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/
│   │   └── ModsController.cs
│   ├── Services/
│   │   ├── IModExtractorService.cs
│   │   ├── ModExtractorService.cs
│   │   ├── ICurseForgeService.cs
│   │   ├── CurseForgeService.cs
│   │   ├── IModMatcherService.cs
│   │   ├── ModMatcherService.cs
│   │   ├── ICacheService.cs
│   │   ├── CacheService.cs
│   │   └── RefreshSchedulerService.cs (BackgroundService)
│   ├── Models/
│   │   ├── ModInfo.cs
│   │   ├── CfMod.cs
│   │   └── Cache.cs
│   └── Dockerfile
└── HytaleModLister.Api.sln
```

### 3.6 Dépendances NuGet

- `Fastenshtein` - Calcul de distance Levenshtein
- `Cronos` - Parsing des expressions CRON
- `Microsoft.Extensions.Hosting` - Background services

---

## 4. Frontend (Svelte)

### 4.1 Responsabilités
- Affichage de la liste des mods sous forme de table
- Mise en évidence des URLs (cliquables, style proéminent)
- Affichage discret des informations secondaires
- Gestion du thème clair/sombre
- Bouton de rafraîchissement manuel
- Affichage du statut (dernière MAJ, refresh en cours)

### 4.2 Interface utilisateur

#### Header
- Titre de l'application
- Toggle thème clair/sombre
- Indicateur de dernière mise à jour
- Bouton "Refresh" avec état de chargement

#### Table des mods

| Colonne | Style | Description |
|---------|-------|-------------|
| **Name** | Principal, gras | Nom du mod |
| **URL** | **Principal, lien cliquable, icône externe** | CurseForge URL si disponible, sinon Website du manifest, sinon "-" |
| Version | Secondaire, discret | Version du mod |
| Authors | Secondaire, discret | Liste des auteurs |
| Description | Secondaire, discret, tronquée (max 100 chars) | Description du mod |
| Source | Badge petit | Source de l'URL : "manifest", "API", "website" (fallback), ou vide |

**Logique d'affichage de l'URL :**
1. Si `curseForgeUrl` existe → afficher avec badge selon `foundVia`
2. Sinon si `website` existe → afficher avec badge "website" (style différent, moins mis en avant)
3. Sinon → afficher "-" en gris

#### Fonctionnalités de la table
- Tri par colonnes (nom, version, auteur)
- Recherche/filtre en temps réel
- Pagination ou scroll infini (selon le nombre de mods)

### 4.3 Thèmes

**Thème clair**
- Background : `#ffffff`
- Text principal : `#1a1a1a`
- Text secondaire : `#6b7280`
- Accent (liens) : `#2563eb`

**Thème sombre**
- Background : `#0f0f0f`
- Text principal : `#f5f5f5`
- Text secondaire : `#9ca3af`
- Accent (liens) : `#60a5fa`

**Persistance du thème :**
- Stocké dans `localStorage` sous la clé `theme`
- Valeurs : `"light"` ou `"dark"`
- Par défaut : respecter `prefers-color-scheme` du système

### 4.4 Structure du projet

```
frontend/
├── src/
│   ├── routes/
│   │   └── +page.svelte
│   ├── lib/
│   │   ├── components/
│   │   │   ├── ModsTable.svelte
│   │   │   ├── Header.svelte
│   │   │   ├── ThemeToggle.svelte
│   │   │   └── RefreshButton.svelte
│   │   ├── stores/
│   │   │   ├── mods.ts
│   │   │   └── theme.ts
│   │   └── api/
│   │       └── client.ts
│   ├── app.html
│   └── app.css
├── static/
├── package.json
├── svelte.config.js
├── vite.config.ts
├── Dockerfile
└── nginx.conf
```

### 4.5 États de l'interface

| État | Affichage |
|------|-----------|
| **Chargement initial** | Spinner centré, message "Loading mods..." |
| **Liste vide** | Message "No mods found. Add .jar or .zip files to the mods folder." |
| **Erreur API** | Bandeau d'erreur rouge avec message et bouton "Retry" |
| **Refresh en cours** | Bouton désactivé avec spinner, barre de progression avec X/Y mods |
| **Refresh terminé** | Toast de succès temporaire (3s) |

### 4.6 Types TypeScript

```typescript
// src/lib/types.ts
export interface Mod {
  name: string;
  fileName: string;
  version: string;
  description?: string;
  authors: string[];
  website?: string;
  curseForgeUrl?: string;
  foundVia?: string;
}

export interface ModListResponse {
  lastUpdated: string;
  totalCount: number;
  mods: Mod[];
}

export interface RefreshProgress {
  processed: number;
  total: number;
  currentMod?: string;
}

export interface StatusResponse {
  lastUpdated?: string;
  modCount: number;
  isRefreshing: boolean;
  progress?: RefreshProgress;
  nextScheduledRefresh?: string;
}
```

### 4.7 Dépendances npm

- `svelte` + `@sveltejs/kit` - Framework
- `@sveltejs/adapter-static` - Adapter pour build statique (nginx)
- `typescript` - Typage
- `tailwindcss` - Styling (optionnel, CSS vanilla acceptable)

---

## 5. Docker et Déploiement

### 5.1 Docker Compose

```yaml
# docker-compose.yml
services:
  backend:
    build:
      context: ./backend
      dockerfile: HytaleModLister.Api/Dockerfile
    ports:
      - "5000:8080"
    volumes:
      - ./mods:/app/mods:ro
      - backend-data:/app/data
    environment:
      - CurseForge__ApiKey=${CURSEFORGE_API_KEY:-}
      - TZ=UTC
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
    restart: unless-stopped

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    ports:
      - "3000:80"
    depends_on:
      backend:
        condition: service_healthy
    restart: unless-stopped

volumes:
  backend-data:
```

### 5.2 Dockerfile Backend

```dockerfile
# backend/HytaleModLister.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish HytaleModLister.Api/HytaleModLister.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Dossier pour les mods (monté en volume)
RUN mkdir -p /app/mods /app/data

# curl pour le healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENTRYPOINT ["dotnet", "HytaleModLister.Api.dll"]
```

### 5.3 Dockerfile Frontend

```dockerfile
# frontend/Dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/build /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### 5.4 Configuration Nginx (Frontend)

```nginx
# frontend/nginx.conf
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy API vers le backend
    location /api/ {
        proxy_pass http://backend:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### 5.5 Volumes et chemins

| Chemin Host | Chemin Container | Description |
|-------------|------------------|-------------|
| `./mods` | `/app/mods` (ro) | Dossier des fichiers mods |
| `backend-data` | `/app/data` | Cache et données persistantes |

---

## 6. Variables d'environnement

| Variable | Service | Description | Défaut |
|----------|---------|-------------|--------|
| `CURSEFORGE_API_KEY` | Backend | Clé API CurseForge (optionnelle) | - |
| `API_URL` | Frontend | URL du backend | `http://backend:8080` |

---

## 7. Tâches de Migration

### Phase 1 : Backend
- [ ] Créer la solution .NET 10 avec la structure de projet
- [ ] Migrer la logique d'extraction des mods dans `ModExtractorService`
- [ ] Migrer la logique CurseForge dans `CurseForgeService`
- [ ] Migrer la logique de matching dans `ModMatcherService`
- [ ] Implémenter `CacheService` pour la persistance
- [ ] Créer `ModsController` avec les endpoints REST
- [ ] Implémenter `RefreshSchedulerService` pour le CRON
- [ ] Créer le Dockerfile
- [ ] Tester l'API

### Phase 2 : Frontend
- [ ] Initialiser le projet SvelteKit
- [ ] Implémenter le client API
- [ ] Créer le store pour les mods
- [ ] Créer le composant `ModsTable`
- [ ] Implémenter le thème clair/sombre
- [ ] Créer le `Header` avec le bouton refresh
- [ ] Ajouter le tri et la recherche
- [ ] Créer le Dockerfile + nginx.conf
- [ ] Tester l'interface

### Phase 3 : Intégration
- [ ] Créer le `docker-compose.yml`
- [ ] Tester le déploiement complet
- [ ] Documenter l'utilisation dans le README

---

## 8. Utilisation

### Développement local

```bash
# Backend
cd backend/HytaleModLister.Api
dotnet run

# Frontend
cd frontend
npm install
npm run dev
```

### Production (Docker)

```bash
# Avec clé API CurseForge (optionnel)
export CURSEFORGE_API_KEY=your_key_here

# Démarrer les services
docker compose up -d

# Accéder à l'application
# Frontend : http://localhost:3000
# Backend API : http://localhost:5000/api/mods
```

---

## 9. Notes techniques

### Gestion des erreurs
- Le backend doit retourner des codes HTTP appropriés (200, 400, 500)
- Le frontend doit afficher des messages d'erreur user-friendly
- Les erreurs de l'API CurseForge ne doivent pas bloquer l'affichage des mods (afficher sans URL)

### Performance
- Le refresh peut prendre du temps (rate limiting API) : afficher un indicateur de progression
- Utiliser le cache autant que possible
- Le frontend doit gérer l'état "refresh en cours" avec un spinner

### Sécurité
- Le dossier mods est monté en lecture seule (`ro`)
- La clé API n'est pas exposée au frontend
- Pas d'authentification requise (usage local/privé)

### Comportement du refresh
- Le POST `/api/mods/refresh` retourne immédiatement (202 Accepted)
- Le frontend poll GET `/api/status` toutes les 2 secondes pendant le refresh
- La progression est affichée via `RefreshProgress` (X/Y mods traités)
- À la fin du refresh, le frontend recharge la liste des mods

### Logging Backend
- Utiliser `ILogger` standard de ASP.NET Core
- Logs structurés en JSON en production
- Niveaux : Information pour les opérations normales, Warning pour les mods non trouvés, Error pour les exceptions

### Comportement au démarrage
- Le backend lance automatiquement un refresh forcé (ignore le cache) au démarrage
- Le refresh s'exécute en arrière-plan, l'API est disponible immédiatement
- GET `/api/mods` retourne une liste vide avec `lastUpdated: null` pendant le premier refresh
- Le frontend affiche l'état "Refresh in progress..." au chargement initial si nécessaire

### Migration depuis l'application console
- L'ancien dossier `HytaleModLister/` peut être supprimé après migration
- Le cache `.mods_cache.json` existant est compatible et sera réutilisé
- Les fichiers `.api_key` et `.env` ne sont plus utilisés (remplacés par variables d'environnement)

---

## 10. Structure finale du projet

```
HytaleModLister/
├── backend/
│   ├── HytaleModLister.Api/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── HytaleModLister.Api.csproj
│   │   └── Dockerfile
│   └── HytaleModLister.Api.sln
├── frontend/
│   ├── src/
│   │   ├── routes/
│   │   ├── lib/
│   │   ├── app.html
│   │   └── app.css
│   ├── static/
│   ├── package.json
│   ├── svelte.config.js
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── Dockerfile
│   └── nginx.conf
├── mods/                    # Dossier des fichiers .jar/.zip (monté en volume)
├── docker-compose.yml
├── .gitignore
└── README.md
```

---

## 11. Fichiers à supprimer après migration

- `HytaleModLister/` (ancien dossier de l'app console)
- `.api_key`
- `.env`
- `mods_list.md` (remplacé par l'interface web)
- `FULL_APP_MIGRATION_SPECS.md` (ce fichier, une fois la migration terminée)
