FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OficinaBilling.Domain/*.csproj src/OficinaBilling.Domain/
COPY src/OficinaBilling.Application/*.csproj src/OficinaBilling.Application/
COPY src/OficinaBilling.Infrastructure/*.csproj src/OficinaBilling.Infrastructure/
COPY src/OficinaBilling.API/*.csproj src/OficinaBilling.API/
RUN dotnet restore src/OficinaBilling.API/OficinaBilling.API.csproj

COPY src/ src/
RUN dotnet publish src/OficinaBilling.API/OficinaBilling.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OficinaBilling.API.dll"]
