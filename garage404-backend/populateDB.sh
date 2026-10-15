#!/usr/bin/env bash
# GARAGE404_API_TOKEN="admin-token" ./garage404-backend/populateDB.sh

set -euo pipefail

: "${GARAGE404_API_TOKEN:?Set GARAGE404_API_TOKEN to an admin Firebase bearer token}"


curl --fail --silent --show-error \
  --request POST "http://localhost:5083/v1/ServiceType" \
  --header "Authorization: Bearer $GARAGE404_API_TOKEN" \
  --header "Accept: application/json" \
  --header "Content-Type: application/json" \
  --data '[
    {"Type": 0, "Name": "Small Service", "Slug": "small-service"},
    {"Type": 1, "Name": "Big Service", "Slug": "big-service"},
    {"Type": 2, "Name": "Tyre Change", "Slug": "tyre-change"},
    {"Type": 3, "Name": "Expertise Preparation", "Slug": "expertise-preparation"},
    {"Type": 4, "Name": "Winter Storage", "Slug": "winter-storage"},
    {"Type": 5, "Name": "Other", "Slug": "other"}
  ]'

printf '\nService types populated successfully.\n'
