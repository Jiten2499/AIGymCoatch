FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/SharedKernel/ src/SharedKernel/
COPY src/Domain/ src/Domain/
COPY src/Application/ src/Application/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Api/ src/Api/

RUN dotnet restore src/Api/LifeFitAI.Api.csproj
RUN dotnet publish src/Api/LifeFitAI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1

EXPOSE 8080

COPY --from=build /app/publish .
COPY docker/entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh \
    && chmod +x /entrypoint.sh \
    && mkdir -p /var/lifefit/dataprotection \
    && chown "$APP_UID:$APP_UID" /var/lifefit/dataprotection

ENTRYPOINT ["/entrypoint.sh"]
