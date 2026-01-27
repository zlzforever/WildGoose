

[![Backend Docker Image CI](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml/badge.svg)](https://github.com/zlzforever/WildGoose/actions/workflows/backend.yml)
###


```
cd src/WildGoose
dotnet ef migrations add Init  -p ../WildGoose.Infrastructure
dotnet ef migrations add AddNIdToOrg  -p ../WildGoose.Infrastructure
 
```

``` 
更新后，检查设置机构管理员后， 角色是否正确增加了
锁定功能是否正常
用户名、邮件、电话不得重复
```
