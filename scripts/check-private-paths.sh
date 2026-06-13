#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

MAC_HOME_PREFIX=$'\x2f\x55\x73\x65\x72\x73\x2f'
PRIVATE_USER_TOKEN=$'\x6a\x6f\x73\x65\x70\x68\x66\x65\x73\x65\x72'
PRIVATE_PROJECT_TOKEN=$'\x46\x46\x50'
PRIVATE_APP_TOKEN=$'\x46\x46\x50\x52\x75\x6e\x6e\x69\x6e\x67\x43\x6c\x75\x62'

patterns=(
  "${MAC_HOME_PREFIX}"'[^[:space:]`"'"'"']+'
  "$PRIVATE_USER_TOKEN"
  "$PRIVATE_PROJECT_TOKEN"
  "$PRIVATE_APP_TOKEN"
)

excluded_paths=(
  ':!scripts/check-private-paths.sh'
)

has_matches=0

for pattern in "${patterns[@]}"; do
  if matches="$(git grep -n -I -E "$pattern" -- . "${excluded_paths[@]}" || true)" && [[ -n "$matches" ]]; then
    has_matches=1
    printf '%s\n' "$matches"
  fi
done

if [[ "$has_matches" -ne 0 ]]; then
  cat >&2 <<'EOF'

Private path guard failed.

Replace machine-local paths and private fixture names with generic placeholders such as:
- <external-sample-repos>
- <private-client-app>
- <private-server-root>

If a local-only helper needs private paths, keep it ignored and untracked.
EOF
  exit 1
fi

echo "Private path guard passed."
