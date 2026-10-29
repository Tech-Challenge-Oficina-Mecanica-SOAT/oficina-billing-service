#!/usr/bin/env bash
# Protege a branch main: PR obrigatório, 1 aprovação, checks verdes e sem push direto/force push.
# Uso:  ./scripts/protect-main.sh <owner>/<repo>     (requer GitHub CLI autenticado como admin do repositório)
set -euo pipefail

REPO="${1:?uso: $0 <owner>/<repo>}"

gh api -X PUT "repos/${REPO}/branches/main/protection" --input - <<JSON
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["build-test-quality", "terraform-validate", "docker-build"]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
JSON

echo "Branch main de ${REPO} protegida."
