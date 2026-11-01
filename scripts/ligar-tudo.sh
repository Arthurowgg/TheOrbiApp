#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Orbi — "liga tudo": publica o site no Firebase Hosting, publica as regras do
# Firestore e configura o GitHub Pages para publicar o build (não o código).
#
# Uso:
#   bash scripts/ligar-tudo.sh            # site no Firebase + Pages
#   bash scripts/ligar-tudo.sh --hosting  # só Firebase
#   bash scripts/ligar-tudo.sh --pages    # só GitHub Pages
#
# Pré-requisitos (uma vez):
#   - Node 20+         (npm ci resolve o resto)
#   - gh autenticado   (gh auth login)  — só para a parte do Pages
# ---------------------------------------------------------------------------
set -euo pipefail

PROJECT="orbi-app-auth"
REPO="Arthurowgg/TheOrbiApp"
HOSTING_URL="https://${PROJECT}.web.app"
PAGES_URL="https://arthurowgg.github.io/TheOrbiApp/"

do_hosting=1
do_pages=1
case "${1:-}" in
  --hosting) do_pages=0 ;;
  --pages)   do_hosting=0 ;;
  "") ;;
  *) echo "Argumento desconhecido: $1"; exit 1 ;;
esac

step() { printf '\n\033[1m▸ %s\033[0m\n' "$1"; }
ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; }
warn() { printf '  \033[33m!\033[0m %s\n' "$1"; }

cd "$(dirname "$0")/.."

# ---------------------------------------------------------------- Firebase
if [ "$do_hosting" = "1" ]; then
  step "Dependências e build do site"
  if [ -f package-lock.json ]; then npm ci --silent; else npm install --silent; fi
  npm run build
  ok "dist/ pronto ($(du -sh dist | cut -f1))"

  step "Login no Firebase (abre o navegador na primeira vez)"
  npx --yes firebase-tools@latest login --reauth >/dev/null 2>&1 || \
    npx --yes firebase-tools@latest login
  ok "logado"

  step "Publicando Hosting + regras do Firestore em $PROJECT"
  npx --yes firebase-tools@latest deploy \
    --only hosting,firestore:rules \
    --project "$PROJECT" --non-interactive
  ok "publicado em $HOSTING_URL"
fi

# ------------------------------------------------------------------- Pages
if [ "$do_pages" = "1" ]; then
  step "GitHub Pages: publicar o build (e não o código-fonte)"
  if ! command -v gh >/dev/null 2>&1; then
    warn "gh (GitHub CLI) não encontrado — faça no navegador:"
    warn "Settings ▸ Pages ▸ Build and deployment ▸ Source = GitHub Actions"
  else
    # Sem isso o Pages serve o index.html do Vite (tela branca).
    if gh api -X PUT "repos/$REPO/pages" -f build_type=workflow >/dev/null 2>&1; then
      ok "origem alterada para GitHub Actions"
    else
      warn "não consegui alterar a origem (token sem permissão de admin)"
      warn "faça no navegador: Settings ▸ Pages ▸ Source = GitHub Actions"
    fi

    gh workflow run pages.yml --repo "$REPO" --ref "$(git rev-parse --abbrev-ref HEAD)" \
      && ok "workflow 'Site no GitHub Pages' disparado"
  fi
  warn "o site aparece em $PAGES_URL em ~1 minuto"
fi

step "Pronto"
echo "  Site Firebase : $HOSTING_URL"
echo "  Site Pages    : $PAGES_URL"
echo "  Instalador    : https://github.com/$REPO/releases/latest"
