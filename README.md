# About StandFast

StandFast is a daily scrum board. You pick a standup and a date, tap people as you spot them in the Teams call, tap them again as they give their update, and capture what they said as markdown alongside whatever they said last time.

It runs as a single Blazor Server container in Azure Container Apps, signs in through OpenID Connect (Kinde), and stores everything in Azure Table Storage.

# Table of Contents

- [About StandFast](#about-standfast)
- [📃 Overview](#-overview)
  - [What it does](#what-it-does)
  - [The daily flow](#the-daily-flow)
  - [Reports](#reports)
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
  - [Charting](#charting)
  - [Auditing and logging](#auditing-and-logging)
  - [Backup and restore](#backup-and-restore)
  - [OpenID Connect sign-in](#openid-connect-sign-in)
  - [Azure Container Apps](#azure-container-apps)
    - [What you gain over App Service](#what-you-gain-over-app-service)
    - [What it costs you](#what-it-costs-you)
    - [What bites Blazor Server specifically](#what-bites-blazor-server-specifically)
  - [Deploying](#deploying)
    - [Versions and branches](#versions-and-branches)
    - [Branch filtering](#branch-filtering)
    - [Custom domain](#custom-domain)
- [🚧 Change Summary](#-change-summary)

# 📃 Overview

## What it does

- **People** are a flat directory: first name, last name, email, active flag, an optional "display as" override, and markdown notes. When "display as" is set, that is how the person appears everywhere, including the roster and the board; otherwise they appear as first and last name. The People screen also lists which standups each person presents at and which they can lead.
- **Standups** are recurring meeting definitions: name, the days they run on, start time, time zone, and two rosters. The Presenter Roster is who gives an update; the Leader Roster is who may run the meeting. The same person can be on both.
- **The board** is one standup on one date. It opens on today with the current week across the top, a dropdown picks the standup independently of the date, and a second dropdown beside it records who is leading that day.
- **Reports** chart what a standup has recorded over a period. A dropdown picks the report, a second picks the standup, and quick-pick buttons set how far back it runs.
- **Backup** downloads everything the app holds as one file and restores it again, replacing whatever is there at the time. The audit log and each user's own settings are excluded from both directions.
- **Appearance** is light or dark, chosen from the toggle in the title bar and remembered for whoever is signed in. It follows that person to any browser or machine they sign in from, and a user who has never chosen gets light.
- **About** shows the version, summarizes what the app is for, and links to its source repository.

## The daily flow

The board has three columns and one tap moves a person rightwards through them.

1. **Roster** holds everyone on the standup's Presenter Roster, always alphabetical by the name shown. Tap a name as you see them join.
2. **Present, can be called on** holds the people who are actually there, in the same alphabetical order as the roster, so a name sits in the same place whichever of the two columns it is in. Tap a name when you call on them and they finish. Each card here carries the turn that person took at the previous standup, so someone who went late last time can be called early today. Anyone who was not at that standup shows ∞ instead of a number, and the card's caption says when they were marked present.
3. **Presented** holds everyone who has given their update, in the order they gave it.

The Leader dropdown beside the standup picker records who ran the standup that day. It offers the standup's Leader Roster and nobody else, it is per date rather than per standup, and it can be left empty, so a standup nobody was picked for reads as exactly that.

The Lock button beside the date closes that date once the standup is over, so a stray tap on a board someone left open cannot change what was recorded. It becomes available once at least one person has presented, since a date nobody has spoken on has nothing to close. A locked date still opens and still shows every update; what it withholds is the tapping, the undo arrows, the Leader dropdown and the Save button. Unlocking asks first, since that is the press that puts a finished standup back within reach.

The time the lock carries is the time it was applied, as long as the standup is still recent. Lock the date hours later, or the next morning, and it is dated from the last person who presented instead, plus a few minutes, because a board closed the next day should not read as though the meeting ran that long. Both windows are settings; see [Everything you set](#everything-you-set). A date nobody presented on has no such anchor, so it takes the time it was locked whenever that was, and a date locked twice keeps the first time.

A dot under a date in the week strip means someone presented on that date, so a week with a finished standup is recognisable without opening each day. The dot is green while the date is still open and orange once it is locked, matching the colour the Lock button and the locked banner carry.

Your own card carries a dark yellow star after the name, in whichever column you are sitting in, so you can find yourself on a long roster without reading the names. It shows when the address you signed in with matches the one on your person record.

An undo arrow on each card moves someone back a column if you mis-tap.

The notes icon on a card opens that person's update panel. The icon turns red once anything has been recorded for that person on that date, in all three columns, so you can see at a glance who still owes an update. The card of whoever's panel is open carries an outline.

The panel has three boxes:

| Box | Behaviour |
| --- | --------- |
| Prior update | Read only, labelled with the date it came from. A copy button pushes its text into the current update so a "same as yesterday, plus…" update takes one tap. |
| Current update | Markdown editor with a formatting toolbar and a preview toggle. |
| Blockers | Same editor. A card showing blockers gets a warning icon on the board. |

Save writes both boxes. Cancel closes the panel, and asks first when there are edits that have not been saved.

Everything is keyed by standup, person, and date, so navigating to last Tuesday shows exactly what was recorded on last Tuesday.

## Reports

The Reports screen shows one report at a time. The report, the standup, and the period all live in the address, so a view can be pasted to someone else and they see the same thing.

The period runs back from today and is set by the quick-pick buttons: 30 days, 60, 90, 180, or a year. It opens on 30 days.

**Presenting Order vs Date** draws one line per person against the dates the standup ran. The vertical axis is the turn they took, first at the top, with ∞ along the bottom for a day they did not present; the horizontal axis is the dates themselves. A dot marks each day someone actually presented, and the line between dots shows whether they are drifting later or earlier over the period. Only days on which somebody presented appear, so a fortnight off does not fill the chart with empty columns, and only people who presented at least once in the period get a line — including anyone since taken off the roster, because the report is of what happened rather than of who is on the roster now.

Each person gets their own colour. Past the eighth person the colours start again as dashed lines, then as longer dashes, and so on through thirteen patterns, so two people never share both a colour and a line style until the 105th. The legend carries each person's line drawn in their own style, and the order numbers are also available as a plain table under the chart.

**Standup Activity** is a table of the days the standup actually ran, newest first. A day appears once somebody has presented on it, so a date that was only annotated — a leader picked, people marked present — is not one of them.

| Column | Holds |
| ------ | ----- |
| Date | The meeting date. |
| Day of Week | Its weekday, so a standup that keeps slipping to Fridays is visible without reading the dates. |
| Status | A padlock, orange and closed once the day is locked and open while it can still be changed. Hovering it gives the time it was locked. |
| Presenters | How many people gave their update that day. |
| Total Time (min) | Minutes from the day's first turn to the moment it was locked. A day still open has no end time, so it reads — instead of a number. |

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
├── .github/rulesets/              Branch protection ruleset for main and release branches, imported into GitHub
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
    ├── StandFast.Application.Tests     Board, report, and calendar rules against in-memory fakes
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

2. **Role Based Access Control Administrator** on that service connection's principal, at subscription scope. The Bicep gives the app's managed identity its four data-plane roles (otherwise yields `Authorization failed for template resource … Microsoft.Authorization/roleAssignments` in the pipeline). 

   

   **OPTION 1: Command Line**

   Run the following in an Azure Cloud Shell (PowerShell) with **Owner** or **User Access Administrator** on the Azure Subscription:

   ```powershell
   az devops service-endpoint list --organization https://dev.azure.com/sbp-cloud --project Dev --query "[?name=='StandFast-Azure'].authorization.parameters.serviceprincipalid" -o tsv
   ```

   This returns the <Application (client) ID>. Use this to run the following command:

   ```
   az ad sp show --id <Application (client) ID> --query id -o tsv
   ```

   This returns the <service principal object id> to use in the following below:

   ```powershell
   az role assignment create --assignee-object-id <service principal object id> --assignee-principal-type ServicePrincipal --role "Role Based Access Control Administrator" --scope /subscriptions/<subscription id>
   ```

   

   **OPTION 2: Azure Portal**

   Open the Azure DevOps Project-based Service Connection. Note the service connection ID (GUID) under the name. Click **Manage service connection roles**. This opens **Access control (IAM)** for the subscription On the top bar, click **+ Add** → **Add role assignment**. In the Role tab, click **Privileged administrator roles** tab. Click on **Role Based Access Control Administrator**. Click **Next** button, In the Members tab, verify **User, group, or service principal** is selected within **Assign access to**. click **+ Select members**. Search for the service connection ID (GUID) previously noted. By default, it is prefixed with the Azure DevOps Organization Name with a hyphen and the Azure DevOps Project name, followed by a hyphen and the service connection ID (GUID). select the member, click **Select** button, and click **Next** button. In the Conditions tab, choose **Allow user to assign all roles (highly privileged)**. Click the **Review + Assign** button.

   

3. The two variable groups below, with both authorized for the pipeline. A group that exists but is not authorized fails the run identically to one that does not exist.

4. Azure DevOps Pipelines Environment (e.g., `StandFast PRD`).

### Everything you set

Local development on the left, a deployed environment on the right. `<env>` is the lower-cased environment code of a cloud environment, so the group is `standfast-prd-vars` for production. `azure-pipelines.yaml` currently includes one release stage, PRD; a `dev` environment is a second template block plus its own `standfast-dev-vars` group.

| Local Development Setting Location | Local Development Key                                        | Local Development Value                  | Cloud ENV  Setting Location                        | Cloud ENV Key                                                | Cloud ENV Value             | Notes                                                        |
| ---------------------------------- | ------------------------------------------------------------ | ---------------------------------------- | -------------------------------------------------- | ------------------------------------------------------------ | --------------------------- | ------------------------------------------------------------ |
| `secrets.json`                     | `Oidc:Authority`                                             | (HTTPS Url)                              | `standfast-<env>-vars` group                       | `a_OidcAuthority`                                            | (HTTPS Url)                 | Your provider's issuer URL, for example `https://yourbusiness.kinde.com`. Every endpoint is read from its discovery document, so none is configured individually. |
| `secrets.json`                     | `Oidc:ClientId`                                              | (Client ID)                              | `standfast-<env>-vars` group                       | `a_OidcClientId`                                             | (Client ID)                 | Confidential client id from the provider.                    |
| `secrets.json`                     | `Oidc:ClientSecret`                                          | (Client Secret)                          | `standfast-<env>-vars` group                       | `a_OidcClientSecret`                                         | (Client Secret)             | Client secret from the provider. Mark the group variable as secret. Bicep always stores it in Key Vault as `oidc-client-secret` and gives the container app a Key Vault reference, so the value never becomes a plain environment variable. |
| `secrets.json`, optional           | `StandFastUi:DisplayTimeZoneId`                              | (e.g., `Central Standard Time`)          | `standfast-<env>-vars` group                       | `a_DisplayTimeZoneId`                                        | (e.g., `Central Standard Time`) | The [time zone id](#time-zones) the board resolves "today" in. The group variable must exist, because the pipeline passes it on every run; leave its value empty and the app falls back to the server time zone, which in the container is UTC. Locally, omitting it falls back to your machine's zone. |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_CustomDomain`                                             | (e.g., `standup.yourbusiness.com`) | The [custom domain](#custom-domain) the app answers on. The group variable must exist, because the pipeline passes it on every run; leave its value empty and the app is reachable only on its generated Container Apps URL. |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_AllowedBranches`                                          | (e.g., `main\|release/`) | Pipe-delimited list of the branches that may release to this environment; see [Branch filtering](#branch-filtering). Leave it unset and every branch that triggers the pipeline releases here, which for a production environment is rarely what you want. |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_RegionToken`                                              | (e.g., `usnorth`)           | Region segment of every resource name                        |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-<env>-vars` group                       | `a_Location`                                                 | (e.g., `northcentralus`)    | Azure region everything is created in                        |
| (n/a)                              | (n/a)                                                        | (n/a)                                    | `standfast-vars` group                             | `a_AppBase`                                                  | `standfast`                 | First segment of every resource name and the `Product` tag. Identical across environments |
| `secrets.json`                     | `AzureTableStorage:ConnectionString`                         | `UseDevelopmentStorage=true` for Azurite | (n/a)                                              | (n/a)                                                        | (n/a)                       | Table storage for data records. Required locally: no committed file sets it, and startup fails unless either this or `ServiceUri` is present. Left empty in the cloud, where `ServiceUri` and the managed identity are used instead. |
| `appsettings.Development.json`     | `AzureTableStorage:TablePrefix`                              | `StandFastDev`                           | (n/a)                                              | (n/a)                                                        | (n/a)                       | Prefix on every table name, so one storage account can hold several environments. Committed, and overridable in `secrets.json` if you want your own. Letters and digits only, starting with a letter, 21 characters at most. Set by Bicep in the cloud. |
| `appsettings.json`, optional       | `AzureTableStorage:CreateTablesOnStartup`                    | `true`                                   | `appsettings.json`, optional                       | `AzureTableStorage:CreateTablesOnStartup`                    | `true`                      | Already `true` in the committed file, so there is nothing to do unless you are turning it off where the identity has no table-create rights. |
| `appsettings.json`, optional       | `BoardLock:GraceMinutes`                                     | `60`                                     | `standfast-<env>-vars` group, optional             | `a_BoardLockGraceMinutes`                                    | (e.g., `90`)                | Minutes after a day's last turn within which locking its board records the moment you pressed Lock. Past it the lock is dated from that turn instead. The committed value applies everywhere; define the group variable only in an environment that wants its own, since an undefined one leaves the shipped value alone. |
| `appsettings.json`, optional       | `BoardLock:MinutesAfterLastTurn`                             | `5`                                      | `standfast-<env>-vars` group, optional             | `a_BoardLockMinutesAfterLastTurn`                            | (e.g., `10`)                | Minutes added to the day's last turn to date a lock applied past the grace window, which is what a board locked the next morning carries. Same rule: the group variable is optional. |
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

Eight tables, all prefixed with `AzureTableStorage:TablePrefix`:

| Table | Partition key | Row key | Holds |
| ----- | ------------- | ------- | ----- |
| `People` | `Person` | Person id | The directory. One partition because it is small and always listed whole. |
| `Standups` | `Standup` | Standup id | Meeting definitions. Same reasoning. |
| `StandupMembers` | Standup id | Person id | The Presenter Roster. One partition per standup, which is exactly how the board reads it. The People screen's role columns are the one query that crosses partitions; see below. |
| `StandupLeaders` | Standup id | Person id | The Leader Roster, in the same shape. A role gets its own table rather than a column on `StandupMembers`, so the person id stays the whole row key and one person can hold both roles on one standup. |
| `StandupMeetings` | Standup id | Meeting date | What is recorded about a standup on one date apart from any participant: who led it, and when the date was locked. A row exists only once something has been set, so a date with no row is a meeting nobody annotated. |
| `StandupEntries` | Standup id + person id | Inverted meeting date | Attendance state, the update, and the blockers. |
| `UserPreferences` | `UserPreferences` | OpenID Connect subject | One row per signed-in user, holding their chosen appearance and the address the provider reports for them. Always read one user at a time, so one partition and a point read. Outside backup and restore; see [Backup and restore](#backup-and-restore). |
| `AuditLog` | Date bucket | Timestamp | Written by Serilog, not by the repositories. Outside backup and restore; see [Backup and restore](#backup-and-restore). |

Settings are keyed by the provider's subject identifier rather than by a person record, because signing in and being on a roster are independent: anyone who can sign in gets settings, whether or not they appear on a board. That identifier comes from outside the app, so `StorageKeys` folds the characters Azure Table keys reject onto the separator and the row keeps the original value in its own column.

The address stored beside those settings is what bridges the two sides. A signed-in user and a person on a roster are separate records with nothing in common but an email address, so the address the provider reports is written through on every sign-in that changes it, and the board marks the card whose person record carries the same one. `EmailAddress` holds the stored form and the comparison, so a roster address and a claim are normalised and matched on the same terms. Nothing matches when either side has no address, which is what makes an anonymous session mark nobody.

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

"Which dates did this standup finish on" is answered from the same keys without touching a partition directly. Entry partition keys are the standup id followed by the person id, so bracketing the person half with the all-zero and all-ones guids covers exactly one standup's participants, and the inverted row keys bound the date range. Both keys are therefore constrained, the query reads one standup's slice of one week, and only the date column comes back. This is what marks the week strip.

The presenting order report reads the same bounded range with a longer span and one more column, returning who presented and when rather than only the dates. Both queries build their filter from one place, so the range and the "has presented" test cannot drift apart. A year of a twenty-person standup is a few thousand rows of three columns, which is one query rather than the per-person round trips the board makes, because a report wants the whole slice at once while the board wants two rows per person.

"Which standups is this person on" is the one question the key design cannot answer from a partition, because memberships are partitioned by standup. The People screen gets it from a single unfiltered query over each role's membership table, which is a table scan apiece. That is deliberate: a membership table holds one small row per person per standup, so scanning it costs less than maintaining a second index written on every roster change, and the alternative of one partition query per standup trades a scan for N round trips.

Everything hanging off a standup uses that standup's id as its partition key, in `StandupMembers`, `StandupLeaders` and `StandupMeetings` alike. Deleting a standup therefore clears three named partitions and nothing has to be searched for.

A date's lock is the timestamp on its meeting row and nothing else: there is no boolean beside it that could disagree, and when a standup closed is worth keeping on its own. The board disables its controls as soon as it knows about a lock, but `BoardService` is where the lock actually holds — every attendance tap, saved update and leader change reads the meeting row first and refuses a locked date, so a screen opened before somebody else locked it cannot write through. The board catches that refusal and reloads rather than dropping the circuit. What time the lock records is a rule of its own in `BoardLockPolicy`, in the Domain layer, with the two windows supplied from configuration. One range query over the meetings partition, bounded by the two date keys, serves both the week strip's orange dots and the activity report's Status column; it is the same shape as the query behind the green dots and reads one standup's slice of the period.

### Why Table Storage and not SQL

Agreed, and for more reasons than cost. Every read this app performs is either "give me a small list" or "give me this participant's last two rows", which is exactly the access pattern Table Storage is good at. There are no joins, no reporting queries, no referential integrity worth enforcing in the database, and the write volume is a few rows per person per day. Azure SQL or PostgreSQL would add a server to size, patch, back up, and pay for around the clock, in exchange for query capabilities this app never uses.

Two caveats worth knowing before the design ossifies:

- **No cross-table transactions.** Deleting a standup removes its rosters and its meetings in a loop, not atomically. If that matters later, move them into the standup partition so the delete becomes one batch.
- **No ad-hoc queries.** Reports work only where the keys already bound the answer. The presenting order report does, because it asks for one standup over a date range and both halves of the key constrain that. "Show me everyone who was blocked in August" does not: it filters on a column, which means a scan or a second index table written at the same time as the entry. A report of that shape is the point to revisit the store, and moving to SQL then is a contained change because the repository interfaces are the only seam that would move.

## Markdown editing

`MarkdownEditor` is one component used by all three boxes on the update panel, so the toolbar, the preview toggle, and the character limit cannot drift apart. The toolbar itself is data: `MarkdownCommands.All` is a list of records describing each button, and the component renders whatever is in that list.

Formatting runs through a small JavaScript helper, because wrapping a selection needs the caret position and the browser owns that. The helper computes the new text and hands it back to Blazor, which remains the owner of the value. Inline commands toggle: pressing bold on already-bold text unwraps it.

Rendering uses a single pre-built Markdig pipeline with advanced extensions on and raw HTML disabled. Update text is user-supplied and rendered into the page, so HTML is escaped rather than executed.

## Charting

Report charts are SVG written by the component rather than a charting package. The presenting order chart needs an axis whose last tick is a symbol instead of a number, and a separate dash pattern per series; both are a few lines of geometry to draw and a fight to configure. `ChartGeometry` holds every pixel position and nothing else, so the markup reads positions rather than computing them and the layout can be checked on its own.

A chart fills the width the window gives it. The dates are spread across whatever space is reported, down to a floor of 34 pixels apart, which is where their rotated labels would start to overlap; a period long enough to hit that floor grows past the window and scrolls sideways instead. Only the browser knows how much room there is, so `chart-resize.js` reports the width on the first paint and on every resize after it, and the component rebuilds its geometry from the number. The chart draws at the floor until the first measurement arrives, which is what the prerendered HTML carries.

`SeriesStyles` assigns each series its colour and dash pattern. The eight hues are a palette validated for separation under simulated protanopia and deuteranopia and for lightness in both themes; the slot order is what makes neighbouring hues separable, so slots are not reordered. Series fill every hue at one dash pattern before moving to the next, which keeps a chart of eight or fewer people entirely in solid lines and guarantees a repeated colour always arrives with a different line.

Three of the light steps and two of the dark ones fall below a 3:1 contrast ratio against the surface they are drawn on. The relief for that is the table of order numbers beneath the chart, which is why it is there rather than as a convenience.

Series colours are picked in C# from the signed-in user's appearance setting, not by a stylesheet, because MudBlazor switches themes by rewriting its palette variables rather than by a selector a media query could match. Everything else the chart draws — grid, axis text, the ring around a marker — uses the MudBlazor palette variables and follows the theme on its own.

## Auditing and logging

Serilog handles both, separated by a property rather than by a second logger. `IAuditLog.Record` opens a logging scope containing `IsAuditEvent` plus the actor, the target, and the event name; a Serilog sub-logger filters on that property and writes those events to the `AuditLog` table with the audit properties promoted to real columns. Ordinary application logs go to the console, which is what Container Apps forwards to Log Analytics.

The actor comes from `ICurrentUser`, an Application-layer abstraction implemented in the UI over the signed-in principal, so the Application layer attributes an action without referencing ASP.NET Core.

Audit event names live in `AuditEvents` so a later report reads the same constants the writers use.

## Backup and restore

The Backup screen downloads every application table as one JSON file and restores one back, replacing all current data. Two tables are in neither direction. The audit log is out because Serilog owns it, it is the record of who changed what, and restoring an older copy over it would erase the trail explaining the restore itself. User settings are out because they belong to the people using the app rather than to the board data, so restoring last month's copy of the board leaves everyone's own settings alone. `StorageNames.DataTables` is the single list of what a backup covers; `StorageNames.AllTables` adds the two it leaves out, and the Azurite fixture cleans up from that.

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

Authorization code flow with PKCE against the provider's discovery document, with the session held in a cookie. A fallback authorisation policy requires an authenticated user, so a new page is protected unless it opts out; the health endpoint, the two auth endpoints and the static assets are the deliberate exceptions.

The static assets matter more than they look. `MapStaticAssets` registers every stylesheet, script and Blazor framework file as an endpoint, and the fallback policy applies to endpoints, so without an explicit opt-out the browser is sent to the identity provider to fetch a CSS file. Nothing there is secret, and an asset request cannot complete an interactive redirect.

Nothing in the code names a provider. `AuthenticationSetup` reads an `Authority`, a client id and a secret, and discovers every endpoint from `{Authority}/.well-known/openid-configuration`, so swapping providers is a configuration change. StandFast is configured against Kinde.

Inbound claim mapping is switched off, so claims stay under their OIDC names: `sub` is the audit actor and the key user settings are stored under, `email` is what ties a signed-in user to their person record, and `name` is for display. The legacy SOAP claim URIs never appear. `UserClaims` is the only place a claim name is read.

Where that principal comes from depends on what is running. `ICurrentUser` reads the HTTP context, which is what an audited service call has. An interactive Blazor circuit has no HTTP context, so a component that needs the signed-in user takes the cascading authentication state instead; the main layout does exactly that. It loads the settings once, on the prerender and again on the circuit, and cascades them to every page, so a screen that marks the reader's own row does not fetch them for itself.

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

`Dockerfile` restores the project files in their own layer so a source-only change keeps the cached restore, and then publishes with the sources in place, restoring a second time. That second restore is what makes the image correct: a restore evaluated before the Razor components exist settles the project's static web assets without Blazor's framework files among them, and a publish reusing that result omits `wwwroot/_framework`. Such an image serves every page, stylesheet and package asset while returning 404 for `blazor.web.js`, which renders the app and leaves it unable to respond to a click. Nothing else about it looks wrong, which is why the build asserts that file exists.

What you set up once in Azure DevOps is in [Configuration](#configuration).

### Versions and branches

`VersionPrefix` in `Directory.Build.props` is the only version. The About page shows it as `MM.mm`, plus `.pp` once a patch exists (`01.00.01`).

| Branch | Holds | Created from | Merges into by pull request |
| ------ | ----- | ------------ | --------------------------- |
| `feature/*` | One change | The release branch it targets | `release/MM.mm` |
| `release/MM.mm` | One version, e.g. `release/01.00`, with `VersionPrefix` matching; each fix after release raises the patch | `main` | `main`, once released |
| `main` | The newest release | | |

`main` and `release/*` accept changes only by pull request and cannot be force-pushed or deleted, per `.github/rulesets/protected-branches.json`, imported once under **Settings → Rules → Rulesets → New ruleset → Import a ruleset**. Pull requests into either run build and test only.

### Branch filtering

Which branches may release to an environment is `a_AllowedBranches` in that environment's variable group, so tightening or loosening it is an edit in Azure DevOps rather than a pipeline change, and a new environment brings its own answer with it. The release stage's `condition` reads it directly; there is no extra stage, job or script behind it.

Write the branches as a pipe-delimited list, without the `refs/heads/` prefix:

| You write | It matches |
| --------- | ---------- |
| `main` | That branch. |
| `main\|release/` | `main`, and every branch under `release/`, such as `release/01.00`. Matching ignores case. |
| (unset or empty) | Every branch that triggers the pipeline. |

### Custom domain

`a_CustomDomain` in the environment's variable group is the domain the app answers on. The Bicep binds it to the container app's ingress, which is what keeps it in place: a deployment rewrites the app's whole ingress configuration, so a domain bound by hand in the portal lasts only until the next release.

The certificate is a free Azure managed certificate. Before deploying the app, the release stage reads the environment's certificates, both uploaded and managed, and passes the id of the one whose subject is the domain, so an environment that already has a certificate keeps it rather than collecting a second one.

Finding none, the stage deploys twice: once binding the domain with no certificate on it, then again to issue `<AppBase><Env><Region>mc` and bind it. Azure issues a managed certificate only for a hostname that an app in the environment already carries, so there is no single pass that can do both. Later releases find that certificate and deploy once. It renews itself.

Two DNS records at your registrar have to exist before the domain is set, because the certificate is issued only after Azure resolves them:

| Record | Name | Value |
| ------ | ---- | ----- |
| `CNAME` | The subdomain, for example `standup` | The app's generated host, `<app>.<region>.azurecontainerapps.io` |
| `TXT` | `asuid.` plus the subdomain, for example `asuid.standup` | The domain verification id, which the release stage prints at the end of its log |

The order for a new environment is therefore: release once with `a_CustomDomain` empty, take the generated URL and the verification id from the log, create the two records, then set `a_CustomDomain` and release again.

Once the domain is set, the release log prints the callback URLs on the domain rather than the generated host, and those are the ones to register with the identity provider. The generated host keeps working, so a sign-in attempted there fails at the provider rather than at the app.

# 🚧 Change Summary

*Each entry is a specific version (release/\* branch), in descending order (newest version up top), with a plain bullet list summarizing each change without technical jargon.*

### v01.00 — 2026-09-15

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
- Fixed the custom web address being lost every time the app was released: it is now part of the deployment itself, and the release reuses the security certificate the site already has instead of replacing it.
- Added a Backup screen that downloads everything the app holds as a single file, and restores one back by replacing all current data. The audit log is left out of both, so restoring an old backup never wipes the record of who changed what.
- Fixed the repository's ignore rules, which were quietly leaving the new backup source folders out of version control.
- Added an F5 launch configuration and build/test tasks for the editor, with Hot Reload switched off to stop a debugger error appearing in the Debug Console on every run.
- Coloured the notes icon on a board card once that person has an update recorded for the date, so it is obvious who still owes one, with the outline on the card left to mark whose panel is open.
- Changed Cancel on the update panel to always be available and to close the panel, asking first whether unsaved edits should be lost.
- Corrected the configuration table against what the code and the deployment actually do, and split out a short list of the values that are filled in for you.
- Fixed the build pipeline failing every storage integration test: the check for whether the emulator is running threw instead of answering on an agent that has no emulator, so the tests reported as failures rather than skipping as intended.
- Moved the decision about which branches may deploy to an environment out of the pipeline and into that environment's variable group, as a pipe-delimited list of branch names. Leaving it unset lets any branch that triggers the pipeline deploy there.
- Fixed the deployment creating its resource group under the wrong name, so it is now named for the application, environment and region rather than the application alone.
- Documented the one permission the deployment principal needs beyond Contributor, without which the first deployment fails partway through with an authorization error.
- Stopped requiring a signed-in user for stylesheets, scripts and the Blazor framework files, which were being sent through the identity provider like any page.
- Added a site icon, so the browser stops asking for one that was never there and reporting it as a missing file.
- Made the light and dark mode choice stick to the person who made it, so it comes back the next time they sign in on any browser or machine. Anyone who has never chosen gets light.
- Marked your own card on the board with a dark yellow star after your name, so you can pick yourself out of a long roster at a glance. The app remembers the address you sign in with, keeps it current if it changes at the identity provider, and matches it against your person record.
- Made the notes icon red on any card with an update recorded, in all three columns, so who has already been captured stands out from the green used elsewhere.
- Marked the dates in the week strip that someone presented on, so a day with a finished standup can be spotted without opening it.
- Fixed the deployed app loading but never responding to a click: the container build was leaving Blazor's startup script out of the published output, so the browser asked for a file that was not there. The build now also checks the script is present and fails rather than shipping an app that cannot work.
- Added a Reports screen with a dropdown for choosing which report to show, a picker for the standup, and quick-pick buttons for the last 30, 60, 90 or 180 days or the last year.
- Added the first report, Presenting Order vs Date: a line per person showing the turn they took at each standup over the period, with a dot on the days they presented and ∞ along the bottom for the days they did not. Everyone gets their own colour, and once the colours run out they come back as dashed lines so no two people ever look alike. The same numbers are also available as a table under the chart.
- Made a report chart spread across the full width of the browser window and follow it as the window is resized, packing the dates only as tightly as their labels allow before scrolling sideways instead.
- Gave each standup a second roster of the people who may run it, alongside the roster of the people who present. The Standups screen has a button for each, and someone can be on both.
- Renamed the People table's Standups column to Presenters and added a Leaders column beside it, so each person's two kinds of involvement read separately.
- Added a Leader dropdown to the board, beside the standup picker, for recording who ran the standup on the day being viewed. It offers that standup's Leader Roster, applies to that date alone, and can be left empty.
- Added a second report, Standup Activity: a table of the days the standup actually ran, newest first, with the weekday, whether the day is locked or still open, how many people presented, and how many minutes ran from the first person's turn to the lock.
- Turned the week strip's dot orange on a locked day, leaving it green on a day that is finished but still open, so a week shows at a glance which days are closed.
- Greyed out the Lock button until somebody has presented on the date, since a day nobody has spoken on has nothing to close.
- Changed the "can be called on" column to list people alphabetically, the same way the roster does, instead of by who arrived first, so a name sits in the same place in both columns.
- Added a Lock button beside the date on the board, which closes that day's standup so nothing on it can be changed by accident. A locked day still reads in full; unlocking asks first. Locking during or shortly after the standup records the time you pressed it, while locking much later records the last person who presented plus a few minutes, so a day closed the next morning does not read as though the meeting ran that long. How long "shortly after" is, and how many minutes get added, are both settings each environment can change.
- Added an About page to the menu, showing the app icon, a summary of what StandFast is for, and a link to its source repository.
- Added a version number, starting at 01.00, shown on the About page.
- Set out how versions map to release branches, and made the main and release branches accept changes only through pull requests.
- Pull requests into a release branch now get the same build and test check as pull requests into main, and pull request checks never deploy.
- Allowed a folder such as `release/` in the list of branches that may deploy to an environment, so every release branch under it qualifies without being added one at a time.
