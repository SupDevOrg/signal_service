FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем ТОЛЬКО csproj сначала (для кэширования слоя restore)
COPY ["signal_service.csproj", "./"]

# Восстанавливаем пакеты с флагами для CI/CD среды
RUN dotnet restore "signal_service.csproj" --disable-parallel --force-evaluate --verbosity minimal

# Копируем весь исходный код
COPY . .

# Публикуем
RUN dotnet publish "signal_service.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "signal_service.dll"]