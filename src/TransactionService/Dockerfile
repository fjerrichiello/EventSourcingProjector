FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Common/Common.csproj src/Common/
COPY src/DbMigrator/DbMigrator.csproj src/DbMigrator/
COPY src/TransactionService/TransactionService.csproj src/TransactionService/
RUN dotnet restore src/TransactionService/TransactionService.csproj
COPY src/Common/ src/Common/
COPY src/DbMigrator/ src/DbMigrator/
COPY src/TransactionService/ src/TransactionService/
RUN dotnet publish src/TransactionService/TransactionService.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TransactionService.dll"]
