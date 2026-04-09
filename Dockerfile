FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["signal_service.csproj", "./"]
RUN dotnet restore "signal_service.csproj"

COPY . .

RUN dotnet publish "signal_service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN ls -la /app || true

ENTRYPOINT ["dotnet", "signal_service.dll"]