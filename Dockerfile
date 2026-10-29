FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OficinaBilling.Domain/*.csproj src/OficinaBilling.Domain/
COPY src/OficinaBilling.Application/*.csproj src/OficinaBilling.Application/
COPY src/OficinaBilling.Infrastructure/*.csproj src/OficinaBilling.Infrastructure/
COPY src/OficinaBilling.API/*.csproj src/OficinaBilling.API/
RUN dotnet restore src/OficinaBilling.API/OficinaBilling.API.csproj

COPY src/ src/
RUN dotnet publish src/OficinaBilling.API/OficinaBilling.API.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Agente .NET do New Relic instalado na imagem; só é ativado quando o deployment define
# CORECLR_ENABLE_PROFILING=1 (ver k8s/base/deployment.yaml). Local/dev roda sem o agente.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl gnupg ca-certificates \
 && curl -fsSL https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public | gpg --dearmor -o /usr/share/keyrings/newrelic.gpg \
 && echo 'deb [signed-by=/usr/share/keyrings/newrelic.gpg] http://apt.newrelic.com/debian/ newrelic non-free' > /etc/apt/sources.list.d/newrelic.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends newrelic-dotnet-agent \
 && apt-get purge -y gnupg && apt-get autoremove -y \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080 \
    NEW_RELIC_LOG_DIRECTORY=/tmp/newrelic
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "OficinaBilling.API.dll"]
