#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["ExchangeAdapterService/ExchangeAdapterService.csproj", "ExchangeAdapterService/"]
COPY ["Bottrader.Shared/Bottrader.Shared.csproj", "Bottrader.Shared/"]
RUN dotnet restore "ExchangeAdapterService/ExchangeAdapterService.csproj"
COPY . .
WORKDIR "/src/ExchangeAdapterService"
RUN dotnet build "ExchangeAdapterService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExchangeAdapterService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExchangeAdapterService.dll"]
