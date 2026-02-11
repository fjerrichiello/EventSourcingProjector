FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Common/Common.csproj src/Common/
COPY src/RequestApi/RequestApi.csproj src/RequestApi/
RUN dotnet restore src/RequestApi/RequestApi.csproj
COPY src/Common/ src/Common/
COPY src/RequestApi/ src/RequestApi/
RUN dotnet publish src/RequestApi/RequestApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RequestApi.dll"]
