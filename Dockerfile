# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from just the project files, so editing source later doesn't invalidate this layer.
COPY global.json Directory.Build.props ./
COPY PlanningPoker.Infrastructure/PlanningPoker.Infrastructure.csproj PlanningPoker.Infrastructure/
COPY PlanningPoker.Client/PlanningPoker.Client.csproj PlanningPoker.Client/
COPY PlanningPoker.Api/PlanningPoker.Api.csproj PlanningPoker.Api/
RUN dotnet restore PlanningPoker.Api/PlanningPoker.Api.csproj

COPY PlanningPoker.Infrastructure/ PlanningPoker.Infrastructure/
COPY PlanningPoker.Client/ PlanningPoker.Client/
COPY PlanningPoker.Api/ PlanningPoker.Api/

# Publishing the Api project transitively publishes the Blazor Client's static assets into its
# output (the Api -> Client ProjectReference), so there is no separate Client publish step.
RUN dotnet publish PlanningPoker.Api/PlanningPoker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --uid 5678 --user-group --shell /usr/sbin/nologin appuser
COPY --from=build /app/publish .
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlanningPoker.Api.dll"]
