FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Inventory.Api/ECommerce.Inventory.Api.csproj", "src/ECommerce.Inventory.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Inventory.Domain/ECommerce.Inventory.Domain.csproj", "src/ECommerce.Inventory.Domain/"]
COPY ["src/ECommerce.Inventory.Application/ECommerce.Inventory.Application.csproj", "src/ECommerce.Inventory.Application/"]
COPY ["src/ECommerce.Inventory.Infrastructure/ECommerce.Inventory.Infrastructure.csproj", "src/ECommerce.Inventory.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Inventory.Api/ECommerce.Inventory.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Inventory.Api"
RUN dotnet build "ECommerce.Inventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Inventory.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Inventory.Api.dll"]
