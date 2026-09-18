# About StandFast

StandFast is a daily scrum board. You pick a standup and a date, tap people as you spot them in the Teams call, tap them again as they give their update, and capture what they said as markdown alongside whatever they said last time.

It runs as a single Blazor Server container in Azure Container Apps, signs in through OpenID Connect (Kinde), and stores everything in Azure Table Storage.

# Table of Contents

- [About StandFast](#about-standfast)
- [📃 Overview](#-overview)
  - [What it does](#what-it-does)
  - [The daily flow](#the-daily-flow)
- [💻 Technical Overview](#-technical-overview)
  - [Solution layout](#solution-layout)
  - [Running it locally](#running-it-locally)
  - [Configuration](#configuration)
    - [Everything you set](#everything-you-set)
    - [Naming by location](#naming-by-location)
- [📐 Architecture Summary](#-architecture-summary)
  - [Layering](#layering)
  - [Data model and Azure Table Storage](#data-model-and-azure-table-storage)
    - [Tables and keys](#tables-and-keys)
    - [Why Table Storage and not SQL](#why-table-storage-and-not-sql)
  - [Markdown editing](#markdown-editing)
  - [Auditing and logging](#auditing-and-logging)
  - [Backup and restore](#backup-and-restore)
  - [OpenID Connect sign-in](#openid-connect-sign-in)
  - [Azure Container Apps](#azure-container-apps)
    - [What you gain over App Service](#what-you-gain-over-app-service)
    - [What it costs you](#what-it-costs-you)
    - [What bites Blazor Server specifically](#what-bites-blazor-server-specifically)
  - [Deploying](#deploying)
    - [Branch filtering](#branch-filtering)
- [🚧 Change Summary](#-change-summary)

# 📃 Overview

## What it does

- **People** are a flat directory: first name, last name, email, active flag, an optional "display as" override, and markdown notes. When "display as" is set, that is how the person appears everywhere, including the roster and the board; otherwise they appear as first and last name. The People screen also lists which standups each person is on.
- **Standups** are recurring meeting definitions: name, the days they run on, start time, time zone, and a roster of people.
- **The board** is one standup on one date. It opens on today with the current week across the top, and a dropdown picks the standup independently of the date.
- **Backup** downloads everything the app holds as one file and restores it again, replacing whatever is there at the time. The audit log is excluded from both directions.

## The daily flow

The board has three columns and one tap moves a person rightwards through them.

1. **Roster** holds everyone on the standup, always alphabetical by the name shown. Tap a name as you see them join.
2. **Present, can be called on** holds the people who are actually there, oldest arrival first so whoever has waited longest sits at the top. Tap a name when you call on them and they finish. Each card here carries the turn that person took at the previous standup, so someone who went late last time can be called early today. Anyone who was not at that standup shows ∞ instead of a number.
3. **Presented** holds everyone who has given their update, in the order they gave it.

An undo arrow on each card moves someone back a column if you mis-tap.

The notes icon on a card opens that person's update panel. The icon is coloured once anything has been recorded for that person on that date, so you can see at a glance who still owes an update, and the card of whoever's panel is open carries an outline.

The panel has three boxes:

| Box | Behaviour |
| --- | --------- |
| Prior update | Read only, labelled with the date it came from. A copy button pushes its text into the current update so a "same as yesterday, plus…" update takes one tap. |
| Current update | Markdown editor with a formatting toolbar and a preview toggle. |
| Blockers | Same editor. A card showing blockers gets a warning icon on the board. |

Save writes both boxes. Cancel closes the panel, and asks first when there are edits that have not been saved.

Everything is keyed by standup, person, and date, so navigating to last Tuesday shows exactly what was recorded on last Tuesday.

# 💻 Technical Overview

| Element | Requirement |
| ------- | ----------- |
| Platform | .NET 10, Linux containers |
| Development Environment | Visual Studio Code or Visual Studio 2026 |
| Compiler | .NET SDK 10.0.400 (pinned in `global.json`) |
| Programming Language | C# 14 |
| UI | Blazor Server (interactive server render mode) with MudBlazor 9 |
| Markdown | Markdig for rendering, a custom toolbar plus a small JavaScript selection helper for editing |
| Identity | OpenID Connect (Kinde) via `Microsoft.AspNetCore.Authentication.OpenIdConnect` |
| Storage | Azure Table Storage (`Azure.Data.Tables`), managed identity authentication |
| Logging | Serilog: console everywhere, an Azure Table sink for audit records |
| Hosting | Azure Container Apps, external ingress, sticky sessions |
| Infrastructure as Code | Bicep, deployed by Azure DevOps Pipelines |
| Architecture | Four layers (UI, Application, Domain, Infrastructure) with the Domain owning the repository interfaces |
| Assistive Development | AI coding assistant |

## Solution layout

```text
StandFast.slnx
├── .vscode/                       F5 launch configuration and build/test tasks
├── Directory.Build.props          Shared build settings and the single version number
├── Directory.Packages.props       Central package version management
├── Dockerfile                     Multi-stage build onto the chiseled ASP.NET runtime
├── azure-pipelines.yaml           Test stage plus one release template block per environment
├── azure-pipelines-release.yaml   Release stage template: provisions, builds the image, deploys
├── infra/main.bicep               All Azure resources
├── src/
│   ├── StandFast.Domain           Entities, enums, calendar rules, repository interfaces
│   ├── StandFast.Application      Services, DTOs, validators, mapping, auditing
│   ├── StandFast.Infrastructure   Azure Table Storage implementation of the repositories
│   └── StandFast.Ui               Blazor Server app
└── tests/
    ├── StandFast.Application.Tests     Board behaviour and calendar rules against in-memory fakes
    └── StandFast.Infrastructure.Tests  Storage key ordering, table entity round-trips, and Azurite integration tests
```

## Running it locally

Azurite supplies Table Storage. Install it from npm, start it, then start the app with `dotnet run --project src/StandFast.Ui`.

Everything sensitive goes into user secrets rather than into `appsettings.json`; the project already carries a `UserSecretsId`. At a minimum that is `AzureTableStorage:ConnectionString` set to `UseDevelopmentStorage=true` for Azurite, and the three OIDC values. Sign-in needs an application registered with your OpenID Connect provider: in Kinde, create a **Back-end web** application, add `https://localhost:7111/signin-oidc` to its allowed callback URLs and `https://localhost:7111/signout-callback-oidc` to its allowed logout redirect URLs, then set `Oidc:Authority` (your `https://yourbusiness.kinde.com` domain), `Oidc:ClientId` and `Oidc:ClientSecret`. [Everything you set](#everything-you-set) lists the rest.

Run the tests with `dotnet test`. The Azurite integration tests skip themselves when nothing is listening on the emulator's table port, so the suite is green with or without it, on this machine and on the build agent.

`.vscode/launch.json` gives an F5 configuration that builds the solution and launches the UI on the `https` profile, taking its ports and environment from `launchSettings.json` rather than repeating them. It sets `hotReloadEnabled` to false, because starting a Hot Reload session writes `Service IManagedEditAndContinueEngineRegistration is unavailable` into the Debug Console on every launch. That exception comes from the debugger, not the app, and nothing stops working, but it appears on every run. Set the flag back to true once the C# Dev Kit installation is sorted out.

## Configuration

The release stage creates the resource group, then deploys the platform resources, then builds the image straight into the registry it just created, then deploys the app. The only one-time setup in Azure DevOps is:

1. An **Azure Resource Manager** service connection named `StandFast-Azure`. The name lives in `azure-pipelines.yaml` rather than a variable group, because Azure DevOps resolves service connection references while compiling the pipeline, before any variable group has been read.
2. The two variable groups below, with both authorized for the pipeline. A group that exists but is not authorized fails the run identically to one that does not exist.
3. Azure DevOps Pipelines Environment (e.g., `StandFast PRD`).

### Everything you set

Local development on the left, a deployed environment on the right. `<env>` is the lower-cased environment code of a cloud environment, so the group is `standfast-prd-vars` for production. `azure-pipelines.yaml` currently includes one release stage, PRD; a `dev` environment is a second template block plus its own `standfast-dev-vars` group.

| Local Development Setting Location | Local Development Key                                        | Local Development Value                  | Cloud ENV  Setting Location                        | Cloud ENV Key                                                | Cloud ENV Value             | Notes                                                        |
| ---------------------------------- | ------------------------------------------------------------ | ---------------------------------------- | -------------------------------------------------- | ------------------------------------------------------------ | --------------------------- | ------------------------------------------------------------ |
| `secrets.json`                     | `Oidc:Authority`                                             | (HTTPS Url)                              | `standfast-<env>-vars` group                       | `a_OidcAuthority`                                            | (HTTPS Url)                 | Your provider's issuer URL, for example `https://yourbusiness.kinde.com`. Every endpoint is read from its discovery document, so none is configured individually. |
| `secrets.json`                     | `Oidc:ClientId`                                              | (Client ID)                              | `standfast-<env>-vars` group                       | `a_OidcClientId`                                             | (Client ID)                 | Confidential client id from the provider.                    |
| `secrets.json`                     | `Oidc:ClientSecret`                                          | (Client Secret)                          | `standfast-<env>-vars` group                       | `a_OidcClientSecret`                                         | (Client Secret)             | Client secret from the provider. Mark the group variable as secret. Bicep always stores it in Key Vault as `oidc-client-secret` and gives the container app a Key Vault reference, so the value never becomes a plain environment variable. |
| `secrets.json`, optional           | `StandFastUi:DisplayTimeZoneId`                              | (e.g., `Central Standard Time`)          | `standfast-<env>-vars` group                       | `a_DisplayTimeZoneId`                                        | (e.g., `Central Standard Time`) | The [time zone id](#time-zones) the board resolves "today" in. The group variable must exist, because the pipeline passes it on every run; leave its value empty and the app falls back to the server time zone, which in the container is UTC. Locally, omitting it falls back to your machine's zone. |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_AllowedBranches`                                          | (e.g., `main\|release/1.2`) | Pipe-delimited list of the branches that may release to this environment; see [Branch filtering](#branch-filtering). Leave it unset and every branch that triggers the pipeline releases here, which for a production environment is rarely what you want. |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_RegionToken`                                              | (e.g., `usnorth`)           | Region segment of every resource name                        |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_Location`                                                 | (e.g., `northcentralus`)    | Azure region everything is created in                        |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-vars` group                             | `a_AppBase`                                                  | `standfast`                 | First segment of every resource name and the `Product` tag. Identical across environments |
| `secrets.json`                     | `AzureTableStorage:ConnectionString`                         | `UseDevelopmentStorage=true` for Azurite | (n/a)                                              | (n/a)                                                        | (n/a)                       | Table storage for data records. Required locally: no committed file sets it, and startup fails unless either this or `ServiceUri` is present. Left empty in the cloud, where `ServiceUri` and the managed identity are used instead. |
| `appsettings.Development.json`     | `AzureTableStorage:TablePrefix`                              | `StandFastDev`                           | (n/a)                                              | (n/a)                                                        | (n/a)                       | Prefix on every table name, so one storage account can hold several environments. Committed, and overridable in `secrets.json` if you want your own. Letters and digits only, starting with a letter, 21 characters at most. Set by Bicep in the cloud. |
| `appsettings.json`, optional       | `AzureTableStorage:CreateTablesOnStartup`                    | `true`                                   | `appsettings.json`, optional                       | `AzureTableStorage:CreateTablesOnStartup`                    | `true`                      | Already `true` in the committed file, so there is nothing to do unless you are turning it off where the identity has no table-create rights. |
| `appsettings.json`                 | `Oidc:CallbackPath`, `Oidc:SignedOutCallbackPath`, `Oidc:SignedOutRedirectUri` | (See file)                               | `appsettings.json`                                 | `Oidc:CallbackPath`, `Oidc:SignedOutCallbackPath`, `Oidc:SignedOutRedirectUri` | (See file)                  | Standard ASP.NET Core paths. Both callback paths must be registered with the provider; the release stage prints the exact URLs to register. |
| `appsettings.json`, optional       | `Oidc:Scopes`                                                | `openid`, `profile`, `email`             | `appsettings.json`, optional                       | `Oidc:Scopes`                                                | `openid`, `profile`, `email` | Anything listed is added to those three rather than replacing them. Only needed if your provider requires an extra scope. |

#### Set for you

Nothing here is yours to fill in. It is listed so a value you find in the deployed app can be traced back to what produced it.

| Key                                   | Where it comes from                                          |
| ------------------------------------- | ------------------------------------------------------------ |
| `AzureTableStorage:ServiceUri`        | Bicep, from the storage account it creates. The app then authenticates with `DefaultAzureCredential` and no key is involved. Empty locally, which is what makes the connection string take over. |
| `AzureTableStorage:TablePrefix`       | Bicep, as `a_AppBase` + the environment code, so `standfastPRD`. Overrides the committed `appsettings.json` value in the cloud. |
| `StandFastUi:DataProtectionBlobUri`   | Bicep, from the same storage account. Holds the shared Data Protection key ring, which more than one replica requires. |
| `ASPNETCORE_ENVIRONMENT`              | Bicep, always `Production`, in every cloud environment including DEV. `appsettings.Development.json` is therefore a local-only file and never applies to a deployed environment. |
| Resource group, registry login server, image tag | The release stage, from `a_AppBase`, `a_RegionToken` and the build number. |
| `p_MinReplicas`, `p_MaxReplicas`      | `infra/main.bicep` defaults of 1 and 3. No variable group feeds them; change the defaults to change the scale range, and read [What bites Blazor Server specifically](#what-bites-blazor-server-specifically) before dropping the minimum to zero. |

The `g_` variables declared in `azure-pipelines.yaml` and the `v_` values the release stage computes are part of the pipeline rather than settings, and need nothing from you.

### Naming by location

The same setting is spelled differently depending on where it lives.

| Location | Used by | Naming | Example |
| -------- | ------- | ------ | ------- |
| `appsettings.json` | Every environment. Committed defaults, never secrets. | Nested JSON: section object, then key. | `"Oidc": { "CallbackPath": "/signin-oidc" }` |
| `appsettings.Development.json` | Local only. Committed. | Same nesting, merged over the base file. Arrays merge by index, so an entry replaces the base array's entry rather than adding to it. | `"AzureTableStorage": { "TablePrefix": "StandFastDev" }` |
| User secrets | Local only, for anything sensitive. Never committed; stored outside the repo and keyed by the `UserSecretsId` in `StandFast.Ui.csproj`. | Flat, colon separated (can also be nested JSON like `appsettings.json`) | `"Oidc:ClientId": "…"` |
| Container environment variables | Azure only. Set on the container app by `infra/main.bicep`. | Section and key joined by a double underscore, because a colon is not portable across shells. | `Oidc__ClientId` |
| Key Vault | Azure only, for secrets. Referenced by the container app and resolved with the user-assigned managed identity. | Lower case, hyphen separated. | `oidc-client-secret` |
| Bicep parameters | Deployment inputs in `infra/main.bicep`. | `p_` prefix, Pascal case. Locals are `v_`, outputs are `o_`. | `p_OidcClientId` |
| Azure DevOps variable groups | Pipeline inputs that supply the Bicep parameters. | `a_` prefix, Pascal case. Variables declared in the pipeline YAML itself use `g_`. | `a_OidcClientId` |

Later sources override earlier ones: `appsettings.json` is the base, `appsettings.Development.json` merges over it locally, and user secrets and environment variables win over both.

#### Time Zones

`DisplayTimeZoneId` accepts a Windows id or an IANA id, and .NET resolves both on Windows and on the Linux container, so `Central Standard Time` and `America/Chicago` are equivalent. The US zones are:

| Windows id | IANA id | Covers |
| ---------- | ------- | ------ |
| `Hawaiian Standard Time` | `Pacific/Honolulu` | Hawaii, no daylight saving |
| `Alaskan Standard Time` | `America/Anchorage` | Alaska |
| `Pacific Standard Time` | `America/Los_Angeles` | Pacific |
| `US Mountain Standard Time` | `America/Phoenix` | Arizona, no daylight saving |
| `Mountain Standard Time` | `America/Denver` | Mountain |
| `Central Standard Time` | `America/Chicago` | Central, the Bicep default |
| `Eastern Standard Time` | `America/New_York` | Eastern |
| `US Eastern Standard Time` | `America/Indiana/Indianapolis` | Indiana (East) |

For anywhere else, `TimeZoneInfo.GetSystemTimeZones()` lists every id the .NET C# runtime accepts.

# 📐 Architecture Summary

## Layering

Each layer depends only on the one below it. Compile-time dependencies point inward: the Domain declares the repository interfaces, and Infrastructure implements them, so nothing above the Domain knows Azure Table Storage exists.

```text
┌─────────────────────────────────────────────────────────────┐
│ StandFast.Ui                                                │
│ Blazor Server, MudBlazor, OpenID Connect sign-in, Serilog   │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ StandFast.Application                                       │
│ Services, DTOs, validators, mapping, audit records          │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ StandFast.Domain                                            │
│ Entities, enums, calendar rules, repository interfaces      │
└──────────────────────────────┬──────────────────────────────┘
                               │ implements
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ StandFast.Infrastructure                                    │
│ Azure Table Storage clients, table entities, key design     │
└─────────────────────────────────────────────────────────────┘
```

The UI never sees a domain entity and never sees an `ITableEntity`. Services return DTOs; the Application layer is the only place a mapping happens; Infrastructure owns a parallel set of table entity classes because Azure Tables has no enum, `DateOnly`, or `TimeOnly` type and those encodings are a storage concern, not a domain one.

Ids are version 7 GUIDs. They sort by creation time, which keeps row keys from fragmenting, and they are safe to put in a URL.

## Data model and Azure Table Storage

Four tables, all prefixed with `AzureTableStorage:TablePrefix`:

| Table | Partition key | Row key | Holds |
| ----- | ------------- | ------- | ----- |
| `People` | `Person` | Person id | The directory. One partition because it is small and always listed whole. |
| `Standups` | `Standup` | Standup id | Meeting definitions. Same reasoning. |
| `StandupMembers` | Standup id | Person id | The roster. One partition per standup, which is exactly how the board reads it. The People screen's Standups column is the one query that crosses partitions; see below. |
| `StandupEntries` | Standup id + person id | Inverted meeting date | Attendance state, the update, and the blockers. |
| `AuditLog` | Date bucket | Timestamp | Written by Serilog, not by the repositories. Outside backup and restore; see [Backup and restore](#backup-and-restore). |

### Tables and keys

The entries table is the only interesting piece of key design. The board needs two things for each person: their record for the selected date, and their most recent record before it. Storing the row key as the date subtracted from `99999999` makes ascending row-key order the same as descending date order, which collapses both lookups into one range query.

```text
┌───────────────────────────────────────────────────────────────────┐
│ StandFastEntries                                                  │
│                                                                   │
│ PartitionKey   {standupId}_{personId}                             │
│ RowKey         99999999 - yyyyMMdd                                │
│                                                                   │
│ One participant's whole history is one partition, ordered         │
│ newest first, so a single range query returns both rows the       │
│ board needs:                                                      │
│                                                                   │
│   PartitionKey eq {pk} and RowKey ge {rowKey(date)}   take 2      │
│     row 1  the requested date, when it exists                     │
│     row 2  the most recent earlier date, the prior update         │
└───────────────────────────────────────────────────────────────────┘
```


Rendering the board is then one range query per roster member, run in parallel. For a standup of ten to twenty people that is ten to twenty point-ish reads of a few milliseconds each. The alternative, partitioning entries by standup and date so the whole board is a single partition scan, would make the board cheaper but would force a second, denormalised copy of every row to answer "what did this person say last time". One consistent copy beats a dual write here.

`StorageKeys` is the only place a partition or row key is constructed, and its ordering guarantees are covered by tests, because getting this wrong fails silently by showing the wrong prior update rather than by throwing.

"Which standups is this person on" is the one question the key design cannot answer from a partition, because memberships are partitioned by standup. The People screen gets it from a single unfiltered query over `StandupMembers`, which is a table scan. That is deliberate: the table holds one small row per person per standup, so scanning it costs less than maintaining a second index written on every roster change, and the alternative of one partition query per standup trades a scan for N round trips.

### Why Table Storage and not SQL

Agreed, and for more reasons than cost. Every read this app performs is either "give me a small list" or "give me this participant's last two rows", which is exactly the access pattern Table Storage is good at. There are no joins, no reporting queries, no referential integrity worth enforcing in the database, and the write volume is a few rows per person per day. Azure SQL or PostgreSQL would add a server to size, patch, back up, and pay for around the clock, in exchange for query capabilities this app never uses.

Two caveats worth knowing before the design ossifies:

- **No cross-table transactions.** Deleting a standup removes its roster in a loop, not atomically. If that matters later, move the roster into the standup partition so the delete becomes one batch.
- **No ad-hoc queries.** "Show me everyone who was blocked in August" means a scan, or a second index table written at the same time as the entry. If reporting becomes a real requirement rather than a nice-to-have, that is the point to revisit this, and moving to SQL then is a contained change because the repository interfaces are the only seam that would move.

## Markdown editing

`MarkdownEditor` is one component used by all three boxes on the update panel, so the toolbar, the preview toggle, and the character limit cannot drift apart. The toolbar itself is data: `MarkdownCommands.All` is a list of records describing each button, and the component renders whatever is in that list.

Formatting runs through a small JavaScript helper, because wrapping a selection needs the caret position and the browser owns that. The helper computes the new text and hands it back to Blazor, which remains the owner of the value. Inline commands toggle: pressing bold on already-bold text unwraps it.

Rendering uses a single pre-built Markdig pipeline with advanced extensions on and raw HTML disabled. Update text is user-supplied and rendered into the page, so HTML is escaped rather than executed.

## Auditing and logging

Serilog handles both, separated by a property rather than by a second logger. `IAuditLog.Record` opens a logging scope containing `IsAuditEvent` plus the actor, the target, and the event name; a Serilog sub-logger filters on that property and writes those events to the `AuditLog` table with the audit properties promoted to real columns. Ordinary application logs go to the console, which is what Container Apps forwards to Log Analytics.

The actor comes from `ICurrentUser`, an Application-layer abstraction implemented in the UI over the signed-in principal, so the Application layer attributes an action without referencing ASP.NET Core.

Audit event names live in `AuditEvents` so a later report reads the same constants the writers use.

## Backup and restore

The Backup screen downloads every application table as one JSON file and restores one back, replacing all current data. The audit log is not in either direction: Serilog owns that table, it is the record of who changed what, and restoring an older copy over it would erase the trail explaining the restore itself. `StorageNames.DataTables` is the single list of what a backup covers, and the Azurite fixture cleans up from the same list.

The file carries rows as columns rather than as typed entities, with each value tagged by its storage type:

```text
┌─────────────────────────────────────────────────────────────┐
│ Backup file                                                 │
│                                                             │
│  formatVersion, createdUtc, application                     │
│                                                             │
│  tables[]                                                   │
│   ├─ name    logical table name, no environment prefix      │
│   └─ rows[]  column name → { kind, value }                  │
│                                                             │
│  kind is one of String, Boolean, Int32, Int64, Double,      │
│  DateTimeOffset, Guid, Binary: the types Azure Tables       │
│  stores, so a value goes back as the type it came out as.   │
└─────────────────────────────────────────────────────────────┘
```

Two consequences follow from that shape. A column added to a table entity is backed up without touching the backup code, and a file taken before that column still restores. And because the table name stored is the logical one, a backup taken from one environment restores into another whatever its table prefix is.

A restore is validated in full before a single row is touched: an unrecognised format version, a table the application does not own, a table listed twice, or a row that cannot be converted all stop the restore with the data untouched. Past that point each table is emptied and refilled, in batches of 100 within a partition, because Azure Tables has no transaction spanning tables or partitions. A failure mid-way therefore leaves the remaining tables as they were rather than half-merged; the file is still on disk, so the fix is to run the restore again.

The download is a plain HTTP endpoint rather than something the Blazor circuit produces, so the file streams with its own content type and file name and never sits in circuit memory. It carries no authorisation metadata, which means the fallback policy protects it exactly like a page. Both directions are audited.

## OpenID Connect sign-in

Authorization code flow with PKCE against the provider's discovery document, with the session held in a cookie. A fallback authorisation policy requires an authenticated user, so a new page is protected unless it opts out; the health endpoint and the two auth endpoints are the deliberate exceptions.

Nothing in the code names a provider. `AuthenticationSetup` reads an `Authority`, a client id and a secret, and discovers every endpoint from `{Authority}/.well-known/openid-configuration`, so swapping providers is a configuration change. StandFast is configured against Kinde.

Inbound claim mapping is switched off, so claims stay under their OIDC names: `sub` is the audit actor, with `name` and `email` for display. The legacy SOAP claim URIs never appear.

`/auth/login` and `/auth/logout` replace what an identity-provider-specific UI package would otherwise supply. Sign-out ends both the local cookie session and the provider's own session, so the next sign-in is a real one rather than a silent re-issue. The login endpoint accepts a `returnUrl`, and only site-relative values are honoured, so a crafted link cannot bounce a user to another host once authenticated.

Two deployment details matter:

- Container Apps terminates TLS at its ingress and forwards plain HTTP, so `UseForwardedHeaders` runs first in the pipeline. Without it the app builds `http://` redirect URIs, which the provider rejects as unregistered.
- The client secret is a Key Vault reference on the container app, resolved with the user-assigned managed identity. Everything else, storage and blobs included, uses that identity directly and has no secret at all.

## Azure Container Apps

This is the part of the stack that is new if you come from App Service, so here is what actually changes in practice.

Container Apps is Kubernetes with the Kubernetes removed from view. Underneath there is AKS, KEDA for scaling, Envoy for ingress, and Dapr if you want it; you get a managed environment, revisions, and a YAML-free deployment surface. An App Service Plan maps roughly to a Container Apps *Environment*, and a Web App maps roughly to a *Container App*, but the unit you deploy is an image rather than a build output.

### What you gain over App Service

- **The runtime is yours.** No waiting for a stack version to appear in the platform's list, no `WEBSITE_*` app settings to coax a preview SDK into working. The .NET 10 chiseled base image in `Dockerfile` is the whole story, and it is the same image locally, in the pipeline, and in Azure.
- **Revisions are first class.** Every deployment creates a revision. You can run two at once and split traffic by percentage without slot swaps, and rolling back is pointing traffic at the previous revision rather than redeploying.
- **Scaling responds to more than CPU.** KEDA scalers cover HTTP concurrency, queue depth, and custom metrics. This app only uses the HTTP scaler, but the door is open.
- **Scale to zero exists.** Not useful here (see below), but it is the reason Container Apps is cheap for bursty or internal workloads.
- **Per-second consumption billing.** You pay for vCPU-seconds and memory-seconds actually used, rather than for a plan sitting idle. A small always-on app can land either side of a B1 plan depending on replica count.
- **Managed identity works the same way**, so the storage and Key Vault story is identical to what you already do on App Service.

### What it costs you

- **You now own the image.** Base image CVEs, `dotnet publish` inside a container, and a registry to feed are your problem. App Service patched the runtime for you.
- **There is no Kudu.** No console into the app, no `/home` file share, no log files on disk unless you mount storage. Diagnostics go through Log Analytics, which is why the file sink here is development-only and production logs go to stdout.
- **The local filesystem is ephemeral and the container is not writable in the usual places.** Anything that assumed a writable working directory needs rethinking. This is what forces the Data Protection key ring into blob storage.
- **Cold start is a container start, not a warm-up.** Pulling and starting an image is slower than an App Service instance waking up.
- **More moving parts to provision.** Registry, environment, Log Analytics workspace, and identity, versus a plan and a site. Compare the two halves of `infra/main.bicep` to see it.
- **Cost is harder to predict.** A plan has a price per month. Consumption billing depends on how often you scale and for how long.

### What bites Blazor Server specifically

Blazor Server keeps a stateful SignalR circuit per browser tab, which makes it the least forgiving workload to put on an elastic, multi-replica platform. Three settings in this repo exist solely because of that, and they are worth understanding before you reuse this pattern:

1. **Sticky sessions are on.** `ingress.stickySessions.affinity` is `sticky`. Without it a reconnecting browser can be routed to a different replica, which has never heard of its circuit, and the user gets the reconnect overlay followed by a full reload.
2. **The Data Protection key ring is shared.** Circuit state and antiforgery tokens are encrypted with it. Left on local disk, each replica generates its own key ring and any request that lands on another replica fails to decrypt. The key ring lives in a blob, written through the managed identity.
3. **Minimum replicas is one, not zero.** Scaling to zero terminates every live circuit. For an app whose whole job is to be open during a meeting, that is not a trade worth making, so the floor stays at one and the saving goes with it.

If those three were not needed, the same Bicep would happily scale this to zero and cost close to nothing between standups. That tension between an elastic host and a stateful UI framework is the real lesson of running this here.

## Deploying

`azure-pipelines.yaml` restores, builds and tests, then includes `azure-pipelines-release.yaml` once per environment. Each release stage is self-contained: it creates the resource group, deploys `infra/main.bicep` with `p_DeployApp=false` to bring up the registry and the rest of the platform, runs `az acr build` to build the image inside that registry, then deploys the same Bicep again with `p_DeployApp=true` and the resulting image. Both deployments read one parameters file composed in PowerShell, so their inputs cannot drift apart.

Building in the registry rather than on the agent means there is no Docker registry service connection and no Docker daemon in the pipeline. The cost is that each environment builds its own image rather than promoting one artefact; with a registry per environment that would need an import step either way.

What you set up once in Azure DevOps is in [Configuration](#configuration).

### Branch filtering

Which branches may release to an environment is `a_AllowedBranches` in that environment's variable group, so tightening or loosening it is an edit in Azure DevOps rather than a pipeline change, and a new environment brings its own answer with it. The release stage's `condition` reads it directly; there is no extra stage, job or script behind it.

Write the branches as a pipe-delimited list, without the `refs/heads/` prefix:

| You write | It matches |
| --------- | ---------- |
| `main` | That branch. |
| `main\|release/1.2` | Either of those two. |
| (unset or empty) | Every branch that triggers the pipeline. |

Entries are whole branch names, compared one for one. `release/` does not stand for everything beneath it, because an Azure DevOps condition can test a list for an exact member but has no way to ask whether any entry is a prefix of the branch. A versioned release branch is therefore listed as it is named, and a new one is a variable group edit at the point it is cut.

One thing to confirm on the first run: the variable group is linked to the release stage, and a stage's `condition` is evaluated close to when its variables are expanded. If `a_AllowedBranches` reads as empty in a run where it is set, the condition is being evaluated before the group is read, and the fix is to link the group at the root of `azure-pipelines.yaml` instead. Note which way this fails: an unreadable variable looks the same as an unset one, and an unset one allows every branch.

# 🚧 Change Summary

*Each entry is a specific version (release/\* branch), in descending order (newest version up top), with a plain bullet list summarizing each change without technical jargon.*

### v01.00.00 — 2026-09-15

- Created the StandFast solution: a daily scrum board with people, standup definitions, and a date-based board.
- Added the three-column board that opens on today, with the current week across the top and a separate picker for which standup you are running.
- Added tap-to-advance attendance: one tap marks someone present, a second marks them as having presented, and an undo arrow moves them back.
- Added the update panel with a read-only prior update (labelled with the date it came from), a current update, and a blockers box, all with markdown editing, a formatting toolbar, and a preview toggle.
- Added a one-tap copy of the prior update into the current update.
- Added people and standup management screens, including standup rosters and which days a standup runs on.
- Added Entra ID sign-in, with every page requiring a signed-in user.
- Changed sign-in from Entra ID to a standard OpenID Connect provider, configured against Kinde.
- Added auditing of every change through Serilog, written to its own storage table.
- Added the Azure deployment: container build, Bicep for all resources, and the Azure DevOps pipelines that deploy it.
- Added integration tests that run the storage layer against the Azurite emulator, covering the prior-update lookup and roster cleanup, and skipped automatically when the emulator is not running.
- Fixed startup hanging before the app began listening.
- Fixed console logging disappearing when running locally.
- Made deployment self-provisioning: the release stage creates the resource group, brings up the registry and supporting resources, builds the image inside that registry, then deploys the app, so nothing has to be created by hand first and no container registry connection is needed.
- Documented configuration as a single table of everything you set, with the naming used in each place a setting can live and the accepted time zone values.
- Added the previous standup's turn number to each card in the "can be called on" column, with ∞ for anyone who was not there, so whoever went last time can be called first today.
- Added an optional display name and markdown notes to each person, with the display name used everywhere in place of their first and last name when it is set.
- Fixed the board column ordering: the roster is always alphabetical by the name shown, the "can be called on" column runs oldest arrival first, and Presented runs in the order people actually presented.
- Added a Cancel button beside Save on the update panel, which discards unsaved edits.
- Added a Standups column to the People table showing which standups each person is on.
- Added seconds to the presented time on the board, so the order people presented in is unambiguous.
- Added a Backup screen that downloads everything the app holds as a single file, and restores one back by replacing all current data. The audit log is left out of both, so restoring an old backup never wipes the record of who changed what.
- Fixed the repository's ignore rules, which were quietly leaving the new backup source folders out of version control.
- Added an F5 launch configuration and build/test tasks for the editor, with Hot Reload switched off to stop a debugger error appearing in the Debug Console on every run.
- Coloured the notes icon on a board card once that person has an update recorded for the date, so it is obvious who still owes one, with the outline on the card left to mark whose panel is open.
- Changed Cancel on the update panel to always be available and to close the panel, asking first whether unsaved edits should be lost.
- Corrected the configuration table against what the code and the deployment actually do, and split out a short list of the values that are filled in for you.
- Fixed the build pipeline failing every storage integration test: the check for whether the emulator is running threw instead of answering on an agent that has no emulator, so the tests reported as failures rather than skipping as intended.
- Moved the decision about which branches may deploy to an environment out of the pipeline and into that environment's variable group, as a pipe-delimited list of branch names. Leaving it unset lets any branch that triggers the pipeline deploy there.
