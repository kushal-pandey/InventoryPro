FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ./InventoryPro.sln

RUN dotnet publish ./src/InventoryPro.API/InventoryPro.API.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet InventoryPro.API.dll --urls http://0.0.0.0:${PORT:-10000}"]
