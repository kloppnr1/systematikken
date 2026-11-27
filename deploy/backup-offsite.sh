#!/bin/sh
# Copies the nightly database backups and the cookie keys to a Hetzner Storage Box.
# Run from the deploy user's crontab, after the app's own backup (which runs at start-up and every 24 hours):
#   30 3 * * * /opt/systematikken/backup-offsite.sh >> /opt/systematikken/backup.log 2>&1
# Needs STORAGE_BOX in /opt/systematikken/backup.env, e.g. STORAGE_BOX=u123456@u123456.your-storagebox.de
# and the deploy user's SSH key added to the Storage Box.
set -eu
. /opt/systematikken/backup.env

rsync -az --delete -e "ssh -p 23 -o BatchMode=yes" \
  /opt/systematikken/data/backups/ "$STORAGE_BOX:systematikken/backups/"
rsync -az -e "ssh -p 23 -o BatchMode=yes" \
  /opt/systematikken/data/keys/ "$STORAGE_BOX:systematikken/keys/"

# Tell the outside world the backup ran. A missing ping means something is wrong.
if [ -n "${HEALTHCHECK_URL:-}" ]; then
  wget -q -O /dev/null "$HEALTHCHECK_URL" || true
fi
echo "$(date -Is) offsite backup ok"
