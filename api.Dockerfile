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
RUN dotnet restore src/WildGoose/WildGoose.csproj
COPY ./src/WildGoose /workspace/src/WildGoose
COPY ./src/WildGoose.Application /workspace/src/WildGoose.Application
COPY ./src/WildGoose.Domain /workspace/src/WildGoose.Domain
RUN dotnet build src/WildGoose/WildGoose.csproj --no-restore -c Release

FROM build AS publish
RUN dotnet publish src/WildGoose/WildGoose.csproj --no-restore -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY docker-entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/docker-entrypoint.sh
COPY --from=publish /app/publish .
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "WildGoose.dll"]
