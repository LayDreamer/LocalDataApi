# PMC concurrency deployment

## Required configuration

The repository no longer contains production credentials. Configure these values in the deployment environment:

```text
ConnectionStrings__DefaultConnection
WechatWork__CorpId
WechatWork__AgentSecret
WechatWork__AgentId
WechatWork__RedirectUri
Performance__DatabaseConcurrency   (default: 64)
Performance__DatabaseQueue         (default: 256)
```

Rotate the database password and WeChat Work secret that previously existed in `appsettings.json`; removing them from the current file does not remove them from Git history.

### IIS application pool (`localDataApi`)

Production runs in the `localDataApi` IIS application pool and is deployed to
`D:\IISWebSitefiles`. Do not put production secrets in the deployed
`appsettings.json` or `web.config`.

Run the following from an elevated PowerShell window on the IIS server. The
script prompts for secrets without placing them in the command line and writes
them to the application-pool environment-variable collection in
`applicationHost.config`:

```powershell
.\Deployment\Configure-IisAppPoolEnvironment.ps1
```

Review the application-pool variables, publish the API, and then recycle once
during the agreed deployment window. To configure and recycle in one operation:

```powershell
.\Deployment\Configure-IisAppPoolEnvironment.ps1 -Recycle
```

IIS stores these values under the `localDataApi` application pool in
`%windir%\System32\inetsrv\config\applicationHost.config`. Do not edit that file
by hand or commit a copy of it to source control.

## Database rollout

1. Back up the database and run `DatabaseScripts/20260803_PMC_ConcurrencyAndIndexes.sql` in a staging copy.
2. If the script reports duplicate business keys, reconcile those records and rerun it. Do not bypass the checks.
3. Verify every new index with the actual execution plan and SQL Server index usage statistics.
4. Deploy the API only after all `RowVersion` columns exist. The API model expects those columns.
5. Use the matching rollback script if the application deployment must be reverted.

## API contract change

Large PMC list endpoints now accept `Page` and `PageSize` (`PageSize` defaults to 20 and is capped at 100). Their `Data` value is:

```json
{
  "Items": [],
  "Total": 0,
  "Page": 1,
  "PageSize": 20
}
```

Mutable PMC entities expose `RowVersion` as a Base64 JSON string. Clients must send the last value they read when updating an existing record. A stale value returns HTTP 409 and the client must reload before retrying.

## Capacity validation

Before production rollout, run a 30-minute test with 1,000 virtual users, 2–5 second think time and at most 100 active requests. Monitor API p95 latency, 409/429/500 counts, SQL CPU, lock waits and connection-pool waits. Start with the configured limit of 64; raise it only when the database has measured spare capacity.
