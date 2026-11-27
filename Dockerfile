# One image: the React app built into the API's wwwroot, served by ASP.NET Core.

FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY Directory.Build.props Systematikken.slnx ./
COPY src/ src/
COPY --from=web /src/src/Systematikken.Api/wwwroot src/Systematikken.Api/wwwroot
RUN dotnet publish src/Systematikken.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# All state lives in /data: the database, the cookie keys and the nightly backups.
RUN mkdir -p /data && chown app:app /data
ENV ASPNETCORE_URLS=http://+:8080 \
    DataDirectory=/data \
    ASPNETCORE_ENVIRONMENT=Production
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Systematikken.Api.dll"]
