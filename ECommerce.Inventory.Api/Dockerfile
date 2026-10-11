FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommerce.Inventory.Api/ECommerce.Inventory.Api.csproj", "ECommerce.Inventory.Api/"]
COPY ["ECommerce.Security/ECommerce.Security.csproj", "ECommerce.Security/"]
COPY ["ECommerce.Inventory.Domain/ECommerce.Inventory.Domain.csproj", "ECommerce.Inventory.Domain/"]
COPY ["ECommerce.Inventory.Application/ECommerce.Inventory.Application.csproj", "ECommerce.Inventory.Application/"]
COPY ["ECommerce.Inventory.Infrastructure/ECommerce.Inventory.Infrastructure.csproj", "ECommerce.Inventory.Infrastructure/"]
RUN dotnet restore "ECommerce.Inventory.Api/ECommerce.Inventory.Api.csproj"
COPY . .
WORKDIR "/src/ECommerce.Inventory.Api"
RUN dotnet build "ECommerce.Inventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Inventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Inventory.Api.dll"]
