FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Catalog.Api/ECommerce.Catalog.Api.csproj", "src/ECommerce.Catalog.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Catalog.Domain/ECommerce.Catalog.Domain.csproj", "src/ECommerce.Catalog.Domain/"]
COPY ["src/ECommerce.Catalog.Application/ECommerce.Catalog.Application.csproj", "src/ECommerce.Catalog.Application/"]
COPY ["src/ECommerce.Catalog.Infrastructure/ECommerce.Catalog.Infrastructure.csproj", "src/ECommerce.Catalog.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Catalog.Api/ECommerce.Catalog.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Catalog.Api"
RUN dotnet build "ECommerce.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Catalog.Api.dll"]
