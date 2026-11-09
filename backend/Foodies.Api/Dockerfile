# Build from the repository root:
# docker build -f backend/Foodies.Api/Dockerfile -t foodies-api:local .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Foodies.Api/Foodies.Api.csproj backend/Foodies.Api/
RUN dotnet restore backend/Foodies.Api/Foodies.Api.csproj
COPY backend/Foodies.Api/ backend/Foodies.Api/
RUN dotnet publish backend/Foodies.Api/Foodies.Api.csproj \
    --configuration Release --no-restore --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Media__StoragePath=/data/foodies/media \
    DataProtection__KeyPath=/data/foodies/keys \
    DevelopmentAccess__Enabled=false
RUN mkdir -p /data/foodies/media /data/foodies/keys \
    && chown -R app:app /data/foodies
COPY --from=build /publish/ ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=8s --start-period=30s --retries=3 \
    CMD ["dotnet", "Foodies.Api.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Foodies.Api.dll"]
