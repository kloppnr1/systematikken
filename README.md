# Systematikken

Free, low-maintenance SaaS for Danish private daycare institutions: children and payers, waiting list, public sign-up, monthly billing through Betalingsservice, and the occupancy report.

Written from scratch.

## Layout

| Folder | What |
|---|---|
| `src/Systematikken.Billing` | Pure rules with no database or UI: the fee engine, Betalingsservice files, occupancy report, bookkeeping voucher |
| `src/Systematikken.Api` | ASP.NET Core API on .NET 10, SQLite through EF Core, login with Google/Microsoft. Serves the built React app |
| `web` | React + TypeScript app built with Vite |
| `tests` | Rule tests, and flow tests that run the real API |
| `deploy` | Docker Compose, Caddy and the server setup for Hetzner |

## Specs

- [Fee rules](docs/fee-rules.md)
- [Occupancy report](docs/occupancy-report.md)
- [Bookkeeping voucher](docs/accounting-voucher.md)
- [Betalingsservice files](docs/betalingsservice.md)
- [Running it](docs/operations.md)
