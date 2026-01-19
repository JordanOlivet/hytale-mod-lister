#!/bin/bash

#############################################
# Script de listage des mods Hytale
# Génère un fichier Markdown avec les infos des mods
#############################################

set -o pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODS_DIR="$SCRIPT_DIR/mods"
OUTPUT_FILE="$SCRIPT_DIR/mods_list.md"
CACHE_FILE="$SCRIPT_DIR/.mods_cache.json"
ENV_FILE="$SCRIPT_DIR/.env"
TEMP_DATA_DIR="/tmp/mods_data_$$"
HYTALE_GAME_ID="70216"
API_BASE_URL="https://api.curseforge.com/v1"

# Configuration API et batching
BATCH_SIZE=50                # Taille d'une requête API (max 50)
META_BATCH_SIZE=1000         # Meta-batch (1000 mods)
MAX_API_LIMIT=10000          # Limite absolue de l'API
CACHE_VALIDITY_DAYS=7        # Validité du cache en jours
REQUESTS_PER_SECOND=3        # Rate limiting

# Couleurs pour l'affichage
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Variables globales
DRY_RUN=false
FORCE_REFRESH=false
CURSEFORGE_API_KEY=""
USE_API=false

#############################################
# Fonctions utilitaires
#############################################

print_help() {
    cat << EOF
Usage: $0 [OPTIONS]

Génère une liste Markdown des mods Hytale avec leurs URLs CurseForge.

OPTIONS:
    --help              Affiche cette aide
    --install-deps      Installe les dépendances manquantes (jq)
    --dry-run           Mode test (n'écrit pas de fichier)
    --force-refresh     Ignore le cache et refait toutes les requêtes API

EXEMPLES:
    $0                      # Exécution normale
    $0 --install-deps       # Installe jq si nécessaire
    $0 --dry-run            # Test sans créer de fichier
    $0 --force-refresh      # Force le rafraîchissement du cache

PRÉREQUIS:
    - jq (parser JSON)
    - curl (requêtes HTTP)
    - unzip (extraction des archives)

CONFIGURATION:
    La clé API CurseForge doit être définie dans le fichier .env :
    CURSEFORGE_API_KEY='votre_clé_ici'

FONCTIONNEMENT:
    Le script utilise un système de meta-batching pour rechercher les mods :
    - Recherche par batches de 1000 mods sur CurseForge
    - Continue jusqu'à trouver tous les mods ou atteindre la limite de 10,000
    - Cache les résultats pour les prochaines exécutions

EOF
}

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_api() {
    echo -e "${CYAN}[API]${NC} $1"
}

cleanup() {
    rm -rf "$TEMP_DATA_DIR"
}

trap cleanup EXIT

check_dependencies() {
    local missing_deps=()

    if ! command -v unzip &> /dev/null; then
        missing_deps+=("unzip")
    fi

    if ! command -v curl &> /dev/null; then
        missing_deps+=("curl")
    fi

    # Chercher jq dans le PATH ou dans le dossier courant
    if ! command -v jq &> /dev/null && [ ! -f "$SCRIPT_DIR/jq.exe" ]; then
        missing_deps+=("jq")
    fi

    if [ ${#missing_deps[@]} -gt 0 ]; then
        log_error "Dépendances manquantes : ${missing_deps[*]}"
        log_info "Exécutez '$0 --install-deps' pour les installer"
        return 1
    fi

    # Ajouter le dossier courant au PATH si jq.exe est présent
    if [ -f "$SCRIPT_DIR/jq.exe" ]; then
        export PATH="$SCRIPT_DIR:$PATH"
    fi

    return 0
}

install_dependencies() {
    log_info "Vérification des dépendances..."

    # Vérifier si jq est déjà disponible
    if command -v jq &> /dev/null || [ -f "$SCRIPT_DIR/jq.exe" ]; then
        log_success "Toutes les dépendances sont déjà installées"
        return 0
    fi

    log_info "Installation de jq..."

    # Détection de l'OS
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        sudo apt-get update && sudo apt-get install -y jq
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        brew install jq
    elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        log_info "Téléchargement de jq pour Windows..."
        curl -L -o "$SCRIPT_DIR/jq.exe" https://github.com/stedolan/jq/releases/latest/download/jq-win64.exe
        chmod +x "$SCRIPT_DIR/jq.exe"
        log_success "jq installé dans $SCRIPT_DIR/jq.exe"
    else
        log_error "OS non supporté pour l'installation automatique"
        log_info "Téléchargez jq depuis : https://stedolan.github.io/jq/download/"
        return 1
    fi

    log_success "Dépendances installées avec succès"
}

load_env() {
    local api_key_file="$SCRIPT_DIR/.api_key"

    # Essayer de charger depuis .api_key (fichier texte brut)
    if [ -f "$api_key_file" ]; then
        CURSEFORGE_API_KEY=$(cat "$api_key_file")
        if [ -n "$CURSEFORGE_API_KEY" ]; then
            log_success "Clé API chargée depuis .api_key"
            USE_API=true
            return 0
        fi
    fi

    # Sinon essayer le fichier .env
    if [ -f "$ENV_FILE" ]; then
        # Extraire la clé sans utiliser source (évite l'interprétation des $)
        CURSEFORGE_API_KEY=$(grep -oP "(?<=CURSEFORGE_API_KEY=['\"])[^'\"]+(?=['\"])" "$ENV_FILE" 2>/dev/null || \
                           grep -oP "(?<=CURSEFORGE_API_KEY=)[^'\"]+" "$ENV_FILE" 2>/dev/null)

        if [ -n "${CURSEFORGE_API_KEY:-}" ]; then
            log_success "Clé API chargée depuis .env"
            USE_API=true
            return 0
        fi
    fi

    if [ -n "${CURSEFORGE_API_KEY:-}" ]; then
        log_success "Clé API trouvée dans les variables d'environnement"
        USE_API=true
        return 0
    fi

    log_warning "Clé API CurseForge non trouvée"
    log_info "Le script utilisera uniquement les URLs du manifest"
    USE_API=false
    return 1
}

#############################################
# Fonctions de cache
#############################################

init_cache() {
    if [ ! -f "$CACHE_FILE" ] || [ "$FORCE_REFRESH" = true ]; then
        log_info "Initialisation du cache..."
        echo '{
  "cache_version": "1.0",
  "hytale_game_id": 70216,
  "last_updated": "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'",
  "cache_validity_days": '$CACHE_VALIDITY_DAYS',
  "highest_offset_searched": 0,
  "mods": {}
}' > "$CACHE_FILE"
    fi
}

get_from_cache() {
    local mod_name="$1"

    if [ ! -f "$CACHE_FILE" ]; then
        echo ""
        return 1
    fi

    # Chercher le mod dans le cache
    local cached_data=$(jq -r --arg name "$mod_name" '.mods[$name] // empty' "$CACHE_FILE" 2>/dev/null)

    if [ -n "$cached_data" ] && [ "$cached_data" != "null" ]; then
        # Vérifier si le cache est encore valide
        local cached_at=$(echo "$cached_data" | jq -r '.cached_at // empty')
        if [ -n "$cached_at" ]; then
            local cache_age_days=$(( ($(date +%s) - $(date -d "$cached_at" +%s 2>/dev/null || echo 0)) / 86400 ))

            if [ "$cache_age_days" -lt "$CACHE_VALIDITY_DAYS" ] && [ "$FORCE_REFRESH" = false ]; then
                echo "$cached_data"
                return 0
            fi
        fi
    fi

    echo ""
    return 1
}

save_to_cache() {
    local mod_name="$1"
    local mod_data="$2"

    # Ajouter la date de cache
    local enriched_data=$(echo "$mod_data" | jq --arg cached_at "$(date -u +"%Y-%m-%dT%H:%M:%SZ")" '. + {cached_at: $cached_at}')

    # Sauvegarder dans le cache
    local temp_cache=$(mktemp)
    jq --arg name "$mod_name" --argjson data "$enriched_data" '.mods[$name] = $data | .last_updated = "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'"' "$CACHE_FILE" > "$temp_cache"
    mv "$temp_cache" "$CACHE_FILE"
}

update_highest_offset() {
    local offset="$1"

    local temp_cache=$(mktemp)
    jq --arg offset "$offset" '.highest_offset_searched = ($offset | tonumber)' "$CACHE_FILE" > "$temp_cache"
    mv "$temp_cache" "$CACHE_FILE"
}

#############################################
# Fonctions de matching
#############################################

slugify() {
    local text="$1"
    echo "$text" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/-/g' | sed 's/--*/-/g' | sed 's/^-//' | sed 's/-$//'
}

# Normalise un nom pour le matching (enlève tout sauf lettres et chiffres, lowercase)
normalize_for_match() {
    local text="$1"
    echo "$text" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]//g'
}

calculate_similarity() {
    local str1="$1"
    local str2="$2"

    # Convertir en minuscules
    str1=$(echo "$str1" | tr '[:upper:]' '[:lower:]')
    str2=$(echo "$str2" | tr '[:upper:]' '[:lower:]')

    # Si identiques
    if [ "$str1" = "$str2" ]; then
        echo "100"
        return
    fi

    # Vérifier si l'un contient l'autre
    if [[ "$str1" == *"$str2"* ]] || [[ "$str2" == *"$str1"* ]]; then
        echo "80"
        return
    fi

    # Sinon, similarité faible
    echo "30"
}

match_mod() {
    local local_mod_name="$1"
    local local_mod_authors="$2"
    local cf_mod_json="$3"

    local cf_name=$(echo "$cf_mod_json" | jq -r '.name')
    local cf_slug=$(echo "$cf_mod_json" | jq -r '.slug')
    local cf_authors=$(echo "$cf_mod_json" | jq -r '.authors[].name' | tr '\n' ',' | sed 's/,$//')

    local score=0

    # Comparaison du nom
    local name_similarity=$(calculate_similarity "$local_mod_name" "$cf_name")
    score=$((score + name_similarity))

    # Comparaison du slug
    local local_slug=$(slugify "$local_mod_name")
    if [ "$local_slug" = "$cf_slug" ]; then
        score=$((score + 50))
    fi

    # Comparaison des auteurs (si disponibles)
    if [ -n "$local_mod_authors" ] && [ "$local_mod_authors" != "Unknown" ]; then
        if [[ "$cf_authors" == *"$local_mod_authors"* ]]; then
            score=$((score + 50))
        fi
    fi

    # Seuil de correspondance
    if [ $score -ge 120 ]; then
        echo "true"
    else
        echo "false"
    fi
}

#############################################
# Fonctions API CurseForge
#############################################

api_request() {
    local endpoint="$1"
    local api_key_file="$SCRIPT_DIR/.api_key"
    local header_file=$(mktemp)

    # Créer le fichier header avec la clé API (évite l'interprétation shell des $)
    printf 'header = "x-api-key: ' > "$header_file"
    cat "$api_key_file" >> "$header_file"
    printf '"\n' >> "$header_file"

    local response
    response=$(curl -s -H "Accept: application/json" -K "$header_file" "$API_BASE_URL$endpoint")

    rm -f "$header_file"

    # Rate limiting (sleep 0.35s pour ~3 requêtes/seconde)
    sleep 0.35

    echo "$response"
}

search_mods_via_api() {
    local mods_info_dir="$1"

    log_api "🔍 Recherche des URLs CurseForge via l'API..."

    # Créer les fichiers de travail
    local mods_map_file=$(mktemp)
    local authors_file=$(mktemp)
    local total_to_find=0

    # Phase 1: Collecter les mods à rechercher et leurs auteurs
    for json_file in "$mods_info_dir"/*.json; do
        [ -f "$json_file" ] || continue

        local mod_data=$(jq -c '{name: .name, website: .website, authors: .authors}' "$json_file" 2>/dev/null)
        local mod_name=$(echo "$mod_data" | jq -r '.name // "N/A"')
        local website=$(echo "$mod_data" | jq -r '.website // ""')
        local author=$(echo "$mod_data" | jq -r '.authors // "Unknown"')

        # Si pas d'URL CurseForge dans le manifest
        if [[ "$website" != *"curseforge.com/hytale/mods/"* ]] && [[ "$website" != *"legacy.curseforge.com/hytale/mods/"* ]]; then
            # Vérifier le cache
            local cached=$(get_from_cache "$mod_name")
            if [ -n "$cached" ]; then
                local cf_url=$(echo "$cached" | jq -r '.curseforge_url // empty')
                if [ -n "$cf_url" ]; then
                    log_success "  ✓ $mod_name (cache) → $cf_url"
                    local temp_json=$(mktemp)
                    jq --arg url "$cf_url" '. + {curseforge_url: $url, found_via: "cache"}' "$json_file" > "$temp_json"
                    mv "$temp_json" "$json_file"
                    continue
                fi
            fi

            # Stocker le mod avec son auteur
            local slug=$(slugify "$mod_name")
            echo "${mod_name}@@@${json_file}@@@${slug}@@@${author}" >> "$mods_map_file"
            echo "$author" >> "$authors_file"
            ((total_to_find++))
        fi
    done

    if [ $total_to_find -eq 0 ]; then
        log_success "Tous les mods ont déjà une URL CurseForge !"
        rm -f "$mods_map_file" "$authors_file"
        return 0
    fi

    log_api "Mods à rechercher : $total_to_find"

    # Obtenir la liste des auteurs uniques
    local unique_authors=$(sort -u "$authors_file" | grep -v "Unknown" | grep -v "^$")
    local num_authors=$(echo "$unique_authors" | grep -c "." || echo "0")
    log_api "Auteurs uniques : $num_authors"
    echo ""

    local total_found=0
    local remaining=$total_to_find

    # Phase 2: Recherche par auteur (plus précise)
    log_api "📋 Stratégie 1: Recherche par auteur..."
    echo ""

    while IFS= read -r author; do
        [ -z "$author" ] && continue
        [ $remaining -eq 0 ] && break

        # Chercher les mods de cet auteur sur CurseForge
        log_info "  Recherche des mods de '$author'..."

        local cf_mods_file=$(mktemp)
        local api_response=$(api_request "/mods/search?gameId=$HYTALE_GAME_ID&searchFilter=$(echo "$author" | sed 's/ /%20/g')&pageSize=50")
        echo "$api_response" | jq -r '.data[]? | select(.links.websiteUrl | contains("/mods/")) | "\(.name)@@@\(.slug)@@@\(.id)@@@\(.links.websiteUrl)@@@\(.authors[0].name // "")"' 2>/dev/null > "$cf_mods_file"

        # Matcher les mods locaux de cet auteur (utiliser awk car IFS ne gère pas les séparateurs multi-caractères)
        while IFS= read -r map_line; do
            [ -z "$map_line" ] && continue

            local mod_name=$(echo "$map_line" | awk -F'@@@' '{print $1}')
            local json_file=$(echo "$map_line" | awk -F'@@@' '{print $2}')
            local local_slug=$(echo "$map_line" | awk -F'@@@' '{print $3}')
            local mod_author=$(echo "$map_line" | awk -F'@@@' '{print $4}')

            [ -z "$mod_name" ] && continue
            [[ "$mod_author" != *"$author"* ]] && continue

            local normalized_local=$(normalize_for_match "$mod_name")

            # Chercher dans les résultats de cet auteur
            while IFS= read -r line; do
                local cf_name=$(echo "$line" | awk -F'@@@' '{print $1}')
                local cf_slug=$(echo "$line" | awk -F'@@@' '{print $2}')
                local cf_id=$(echo "$line" | awk -F'@@@' '{print $3}')
                local cf_url=$(echo "$line" | awk -F'@@@' '{print $4}')
                local cf_author=$(echo "$line" | awk -F'@@@' '{print $5}')

                # Vérifier que l'auteur correspond
                [[ "$cf_author" != *"$author"* ]] && continue

                local normalized_cf=$(normalize_for_match "$cf_name")

                # Match exact ou par sous-chaîne (avec taille minimale)
                if [[ "$normalized_cf" == "$normalized_local" ]] || \
                   [[ "$cf_slug" == "$local_slug" ]] || \
                   { [ ${#normalized_cf} -ge 5 ] && [[ "$normalized_cf" == *"$normalized_local"* ]]; } || \
                   { [ ${#normalized_local} -ge 5 ] && [[ "$normalized_local" == *"$normalized_cf"* ]]; }; then

                    log_success "  ✓ $mod_name → $cf_url"

                    # Sauvegarder
                    local temp_json=$(mktemp)
                    jq --arg url "$cf_url" '. + {curseforge_url: $url, found_via: "api_author"}' "$json_file" > "$temp_json"
                    mv "$temp_json" "$json_file"

                    save_to_cache "$mod_name" "{\"curseforge_url\": \"$cf_url\", \"curseforge_id\": $cf_id, \"slug\": \"$cf_slug\", \"found_via\": \"author_search\"}"

                    # Retirer de la liste à chercher
                    sed -i "/^${mod_name}@@@/d" "$mods_map_file" 2>/dev/null

                    ((total_found++))
                    ((remaining--))
                    break
                fi
            done < "$cf_mods_file"
        done < "$mods_map_file"

        rm -f "$cf_mods_file"
    done <<< "$unique_authors"

    echo ""
    log_info "Après recherche par auteur : $total_found trouvé(s), $remaining restant(s)"
    echo ""

    # Phase 3: Recherche globale par meta-batches (pour les mods restants)
    if [ $remaining -eq 0 ]; then
        log_success "✅ Tous les mods trouvés via la recherche par auteur !"
        rm -f "$mods_map_file" "$authors_file"
        return 0
    fi

    log_api "📋 Stratégie 2: Recherche globale par batches..."
    echo ""

    local current_offset=0
    local meta_batch_number=1

    while [ $remaining -gt 0 ] && [ $current_offset -lt $MAX_API_LIMIT ]; do
        local meta_batch_end=$((current_offset + META_BATCH_SIZE))

        echo -e "${MAGENTA}🔍 Batch $meta_batch_number${NC} (mods CurseForge $current_offset-$meta_batch_end)"
        log_info "   Mods restants à trouver : $remaining"

        local found_in_this_meta_batch=0
        local sub_batch=0

        while [ $current_offset -lt $meta_batch_end ] && [ $current_offset -lt $MAX_API_LIMIT ] && [ $remaining -gt 0 ]; do
            ((sub_batch++))

            printf "\r   └─ Requête API %d/%d (mods %d-%d)..." "$sub_batch" "$((META_BATCH_SIZE / BATCH_SIZE))" "$current_offset" "$((current_offset + BATCH_SIZE))" >&2

            # Requête API - index est l'offset (position de départ), pas le numéro de page
            local api_response_file=$(mktemp)
            api_request "/mods/search?gameId=$HYTALE_GAME_ID&pageSize=$BATCH_SIZE&index=$current_offset" > "$api_response_file"

            # Vérifier la pagination pour savoir si on a atteint la fin
            local total_count=$(jq -r '.pagination.totalCount // 0' "$api_response_file" 2>/dev/null)
            local result_count=$(jq -r '.pagination.resultCount // 0' "$api_response_file" 2>/dev/null)

            # Extraire les données en une seule passe jq (séparateur @@@ et filtrer uniquement les mods)
            local cf_data_file=$(mktemp)
            jq -r '.data[]? | select(.links.websiteUrl | contains("/mods/")) | "\(.name)@@@\(.slug)@@@\(.id)@@@\(.links.websiteUrl)"' "$api_response_file" 2>/dev/null > "$cf_data_file"
            rm -f "$api_response_file"

            if [ ! -s "$cf_data_file" ] || [ "$result_count" -eq 0 ]; then
                echo "" >&2
                log_info "Fin des mods CurseForge atteinte (total: $total_count)"
                rm -f "$cf_data_file"
                break 2
            fi

            # Matcher avec plusieurs stratégies (utiliser awk car IFS ne gère pas les séparateurs multi-caractères)
            while IFS= read -r map_line; do
                [ -z "$map_line" ] && continue

                local mod_name=$(echo "$map_line" | awk -F'@@@' '{print $1}')
                local json_file=$(echo "$map_line" | awk -F'@@@' '{print $2}')
                local local_slug=$(echo "$map_line" | awk -F'@@@' '{print $3}')

                [ -z "$local_slug" ] && continue

                local match=""
                local normalized_local=$(normalize_for_match "$mod_name")

                # Stratégie 1: Match exact du nom (insensible à la casse)
                match=$(grep -i "^${mod_name}@@@" "$cf_data_file" 2>/dev/null | head -1)

                # Stratégie 2: Match exact du slug
                [ -z "$match" ] && match=$(grep -i "@@@${local_slug}@@@" "$cf_data_file" 2>/dev/null | head -1)

                # Stratégie 3: Le nom local est contenu dans le nom CF ou vice versa (avec longueur minimale)
                if [ -z "$match" ]; then
                    while IFS= read -r line; do
                        local cf_name=$(echo "$line" | awk -F'@@@' '{print $1}')
                        local cf_slug=$(echo "$line" | awk -F'@@@' '{print $2}')

                        local normalized_cf=$(normalize_for_match "$cf_name")

                        # Vérifier avec longueur minimale pour éviter les faux positifs
                        if [ ${#normalized_cf} -ge 5 ] && [ ${#normalized_local} -ge 5 ]; then
                            if [[ "$normalized_cf" == *"$normalized_local"* ]] || [[ "$normalized_local" == *"$normalized_cf"* ]]; then
                                match="$line"
                                break
                            fi
                            # Vérifier aussi le slug normalisé
                            local normalized_cf_slug=$(normalize_for_match "$cf_slug")
                            if [[ "$normalized_cf_slug" == *"$normalized_local"* ]] || [[ "$normalized_local" == *"$normalized_cf_slug"* ]]; then
                                match="$line"
                                break
                            fi
                        fi
                    done < "$cf_data_file"
                fi

                if [ -n "$match" ]; then
                    local cf_name=$(echo "$match" | awk -F'@@@' '{print $1}')
                    local cf_slug=$(echo "$match" | awk -F'@@@' '{print $2}')
                    local cf_id=$(echo "$match" | awk -F'@@@' '{print $3}')
                    local cf_url=$(echo "$match" | awk -F'@@@' '{print $4}')

                    echo "" >&2
                    log_success "  ✓ $mod_name → $cf_url"

                    # Sauvegarder dans le fichier JSON
                    local temp_json=$(mktemp)
                    jq --arg url "$cf_url" '. + {curseforge_url: $url, found_via: "api"}' "$json_file" > "$temp_json"
                    mv "$temp_json" "$json_file"

                    # Sauvegarder dans le cache
                    save_to_cache "$mod_name" "{\"curseforge_url\": \"$cf_url\", \"curseforge_id\": $cf_id, \"slug\": \"$cf_slug\", \"found_at_offset\": $current_offset}"

                    # Marquer comme trouvé dans le fichier map (utiliser @@@ comme séparateur)
                    sed -i "/^${mod_name}@@@/d" "$mods_map_file" 2>/dev/null

                    ((found_in_this_meta_batch++))
                    ((total_found++))
                    ((remaining--))
                fi
            done < "$mods_map_file"

            rm -f "$cf_data_file"

            if [ $remaining -eq 0 ]; then
                echo "" >&2
                log_success "✅ Tous les mods trouvés !"
                break 2
            fi

            current_offset=$((current_offset + BATCH_SIZE))

            # Arrêter si on a parcouru tous les mods disponibles
            if [ "$total_count" -gt 0 ] && [ $current_offset -ge $total_count ]; then
                echo "" >&2
                log_info "Tous les mods CurseForge parcourus ($total_count mods)"
                break 2
            fi
        done

        echo "" >&2
        log_info "Bilan batch $meta_batch_number : $found_in_this_meta_batch mod(s) trouvé(s) - $remaining restant(s)"

        update_highest_offset "$current_offset"
        ((meta_batch_number++))
        echo ""

        if [ $remaining -gt 0 ]; then
            log_api "⏸️  Pause de 1 seconde avant le batch suivant..."
            sleep 1
        fi
    done

    echo ""
    log_success "Recherche terminée : $total_found mod(s) trouvé(s) via l'API"

    if [ $remaining -gt 0 ]; then
        local not_found=$(awk -F'@@@' '{print $1}' "$mods_map_file" | tr '\n' ' ')
        log_warning "Mods non trouvés ($remaining) : $not_found"

        while IFS= read -r map_line; do
            local mod_name=$(echo "$map_line" | awk -F'@@@' '{print $1}')
            save_to_cache "$mod_name" "{\"not_found\": true, \"searched_up_to_offset\": $current_offset}"
        done < "$mods_map_file"
    fi

    rm -f "$mods_map_file" "$authors_file"
}

#############################################
# Fonctions principales
#############################################

extract_manifest() {
    local mod_file="$1"
    local temp_manifest="/tmp/manifest_$$.json"

    if unzip -p "$mod_file" manifest.json > "$temp_manifest" 2>/dev/null; then
        echo "$temp_manifest"
        return 0
    else
        rm -f "$temp_manifest"
        return 1
    fi
}

parse_manifest() {
    local manifest_file="$1"
    local output_file="$2"

    # Utilisation de jq pour parser le JSON (parenthèses nécessaires pour jq 1.6)
    jq -c '{
        name: ((.Name) // "N/A"),
        version: ((.Version) // "N/A"),
        description: ((.Description) // ""),
        website: ((.Website) // ""),
        authors: (([.Authors[]?.Name] | map(select(. != null)) | join(", ")) // "Unknown")
    }' "$manifest_file" 2>/dev/null > "$output_file" || echo '{}' > "$output_file"
}

process_mods() {
    log_info "Recherche des mods dans $MODS_DIR..."

    # Créer le dossier temporaire
    mkdir -p "$TEMP_DATA_DIR"

    # Créer un tableau avec tous les mods
    local mod_files=("$MODS_DIR"/*.jar "$MODS_DIR"/*.zip)

    # Filtrer les fichiers inexistants (quand pas de *.jar ou *.zip)
    local valid_mods=()
    for f in "${mod_files[@]}"; do
        [ -f "$f" ] && valid_mods+=("$f")
    done

    if [ ${#valid_mods[@]} -eq 0 ]; then
        log_error "Aucun mod trouvé dans $MODS_DIR"
        exit 1
    fi

    local total_mods=${#valid_mods[@]}
    log_info "Trouvé $total_mods mod(s)"

    local count=0
    for mod_file in "${valid_mods[@]}"; do
        ((count++))
        local basename_file=$(basename "$mod_file")
        printf "\rTraitement... %d/%d : %-50s" "$count" "$total_mods" "$basename_file" >&2

        local manifest_path
        if manifest_path=$(extract_manifest "$mod_file"); then
            local mod_info_file="$TEMP_DATA_DIR/${count}.json"
            parse_manifest "$manifest_path" "$mod_info_file"
            rm -f "$manifest_path"
        else
            echo "" >&2
            log_warning "Pas de manifest.json dans $basename_file" >&2
        fi
    done

    echo "" >&2 # Nouvelle ligne après la barre de progression

    # Compter les mods valides
    local valid_count=$(ls -1 "$TEMP_DATA_DIR"/*.json 2>/dev/null | wc -l)
    log_success "Traitement terminé : $valid_count mod(s) valide(s)"

    echo ""

    # Recherche via API si clé disponible (et pas en mode dry-run)
    if [ "$USE_API" = true ] && [ "$DRY_RUN" = false ]; then
        search_mods_via_api "$TEMP_DATA_DIR"
        echo ""
    elif [ "$USE_API" = true ] && [ "$DRY_RUN" = true ]; then
        log_info "Mode dry-run : appels API ignorés"
    fi

    # Génération du fichier Markdown
    generate_markdown
}

generate_markdown() {
    log_info "Génération du fichier Markdown..."

    # Trier les fichiers JSON par nom de mod
    local temp_sorted="$TEMP_DATA_DIR/sorted_list.txt"
    for json_file in "$TEMP_DATA_DIR"/*.json; do
        [ -f "$json_file" ] || continue
        local mod_name=$(jq -r '.name' "$json_file" 2>/dev/null || echo "Unknown")
        echo "$mod_name|$json_file"
    done | sort | cut -d'|' -f2 > "$temp_sorted"

    if [ "$DRY_RUN" = true ]; then
        log_info "Mode dry-run activé, affichage du résultat sur stdout"
        local OUTPUT_TARGET="/dev/stdout"
    else
        local OUTPUT_TARGET="$OUTPUT_FILE"
    fi

    {
        echo "# 📦 Liste des Mods Hytale"
        echo ""
        echo "**Date de génération :** $(date '+%Y-%m-%d %H:%M:%S')"
        echo "**Nombre total de mods :** $(wc -l < "$temp_sorted")"
        echo ""
        echo "---"
        echo ""

        while IFS= read -r json_file; do
            [ -f "$json_file" ] || continue

            local mod_name=$(jq -r '.name // "N/A"' "$json_file")
            local version=$(jq -r '.version // "N/A"' "$json_file")
            local description=$(jq -r '.description // ""' "$json_file")
            local authors=$(jq -r '.authors // "Unknown"' "$json_file")
            local website=$(jq -r '.website // ""' "$json_file")
            local curseforge_url=$(jq -r '.curseforge_url // ""' "$json_file")
            local found_via=$(jq -r '.found_via // ""' "$json_file")

            echo "## $mod_name"
            echo "**Version :** $version"
            echo "**Auteur(s) :** $authors"
            [ -n "$description" ] && echo "**Description :** $description"

            # Vérification de l'URL CurseForge
            if [ -n "$curseforge_url" ] && [ "$curseforge_url" != "null" ]; then
                # URL trouvée via API
                if [ "$found_via" = "api" ]; then
                    echo "**URL :** [$curseforge_url]($curseforge_url) 🔍"
                elif [ "$found_via" = "cache" ]; then
                    echo "**URL :** [$curseforge_url]($curseforge_url) 🔍"
                else
                    # URL du manifest
                    echo "**URL :** [$curseforge_url]($curseforge_url) 🔗"
                fi
            elif [[ "$website" == *"curseforge.com/hytale/mods/"* ]]; then
                echo "**URL :** [$website]($website) 🔗"
            elif [[ "$website" == *"legacy.curseforge.com/hytale/mods/"* ]]; then
                echo "**URL :** [$website]($website) 🔗"
            elif [ -n "$website" ] && [ "$website" != "null" ] && [ "$website" != "" ] && [ "$website" != "website" ]; then
                echo "**URL :** Non trouvée via CurseForge ❌"
                echo "**Site alternatif :** [$website]($website)"
            else
                echo "**URL :** Non trouvée ❌"
            fi

            echo ""
            echo "---"
            echo ""
        done < "$temp_sorted"

        echo "*Légende :*"
        echo "🔗 = URL trouvée dans le manifest"
        echo "🔍 = URL trouvée via l'API CurseForge"
        echo "❌ = URL non trouvée"
        echo ""
        echo "---"
        echo ""
        echo "*Généré automatiquement par list_mods.sh*"

    } > "$OUTPUT_TARGET"

    if [ "$DRY_RUN" = false ]; then
        log_success "Fichier généré : $OUTPUT_FILE"
    fi
}

#############################################
# Main
#############################################

main() {
    # Parse des arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --help)
                print_help
                exit 0
                ;;
            --install-deps)
                install_dependencies
                exit $?
                ;;
            --dry-run)
                DRY_RUN=true
                log_info "Mode dry-run activé"
                shift
                ;;
            --force-refresh)
                FORCE_REFRESH=true
                log_info "Force refresh activé"
                shift
                ;;
            *)
                log_error "Option inconnue : $1"
                print_help
                exit 1
                ;;
        esac
    done

    # Vérification des dépendances
    if ! check_dependencies; then
        exit 1
    fi

    # Chargement de la clé API (optionnel)
    set +u
    load_env || true
    set -u

    # Initialiser le cache si l'API est disponible
    if [ "$USE_API" = true ]; then
        init_cache
    fi

    # Vérification du dossier mods
    if [ ! -d "$MODS_DIR" ]; then
        log_error "Le dossier 'mods' n'existe pas : $MODS_DIR"
        exit 1
    fi

    # Traitement des mods
    process_mods

    log_success "Terminé !"
}

main "$@"
