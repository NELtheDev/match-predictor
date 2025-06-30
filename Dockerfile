FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MatchPredictor.Web/MatchPredictor.Web.csproj", "MatchPredictor.Web/"]
COPY ["MatchPredictor.Domain/MatchPredictor.Domain.csproj", "MatchPredictor.Domain/"]
COPY ["MatchPredictor.Infrastructure/MatchPredictor.Infrastructure.csproj", "MatchPredictor.Infrastructure/"]
COPY ["MatchPredictor.Application/MatchPredictor.Application.csproj", "MatchPredictor.Application/"]

RUN dotnet restore "MatchPredictor.Web/MatchPredictor.Web.csproj"

COPY . .
WORKDIR "/src/MatchPredictor.Web"
RUN dotnet build "./MatchPredictor.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MatchPredictor.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

# Bind to 0.0.0.0 and support PORT override (for Render)
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}
EXPOSE 10000

ENTRYPOINT ["dotnet", "MatchPredictor.Web.dll"]
