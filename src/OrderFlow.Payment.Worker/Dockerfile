FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ src/
RUN dotnet restore "src/OrderFlow.Payment.Worker/OrderFlow.Payment.Worker.csproj"
WORKDIR "/src/src/OrderFlow.Payment.Worker"
RUN dotnet build "OrderFlow.Payment.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.Payment.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Payment.Worker.dll"]
