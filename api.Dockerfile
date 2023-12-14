FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /workspace
COPY "./src/WildGoose/WildGoose.csproj" "./src/WildGoose/"
COPY "./src/WildGoose.Application/WildGoose.Application.csproj" "./src/WildGoose.Application/"
COPY "./src/WildGoose.Domain/WildGoose.Domain.csproj" "./src/WildGoose.Domain/"
COPY "./src/WildGoose.Infrastructure/WildGoose.Infrastructure.csproj" "./src/WildGoose.Infrastructure/"
COPY "./src/WildGoose.Tests/WildGoose.Tests.csproj" "./src/WildGoose.Tests/"
COPY "./WildGoose.sln" "./WildGoose.sln"
RUN dotnet restore .
COPY . .
RUN dotnet build -c Release

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WildGoose.dll"]
