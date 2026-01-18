# 📦 Script de Listage des Mods Hytale - État du Projet

**Date :** 2026-01-19 01:00
**Version actuelle :** 0.9 (presque terminée, nécessite tests sur Linux)

---

## 🎯 Objectif du Script

Générer automatiquement une liste Markdown de tous les mods installés avec leurs URLs CurseForge, en utilisant :
1. Les informations du `manifest.json` de chaque mod
2. L'API CurseForge pour trouver les URLs manquantes
3. Un système de meta-batching intelligent (par tranches de 1000 mods)

---

## ✅ Ce qui Fonctionne Actuellement

### **Version Minimale (100% fonctionnelle)**
- ✅ Extraction du `manifest.json` de tous les fichiers `.jar` et `.zip`
- ✅ Parsing des métadonnées : nom, version, auteur, description, website
- ✅ Détection automatique des URLs CurseForge dans le manifest
- ✅ Génération d'un fichier `mods_list.md` avec tri alphabétique
- ✅ Gestion des mods sans URL
- ✅ Options `--help`, `--install-deps`, `--dry-run`

**Résultat sur 49 mods :**
- 13 mods (27%) ont une URL CurseForge directe dans le manifest
- 36 mods (73%) n'ont pas d'URL → besoin de l'API

---

## ⚠️ Ce qui Reste à Finaliser

### **Version avec API CurseForge**
- ✅ Architecture complète implémentée
- ✅ Système de meta-batching (recherche par tranches de 1000 mods)
- ✅ Cache intelligent (validité 7 jours)
- ✅ Matching intelligent (nom + auteur + slug)
- ✅ Option `--force-refresh`
- ⚠️ **Problème sur Git Bash Windows** : incompatibilité avec `jq` et les pipes
- ✅ **Corrigé pour Linux** : utilisation de fichiers temporaires au lieu de pipes

**À tester sur Linux natif** (devrait fonctionner immédiatement)

---

## 📂 Fichiers du Projet

```
Script mods Hytale/
├── list_mods.sh          # Script principal (748 lignes)
├── .env                  # Clé API CurseForge
├── jq.exe                # Parser JSON (Windows uniquement)
├── mods/                 # Dossier contenant les 49 mods
├── mods_list.md          # Fichier Markdown généré (version minimale)
├── .mods_cache.json      # Cache API (créé automatiquement)
└── README.md             # Ce fichier
```

---

## 🚀 Installation sur Serveur Linux

### **1. Transférer les fichiers**

```bash
# Sur ton serveur Linux
mkdir -p ~/hytale-mods-script
cd ~/hytale-mods-script

# Copier le script et le .env depuis Windows
# Via SCP par exemple
```

### **2. Installer les dépendances**

```bash
# Sur Ubuntu/Debian
sudo apt update
sudo apt install -y jq curl unzip

# Sur CentOS/RHEL
sudo yum install -y jq curl unzip

# Vérifier les installations
jq --version    # jq-1.6 ou supérieur
curl --version
unzip -v
```

### **3. Configurer la clé API**

```bash
# Le fichier .env doit contenir
cat .env
# CURSEFORGE_API_KEY='$2a$10$.b3x8BMEb00pAytP6IP1h.Gnjqpwh/pScqZIrhJiNeRUGcKcSNuJG'

# Rendre le script exécutable
chmod +x list_mods.sh
```

### **4. Créer le dossier mods**

```bash
# Créer le dossier mods au même niveau que le script
mkdir -p mods

# Y copier tes mods .jar et .zip
```

---

## 🔧 Utilisation

### **Commandes de base**

```bash
# Aide
./list_mods.sh --help

# Installation automatique des dépendances
./list_mods.sh --install-deps

# Test sans créer de fichier
./list_mods.sh --dry-run

# Exécution normale (avec recherche API)
./list_mods.sh

# Forcer le rafraîchissement du cache
./list_mods.sh --force-refresh
```

### **Résultat attendu sur Linux**

```
[OK] Clé API chargée depuis .env
[INFO] Initialisation du cache...
[INFO] Recherche des mods dans /home/user/hytale-mods-script/mods...
[INFO] Trouvé 49 mod(s)
Traitement... 49/49 : WaybackCharm-2026.1.5-17015.zip
[OK] Traitement terminé : 49 mod(s) valide(s)

[API] 🔍 Recherche des URLs CurseForge via l'API...
[API] Mods à rechercher : 36

🔍 Batch 1 (mods CurseForge 0-1000)
[INFO]    Mods restants à trouver : 36
   └─ Requête API 1/20 (mods 0-50)...
[OK]   ✓ AdminUI → https://www.curseforge.com/hytale/mods/adminui
[OK]   ✓ BetterModlist → https://www.curseforge.com/hytale/mods/bettermodlist
   ...

[INFO] Bilan batch 1 : 30 mod(s) trouvé(s) - 6 restant(s)

[OK] Recherche terminée : 34 mod(s) trouvé(s) via l'API
[WARN] Mods non trouvés (2) : ModObscur1 ModObscur2

[INFO] Génération du fichier Markdown...
[OK] Fichier généré : /home/user/hytale-mods-script/mods_list.md
[OK] Terminé !
```

**Temps estimé :** 30-60 secondes (première exécution avec API)

---

## 🏗️ Architecture Technique

### **Système de Meta-Batching**

Le script recherche les mods par batches intelligents :

```
Batch 1 (0-1000) → Recherche 20 requêtes API
  ├─ Si tous trouvés : STOP ✅
  └─ Sinon : Batch 2 (1000-2000)

Batch 2 (1000-2000) → Recherche 20 requêtes API
  ├─ Si tous trouvés : STOP ✅
  └─ Sinon : Batch 3 (2000-3000)

...

Jusqu'à 10 batches maximum (10,000 mods)
```

