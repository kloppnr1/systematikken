# Running Systematikken

One Hetzner server runs everything: the app container and Caddy in front of it. All data is in one folder, `/opt/systematikken/data`: the SQLite database, the cookie keys and the nightly backups.

## What runs by itself

| What | How |
|---|---|
| Security updates and reboots | Ubuntu unattended-upgrades, reboot at 04:00 when needed |
| HTTPS certificate | Caddy renews it |
| Database backup | The app writes a copy every 24 hours to `data/backups`, keeping 14 |
| Off-site copy | `backup-offsite.sh` from cron copies backups and keys to a Storage Box |
| Deleting old waiting-list entries | The app, daily, after each institution's chosen months |
| Deploy | Every push to `main` that passes the tests is built and deployed |
| Package updates | Dependabot opens one grouped pull request per month |

## What you do by hand

- **Every 2 years:** renew the Microsoft login secret (Entra > App registrations). Put a reminder in your calendar the day you create it.
- **When Dependabot opens a pull request:** merge it if CI is green.
- **Once a quarter:** restore last night's backup on your own machine and open it (see below). An untested backup is not a backup.

## First setup

1. Create a Hetzner Cloud server, Ubuntu 26.04, CX23, Helsinki. Paste `deploy/cloud-init.yaml` as Cloud config. It already holds Martin's SSH key and the deploy key.
2. Point the A records for `systematikken.dk`, `www`, `app` and `demo` at the server. The front page is on `systematikken.dk`, the app on `app.`, the demo on `demo.`.
3. Register the Google and Microsoft logins (see `deploy/env.example` for where and which redirect address).
4. On the server: copy `deploy/env.example` to `/opt/systematikken/.env` and fill it in.
5. Order a Hetzner Storage Box, add the deploy user's SSH key, and write `STORAGE_BOX=...` in `/opt/systematikken/backup.env`. Add the cron line from `backup-offsite.sh`.
6. In the GitHub repo, add secrets: `DEPLOY_HOST` (server IP) and `DEPLOY_SSH_KEY` (the private half of the deploy key, `~/.ssh/systematikken_deploy` on Martin's PC). CI sends the image to the server over SSH, so the server needs no registry login.
7. Push to `main`. CI tests, builds the image, deploys and checks `/health`.

## Restore

1. Stop the app: `docker compose stop app`.
2. Copy a backup over the database: `cp data/backups/systematikken-YYYY-MM-DD-HHMM.db data/systematikken.db`, and remove `data/systematikken.db-wal` and `-shm` if they exist.
3. If the server is new, copy `keys/` back from the Storage Box too. Without the keys everyone is logged out, nothing else is lost.
4. Start it: `docker compose start app`.

To check a backup on your own machine, open it with any SQLite tool and look at `Institutions` and `Children`.

## Running it locally

```
dotnet run --project src/Systematikken.Api    # API on http://localhost:5180
cd web && npm install && npm run dev          # app on http://localhost:5173, forwards /api and /auth to the API
```

In development the e-mail login (`Auth:DevLogin`) is on, so no Google or Microsoft setup is needed.
