FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
RUN apt-get update &&\
    apt-get install -y iputils-ping net-tools curl && apt-get clean

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /workspace
COPY "./src/WildGoose/WildGoose.csproj" "/workspace/src/WildGoose/"
COPY "./src/WildGoose.Application/WildGoose.Application.csproj" "/workspace/src/WildGoose.Application/"
COPY "./src/WildGoose.Domain/WildGoose.Domain.csproj" "/workspace/src/WildGoose.Domain/"
COPY "./src/WildGoose.Infrastructure/WildGoose.Infrastructure.csproj" "/workspace/src/WildGoose.Infrastructure/"
COPY "./src/WildGoose.Tests/WildGoose.Tests.csproj" "/workspace/src/WildGoose.Tests/"
COPY "./WildGoose.sln" "/workspace/WildGoose.sln"
RUN dotnet restore .
COPY . .
RUN dotnet build

FROM build AS publish
RUN dotnet publish  -o /app/publish

FROM base AS final
WORKDIR /app
COPY docker-entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh
COPY --from=publish /app/publish .
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "WildGoose.dll"]
