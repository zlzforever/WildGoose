FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["WildGoose/WildGoose.csproj", "WildGoose/"]
RUN dotnet restore "WildGoose/WildGoose.csproj"
COPY . .
WORKDIR "/src/WildGoose"
RUN dotnet build "WildGoose.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WildGoose.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WildGoose.dll"]
