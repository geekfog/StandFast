# syntax=docker/dockerfile:1

# Build and publish the Blazor Server app, then run it on the chiseled ASP.NET runtime.
# The "extra" chiseled variant is required: the app uses ICU and the tz database for time zone aware meeting dates, which the plain chiseled image omits.
#
# The SDK is pinned to an exact patch and must stay equal to the version in global.json. The floating "10.0" tag moved to 10.0.401, whose publish emits
# no wwwroot/_framework at all, which ships an app that renders and then 404s its own startup script. The runtime tag stays floating so the container
# keeps picking up runtime security patches, which do not affect what publish produces.
ARG SDK_VERSION=10.0.400
ARG RUNTIME_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${SDK_VERSION} AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Project files are restored on their own so a source-only change does not invalidate the cached restore layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/StandFast.Domain/StandFast.Domain.csproj src/StandFast.Domain/
COPY src/StandFast.Application/StandFast.Application.csproj src/StandFast.Application/
COPY src/StandFast.Infrastructure/StandFast.Infrastructure.csproj src/StandFast.Infrastructure/
COPY src/StandFast.Ui/StandFast.Ui.csproj src/StandFast.Ui/
RUN dotnet restore src/StandFast.Ui/StandFast.Ui.csproj

COPY src/ src/
RUN dotnet publish src/StandFast.Ui/StandFast.Ui.csproj --configuration ${BUILD_CONFIGURATION} --no-restore --output /app/publish

# blazor.web.js comes from the SDK rather than from this repository, so the SDK version decides whether it lands in the publish output. An image
# without it starts and serves pages, and the only symptom is a 404 in the browser console and an app that never responds to a click. This guard is
# what caught that, so it stays: it turns a silently broken image into a failed build the next time an SDK changes this.
RUN dotnet --version && ls -l /app/publish/wwwroot/_framework \
 && test -f /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:${RUNTIME_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app

# Container Apps ingress talks plain HTTP to the container and terminates TLS itself, so the app listens on a single HTTP port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "StandFast.Ui.dll"]
