# syntax=docker/dockerfile:1

# Build and publish the Blazor Server app, then run it on the chiseled ASP.NET runtime.
# The "extra" chiseled variant is required: the app uses ICU and the tz database for time zone aware meeting dates, which the plain chiseled image omits.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
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

# blazor.web.js is contributed by the SDK rather than by this repository, so the floating sdk tag decides whether it lands in the publish output. An
# image without it starts and serves pages, and the only symptom is a 404 in the browser console and an app with no interactivity, so fail here
# instead. The SDK version is printed because it is the variable that makes this differ between a developer machine and the build agent.
RUN dotnet --version && ls -l /app/publish/wwwroot/_framework \
 && test -f /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app

# Container Apps ingress talks plain HTTP to the container and terminates TLS itself, so the app listens on a single HTTP port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "StandFast.Ui.dll"]
