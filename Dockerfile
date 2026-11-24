FROM node:22-bookworm-slim AS frontend
WORKDIR /ui
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /source
COPY src/OutcomeGuard.Api/OutcomeGuard.Api.csproj src/OutcomeGuard.Api/
RUN dotnet restore src/OutcomeGuard.Api/OutcomeGuard.Api.csproj
COPY src/ src/
RUN dotnet publish src/OutcomeGuard.Api/OutcomeGuard.Api.csproj -c Release -o /app --no-restore
COPY --from=frontend /ui/dist /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app .
RUN mkdir -p /data && chown app:app /data
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV OutcomeGuard__DatabasePath=/data/outcomeguard.db
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OutcomeGuard.Api.dll"]

