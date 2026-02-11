FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Common/Common.csproj src/Common/
COPY src/DbMigrator/DbMigrator.csproj src/DbMigrator/
RUN dotnet restore src/DbMigrator/DbMigrator.csproj
COPY src/Common/ src/Common/
COPY src/DbMigrator/ src/DbMigrator/
RUN dotnet publish src/DbMigrator/DbMigrator.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "DbMigrator.dll"]
