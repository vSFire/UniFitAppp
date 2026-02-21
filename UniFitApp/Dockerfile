# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["UniFitApp/UniFitApp.csproj", "UniFitApp/"]
RUN dotnet restore "./UniFitApp/UniFitApp.csproj"
COPY . .
WORKDIR "/src/UniFitApp"
RUN dotnet build "./UniFitApp.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UniFitApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# === ДОБАВЛЕНО: Даем права нашему приложению на сохранение файлов ===
USER root
RUN mkdir -p /app/wwwroot/avatars && chown -R $APP_UID:$APP_UID /app/wwwroot
USER $APP_UID
# ======================================================================

ENTRYPOINT ["dotnet", "UniFitApp.dll"]