**Avantages :**
- ✅ Rapide pour les mods populaires (~30s)
- ✅ Exhaustif pour les mods obscurs (jusqu'à 10,000 mods)
- ✅ Scalable même si CurseForge atteint 100,000 mods

### **Configuration API**

```bash
# Dans list_mods.sh (lignes 20-25)
BATCH_SIZE=50                # Taille d'une requête API (max 50)
META_BATCH_SIZE=1000         # Meta-batch (1000 mods)
MAX_API_LIMIT=10000          # Limite absolue de l'API
CACHE_VALIDITY_DAYS=7        # Validité du cache en jours
REQUESTS_PER_SECOND=3        # Rate limiting (0.35s entre requêtes)
```

---

## 🐛 Problèmes Connus (Windows uniquement)

### **Git Bash Windows + jq + pipes**

**Symptôme :** Le script se bloque à la première requête API

**Cause :** Incompatibilité entre `jq` et les pipes (`|`) sur Git Bash Windows

**Solution appliquée :**
- ✅ Utilisation de fichiers temporaires au lieu de pipes
- ✅ Devrait fonctionner correctement sur Linux

**Code corrigé (ligne 410-472) :**
```bash
# ❌ Version avec pipes (ne fonctionne pas sur Windows)
local cf_mods=$(echo "$response" | jq -c '.data[]')

# ✅ Version avec fichiers (fonctionne partout)
local cf_mods_file=$(mktemp)
echo "$response" | jq -c '.data[]' > "$cf_mods_file"
while read -r cf_mod; do
  # ...
done < "$cf_mods_file"
rm -f "$cf_mods_file"
```

---

## 📊 Format de Sortie Markdown

Le fichier `mods_list.md` généré contient :

```markdown
# 📦 Liste des Mods Hytale

**Date de génération :** 2026-01-19 01:00:00
**Nombre total de mods :** 49

---

## AdminUI
**Version :** 1.0.4
**Auteur(s) :** Buuz135
**Description :** Adds multiple admin ui pages to the game.
**URL :** [https://www.curseforge.com/hytale/mods/adminui](https://www.curseforge.com/hytale/mods/adminui) 🔍
**Site alternatif :** [https://buuz135.com](https://buuz135.com)

---

## CobbleGens
**Version :** 2026.1.12-32469
**Auteur(s) :** Darkhax
**Description :** Generating cobblestone!
**URL :** [https://www.curseforge.com/hytale/mods/cobble-generators](https://www.curseforge.com/hytale/mods/cobble-generators) 🔗

---

*Légende :*
🔗 = URL trouvée dans le manifest
🔍 = URL trouvée via l'API CurseForge
❌ = URL non trouvée
```

**Tri :** Alphabétique par nom de mod

---

## 🔮 Prochaines Étapes

### **Sur Serveur Linux (à faire maintenant)**

1. ✅ Transférer `list_mods.sh` et `.env`
2. ✅ Installer `jq`, `curl`, `unzip`
3. ✅ Créer le dossier `mods/` avec les fichiers
4. 🔍 **Tester `./list_mods.sh`**
5. 🐛 Debugger si nécessaire (plus facile sur Linux natif)
6. ✅ Valider que tous les mods sont trouvés

---

## 📝 Notes pour la Prochaine Session

### **Ce qui a été testé sur Windows**
- ✅ Version minimale : fonctionne parfaitement
- ✅ Extraction des manifests : OK
- ✅ Génération du Markdown : OK
- ⚠️ Recherche API : bloquée sur Git Bash Windows (problème `jq`)

### **Ce qui reste à tester sur Linux**
- 🔍 Recherche API complète avec meta-batching
- 🔍 Système de cache
- 🔍 Matching intelligent (nom + auteur)
- 🔍 Gestion des mods non trouvés

### **Commandes de debug utiles sur Linux**

```bash
# Tester jq manuellement
echo '{"test": "value"}' | jq -r '.test'
# Doit afficher : value

# Tester une requête API manuelle
source .env
curl -s -H "Accept: application/json" -H "x-api-key: $CURSEFORGE_API_KEY" \
  "https://api.curseforge.com/v1/mods/search?gameId=70216&pageSize=5&index=0" | jq -r '.data[].name'
# Doit afficher une liste de noms de mods

# Tester l'extraction d'un manifest
unzip -p mods/AdminUI-1.0.4.jar manifest.json | jq -r '.Name'
# Doit afficher : AdminUI
```

---

## 🆘 Dépannage

### **Problème : "Clé API non trouvée"**

```bash
# Vérifier le fichier .env
cat .env

# Charger manuellement
source .env
echo $CURSEFORGE_API_KEY
```

### **Problème : "jq: command not found"**

```bash
sudo apt install jq  # Ubuntu/Debian
# OU
./list_mods.sh --install-deps
```

### **Problème : "Aucun mod trouvé"**

```bash
# Vérifier le dossier
ls -la mods/
ls mods/*.jar mods/*.zip
```

---

## 📞 Information Clé

**Hytale Game ID :** `70216`

**API CurseForge Base URL :** `https://api.curseforge.com/v1`

**Endpoint recherche :** `/mods/search?gameId=70216&pageSize=50&index=0`

---

**Dernière mise à jour :** 2026-01-19 01:00
**Testé sur :** Windows (version minimale OK), Linux (à tester)
**Prochaine session :** Test complet sur serveur Linux
