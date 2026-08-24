#!/usr/bin/env bash
set -euo pipefail

DEVICE_ID="${1:-kitchen-01}"
HOURS="${2:-24}"
STEP_MINUTES="${3:-5}"
TOPIC="thermometrum/${DEVICE_ID}/reading"

if command -v mosquitto_pub >/dev/null 2>&1; then
    publish() { mosquitto_pub -h localhost -p 1883 -t "$TOPIC" -m "$1"; }
else
    publish() { docker compose exec -T mosquitto mosquitto_pub -h localhost -p 1883 -t "$TOPIC" -m "$1"; }
fi

total_minutes=$((HOURS * 60))
count=0

for ((minutes_ago = total_minutes; minutes_ago >= 0; minutes_ago -= STEP_MINUTES)); do
    timestamp=$(date -u -d "-${minutes_ago} minutes" +%Y-%m-%dT%H:%M:%SZ)
    payload=$(awk -v m="$minutes_ago" -v t="$total_minutes" -v ts="$timestamp" 'BEGIN {
        phase = m / t * 2 * 3.14159265
        printf "{\"temperature\":%.2f,\"humidity\":%.2f,\"ts\":\"%s\"}", 21.5 + 2.5 * sin(phase), 47 + 6 * cos(phase), ts
    }')
    publish "$payload"
    count=$((count + 1))
done

echo "published ${count} readings for ${DEVICE_ID} over ${HOURS}h"
