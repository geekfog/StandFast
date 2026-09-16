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
    - [Configuration sources and naming](#configuration-sources-and-naming)
    - [Settings](#settings)
- [📐 Architecture Summary](#-architecture-summary)
  - [Layering](#layering)
  - [Data model and Azure Table Storage](#data-model-and-azure-table-storage)
    - [Tables and keys](#tables-and-keys)
    - [Why Table Storage and not SQL](#why-table-storage-and-not-sql)
  - [Markdown editing](#markdown-editing)
  - [Auditing and logging](#auditing-and-logging)
  - [OpenID Connect sign-in](#openid-connect-sign-in)
  - [Azure Container Apps](#azure-container-apps)
    - [What you gain over App Service](#what-you-gain-over-app-service)
    - [What it costs you](#what-it-costs-you)
    - [What bites Blazor Server specifically](#what-bites-blazor-server-specifically)
  - [Deploying](#deploying)
- [🚧 Change Summary](#-change-summary)

# 📃 Overview

## What it does

- **People** are a flat directory: first name, last name, email, active flag.
- **Standups** are recurring meeting definitions: name, the days they run on, start time, time zone, and a roster of people.
- **The board** is one standup on one date. It opens on today with the current week across the top, and a dropdown picks the standup independently of the date.

## The daily flow

The board has three columns and one tap moves a person rightwards through them.

1. **Roster** holds everyone on the standup. Tap a name as you see them join.
2. **Present, can be called on** holds the people who are actually there. Tap a name when you call on them and they finish.
3. **Presented** holds everyone who has given their update.

An undo arrow on each card moves someone back a column if you mis-tap.

The notes icon on a card opens that person's update panel, which has three boxes:

| Box | Behaviour |
| --- | --------- |
| Prior update | Read only, labelled with the date it came from. A copy button pushes its text into the current update so a "same as yesterday, plus…" update takes one tap. |
| Current update | Markdown editor with a formatting toolbar and a preview toggle. |
| Blockers | Same editor. A card showing blockers gets a warning icon on the board. |

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
├── Directory.Build.props          Shared build settings and the single version number
├── Directory.Packages.props       Central package version management
├── Dockerfile                     Multi-stage build onto the chiseled ASP.NET runtime
├── azure-pipelines.yaml           Build stage plus one template block per environment
├── azure-pipelines-release.yaml   Release stage template
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

Azurite supplies Table Storage, and the development settings already point at it. Install Azurite from npm, start it, then start the app with `dotnet run --project src/StandFast.Ui`.

Sign-in needs an application registered with your OpenID Connect provider. In Kinde, create a **Back-end web** application, add `https://localhost:7111/signin-oidc` to its allowed callback URLs and `https://localhost:7111/signout-callback-oidc` to its allowed logout redirect URLs, then put `Oidc:Authority` (your `https://yourbusiness.kinde.com` domain), `Oidc:ClientId` and `Oidc:ClientSecret` in user secrets rather than in `appsettings.json`. The project already carries a `UserSecretsId`.

Run the tests with `dotnet test`.

## Configuration

Every setting binds to a typed options class through `IOptions<T>` and is validated at startup, so a missing or malformed value fails the app immediately rather than on the first request. Section and key names are the same everywhere; only their spelling changes with where the value comes from.

### Configuration sources and naming

Later sources override earlier ones: `appsettings.json` is the base, `appsettings.Development.json` merges over it locally, and user secrets and environment variables win over both.

| Location | Used by | Naming | Example |
| -------- | ------- | ------ | ------- |
| `appsettings.json` | Every environment. Committed defaults, never secrets. | Nested JSON: section object, then key. | `"Oidc": { "CallbackPath": "/signin-oidc" }` |
| `appsettings.Development.json` | Local only. Committed. | Same nesting, merged over the base file. Arrays merge by index, so an entry replaces the base array's entry rather than adding to it. | `"AzureTableStorage": { "TablePrefix": "StandFastDev" }` |
| User secrets | Local only, for anything sensitive. Never committed; stored outside the repo and keyed by the `UserSecretsId` in `StandFast.Ui.csproj`. | Flat, colon separated. | `"Oidc:ClientId": "…"` |
| Container environment variables | Azure only. Set on the container app by `infra/main.bicep`. | Section and key joined by a double underscore, because a colon is not portable across shells. | `Oidc__ClientId` |
| Key Vault | Azure only, for secrets. Referenced by the container app and resolved with the user-assigned managed identity. | Lower case, hyphen separated. | `oidc-client-secret` |
| Bicep parameters | Deployment inputs in `infra/main.bicep`. | `p_` prefix, Pascal case. Locals are `v_`, outputs are `o_`. | `p_OidcClientId` |
| Azure DevOps variable groups | Pipeline inputs that supply the Bicep parameters. | `a_` prefix, Pascal case. Variables defined in the pipeline YAML itself use `g_`. | `a_OidcClientId` |

Two variable groups are expected in Azure DevOps › Pipelines › Library, where `<env>` is the lower-cased environment code, so `standfast-prd-vars` for production:

| Group | Scope | Holds |
| ----- | ----- | ----- |
| `standfast-vars` | Pipeline, loaded before every stage | `a_AzureServiceConnection`, `a_SubscriptionId`, `a_ContainerRegistryConnection`, `a_AppBase`, and anything else identical across environments. Service connection references resolve at compile time, before stage-level groups exist, so they cannot live in a per-environment group. |
| `standfast-<env>-vars` | Stage, loaded by the release template for the matching environment | Only what genuinely differs per environment: `a_ResourceGroup`, `a_Location`, `a_RegionToken`, `a_ContainerRegistryLoginServer`, `a_OidcAuthority`, `a_OidcClientId`, `a_OidcClientSecret`, `a_DisplayTimeZoneId`. |

### Settings

The app only ever reads the section and key on the left. The two middle columns say where that value is authored. In Azure the release template carries a variable-group value into the matching Bicep parameter, which sets the `Section__Key` environment variable on the container app; that plumbing is the same for every row and is not repeated below.

| Section | Key | Local/Code-based Setting | Azure-based Setting | Notes |
| ------- | --- | ------------------- | -------------------- | ----- |
| `Oidc` | `Authority` | User secrets | `a_OidcAuthority` | Issuer base URL, for example `https://yourbusiness.kinde.com`. Every endpoint is read from its discovery document, so none is configured individually. |
| `Oidc` | `ClientId` | User secrets | `a_OidcClientId` | Confidential client id from the provider. |
| `Oidc` | `ClientSecret` | User secrets | `a_OidcClientSecret`, held as the Key Vault secret `oidc-client-secret` | The only secret the app holds. Everything else in Azure goes through the managed identity. |
| `Oidc` | `CallbackPath`, `SignedOutCallbackPath`, `SignedOutRedirectUri` | `appsettings.json` | `appsettings.json` | Default to the standard ASP.NET Core paths and rarely need changing. Both callback paths must be registered with the provider. |
| `AzureTableStorage` | `ServiceUri` | Not set | Bicep, from the storage account it creates | Table endpoint. When set, the app authenticates with `DefaultAzureCredential` and no key is involved. Takes precedence over `ConnectionString`. |
| `AzureTableStorage` | `ConnectionString` | `appsettings.Development.json` | Not set | Used only when `ServiceUri` is empty. This is how Azurite is reached. |
| `AzureTableStorage` | `TablePrefix` | `appsettings.Development.json` | Bicep, derived from the app base name and environment code | Prefixed to every table name so one storage account can hold several environments. |
| `AzureTableStorage` | `CreateTablesOnStartup` | `appsettings.json` | `appsettings.json` | Creates missing tables on first use. Turn it off where the identity has no table-create rights. |
| `StandFastUi` | `DisplayTimeZoneId` | `appsettings.json` or user secrets, optional | `a_DisplayTimeZoneId` | The time zone the board resolves "today" in. Accepts a Windows id or an IANA id; .NET resolves both on Windows and on the Linux container. Empty falls back to the server time zone. |
| `StandFastUi` | `DataProtectionBlobUri` | Not set | Bicep, from the storage account it creates | Blob holding the shared Data Protection key ring. Required for more than one replica. |

`DisplayTimeZoneId` takes either form, so `Central Standard Time` and `America/Chicago` are equivalent. The US zones are:

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

For anywhere else, `TimeZoneInfo.GetSystemTimeZones()` lists every id the runtime accepts.

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
| `StandupMembers` | Standup id | Person id | The roster. One partition per standup, which is exactly how the board reads it. |
| `StandupEntries` | Standup id + person id | Inverted meeting date | Attendance state, the update, and the blockers. |
| `AuditLog` | Date bucket | Timestamp | Written by Serilog, not by the repositories. |

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

`azure-pipelines.yaml` builds, tests, builds and pushes the image, and publishes `infra/` as an artefact. `azure-pipelines-release.yaml` is the release template, included once per environment, and deploys `main.bicep` with a parameters file composed in PowerShell.

The Azure DevOps variable groups it reads, and the naming convention for every place a setting can come from, are documented together in [Configuration](#configuration).

After the first deployment, take `o_ApplicationUrl` from the outputs and register `<url>/signin-oidc` as an allowed callback URL and `<url>/signout-callback-oidc` as an allowed logout redirect URL with the provider.

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
- Reworked the configuration documentation so every setting shows where it is set locally and in Azure, added the naming rules for each place a setting can come from, and moved the Azure DevOps variable groups next to it instead of leaving them in the deployment section.
- Documented which time zone values the board accepts, with the Windows and IANA name for each US zone.
