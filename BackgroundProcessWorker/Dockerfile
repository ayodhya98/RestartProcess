# Use the full ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BackgroundProcessWorker/BackgroundProcessWorker.csproj", "BackgroundProcessWorker/"]
RUN dotnet restore "BackgroundProcessWorker/BackgroundProcessWorker.csproj"

COPY . .
WORKDIR "/src/BackgroundProcessWorker"
RUN dotnet build "BackgroundProcessWorker.csproj" -c Debug -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "BackgroundProcessWorker.csproj" -c Debug -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BackgroundProcessWorker.dll"]
