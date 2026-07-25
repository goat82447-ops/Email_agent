# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY InboxAgent.csproj ./
RUN dotnet restore InboxAgent.csproj

COPY . ./
RUN dotnet publish InboxAgent.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

ENV DOTNET_ENVIRONMENT=Production

# Runs the web dashboard; Render provides the PORT env var automatically.
ENTRYPOINT ["dotnet", "InboxAgent.dll", "--web"]
