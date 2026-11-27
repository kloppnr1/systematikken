# Systematikken

Free, low-maintenance software for Danish private daycare institutions: children and payers, waiting list with public sign-up, monthly billing through Betalingsservice, bookkeeping voucher, and an overview of places month by month.

- App: https://app.systematikken.dk
- Demo with made-up data: https://demo.systematikken.dk

## Try it locally

```
dotnet run --project src/Systematikken.Api    # API on http://localhost:5180
cd web && npm install && npm run dev          # app on http://localhost:5173
```

In development you log in with any e-mail address; no Google or Microsoft setup is needed.

## Layout

| Folder | What |
|---|---|
| `src/Systematikken.Billing` | Pure rules with no database or UI: the fee engine, Betalingsservice files, places report, bookkeeping voucher |
| `src/Systematikken.Api` | ASP.NET Core API on .NET 10, SQLite through EF Core, login with Google/Microsoft. Serves the built React app |
| `web` | React + TypeScript app built with Vite |
| `tests` | Rule tests, and flow tests that run the real API |
| `deploy` | Docker Compose, Caddy and the server setup for Hetzner |

## Specs

- [Fee rules](docs/fee-rules.md)
- [Places report](docs/occupancy-report.md)
- [Bookkeeping voucher](docs/accounting-voucher.md)
- [Betalingsservice files](docs/betalingsservice.md)
- [Running it](docs/operations.md)

## License

[AGPL-3.0](LICENSE). You may use, change and share Systematikken freely. If you run a changed version as a service for others, you must publish your changes under the same license.
