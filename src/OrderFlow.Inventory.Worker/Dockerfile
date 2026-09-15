FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ src/
RUN dotnet restore "src/OrderFlow.Inventory.Worker/OrderFlow.Inventory.Worker.csproj"
WORKDIR "/src/src/OrderFlow.Inventory.Worker"
RUN dotnet build "OrderFlow.Inventory.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.Inventory.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Inventory.Worker.dll"]
