# poc-exchange-callback

Small ASP.NET Core 8 API that Microsoft Graph can call when a calendar event is created, updated, or deleted on `admin@harmoniousflair.com.au`.

```text
Outlook / Exchange  →  Microsoft Graph webhook  →  this API
```

## Local configuration

Copy `secrets.json.example` to `secrets.json` in the **repository root** (same folder as the `.sln`) **or** next to the project (`src/Poc.Exchange.Callback/secrets.json`). The API loads `secrets.json` from the content root when the file exists. If it does not exist, it reads `Graph__*` environment variables instead.

`secrets.json` is gitignored. Do not commit it.

```json
{
  "Graph": {
    "TenantId": "...",
    "ClientId": "...",
    "ClientSecret": "...",
    "Mailbox": "admin@harmoniousflair.com.au",
    "ClientState": "a-random-guid",
    "NotificationUrl": "https://YOUR_PUBLIC_HTTPS/api/graph/notifications"
  }
}
```

Environment variable names (used on Azure):

- `Graph__TenantId`
- `Graph__ClientId`
- `Graph__ClientSecret`
- `Graph__Mailbox`
- `Graph__ClientState`
- `Graph__NotificationUrl`

## Entra app (one-time)

In your harmoniousflair tenant:

1. App registration → new app.
2. Certificates & secrets → client secret.
3. API permissions → Microsoft Graph **application** permission `Calendars.Read` (add `Calendars.ReadWrite` if you later create events). Grant admin consent.
4. Put tenant id, client id, and secret into `secrets.json`.

## Run locally

Graph cannot call `http://localhost`. Use a public HTTPS tunnel (Dev Tunnels, ngrok) and set `Graph:NotificationUrl` to `https://<tunnel>/api/graph/notifications`.

```bash
dotnet test
dotnet run --project src/Poc.Exchange.Callback
```

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/` | Home page |
| GET | `/notifications` or `/notifications.html` | Notification history UI (filter and sort) |
| GET | `/subscriptions` or `/subscriptions.html` | Create a Graph subscription for a mailbox and view the API result |
| GET | `/health` | Liveness |
| GET/POST | `/api/graph/notifications` | Graph validation + change notifications |
| GET | `/api/notifications` | Last notifications this process received |
| GET | `/api/settings` | Default mailbox and notification URL (no secrets) |
| GET | `/api/subscriptions` | List Graph subscriptions |
| POST | `/api/subscriptions` | Create a Graph subscription. Body: `{ "mailbox": "user@example.com" }` |

After the public URL is live:

```bash
curl -X POST https://<host>/api/subscriptions
```

Then create a meeting on `admin@harmoniousflair.com.au` in Outlook. Check `GET /api/notifications`.

Subscriptions expire in about three days. Call `POST /api/subscriptions` again to renew.

## GitHub Actions / Azure Free (F1)

The workflow creates resource group `rg-poc-exchange-callback` in **Australia East**, an **F1 Linux** App Service plan, and web app `pocxchcb-hemantshelar` (`DOTNETCORE:8.0`).

Add these **GitHub Actions secrets** on `hemantshelar/poc-exchange-callback`:

| Secret | Value |
| --- | --- |
| `AZURE_CREDENTIALS` | JSON from an Azure service principal (see below) |
| `GRAPH_TENANT_ID` | Entra tenant id |
| `GRAPH_CLIENT_ID` | App registration client id |
| `GRAPH_CLIENT_SECRET` | App client secret |
| `GRAPH_MAILBOX` | `admin@harmoniousflair.com.au` |
| `GRAPH_CLIENT_STATE` | Same random string you use as `ClientState` |

Create the service principal (Azure CLI, your personal subscription):

```bash
az login
az ad sp create-for-rbac --name poc-exchange-callback-gha --role contributor --scopes /subscriptions/<SUBSCRIPTION_ID> --sdk-auth
```

Paste the JSON output into `AZURE_CREDENTIALS`.

**F1 limitation:** the free plan sleeps. Graph validation and webhooks fail if the site is cold. Hit `/health` first, then create the subscription.

If Linux F1 is not offered in the region, say so and the plan can be switched to Windows F1 or another region.

## After the first deploy

1. Confirm `https://pocxchcb-hemantshelar.azurewebsites.net/health`.
2. `POST https://pocxchcb-hemantshelar.azurewebsites.net/api/subscriptions`.
3. Add a calendar event on `admin@harmoniousflair.com.au`.
4. `GET https://pocxchcb-hemantshelar.azurewebsites.net/api/notifications`.
