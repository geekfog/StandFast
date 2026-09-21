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

# The publish restores with the sources in place. A restore evaluated before the Razor components exist settles the project's static web assets
# without Blazor's framework files among them, and a publish reusing that result omits wwwroot/_framework: the image then serves every page,
# stylesheet and package asset while returning 404 for blazor.web.js, which renders the app and leaves it unable to respond to a click. The restore
# layer above is still worth its cache, because the packages it fetched are already in the image.
RUN dotnet publish src/StandFast.Ui/StandFast.Ui.csproj --configuration ${BUILD_CONFIGURATION} --output /app/publish

# Everything about such an image looks correct until a browser console is open, so its startup script is asserted to exist here.
RUN test -f /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app

# Container Apps ingress talks plain HTTP to the container and terminates TLS itself, so the app listens on a single HTTP port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "StandFast.Ui.dll"]
