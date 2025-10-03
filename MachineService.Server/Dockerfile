FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim-amd64 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
COPY "." "/src"
WORKDIR "/src"
RUN dotnet restore "./MachineService.Server/MachineService.Server.csproj"
RUN dotnet run --project "./MachineService.Server/MachineService.Server.csproj" --configuration $BUILD_CONFIGURATION -- codegen
RUN dotnet clean "./MachineService.Server/MachineService.Server.csproj" --configuration $BUILD_CONFIGURATION
RUN dotnet restore "./MachineService.Server/MachineService.Server.csproj"
RUN dotnet build "./MachineService.Server/MachineService.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build -r linux-x64

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MachineService.Server/MachineService.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -r linux-x64

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ARG GIT_COMMIT_VERSION
ENV ENVIRONMENT__GIT_COMMIT_VERSION=$GIT_COMMIT_VERSION
ENV ENVIRONMENT__PRECOMPILEDDBCLASSES=true
ENTRYPOINT ["dotnet", "MachineService.Server.dll"]