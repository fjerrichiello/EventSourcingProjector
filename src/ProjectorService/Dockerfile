FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Common/Common.csproj src/Common/
COPY src/DbMigrator/DbMigrator.csproj src/DbMigrator/
COPY src/ProjectorService/ProjectorService.csproj src/ProjectorService/
RUN dotnet restore src/ProjectorService/ProjectorService.csproj
COPY src/Common/ src/Common/
COPY src/DbMigrator/ src/DbMigrator/
COPY src/ProjectorService/ src/ProjectorService/
RUN dotnet publish src/ProjectorService/ProjectorService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectorService.dll"]
