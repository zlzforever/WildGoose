

[![Backend Docker Image CI](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml/badge.svg)](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml)
###


```
cd src/WildGoose
dotnet ef migrations add Init  -p ../WildGoose.Infrastructure
dotnet ef migrations add AddOrgMetadata  -p ../WildGoose.Infrastructure
dotnet ef migrations add AddUserDepartureTime  -p ../WildGoose.Infrastructure
```
