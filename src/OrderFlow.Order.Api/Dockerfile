FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ src/
RUN dotnet restore "src/OrderFlow.Order.Api/OrderFlow.Order.Api.csproj"
WORKDIR "/src/src/OrderFlow.Order.Api"
RUN dotnet build "OrderFlow.Order.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderFlow.Order.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.Order.Api.dll"]
