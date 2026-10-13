FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Order.Api/ECommerce.Order.Api.csproj", "src/ECommerce.Order.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Order.Domain/ECommerce.Order.Domain.csproj", "src/ECommerce.Order.Domain/"]
COPY ["src/ECommerce.Order.Application/ECommerce.Order.Application.csproj", "src/ECommerce.Order.Application/"]
COPY ["src/ECommerce.Order.Infrastructure/ECommerce.Order.Infrastructure.csproj", "src/ECommerce.Order.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Order.Api/ECommerce.Order.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Order.Api"
RUN dotnet build "ECommerce.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Order.Api.dll"]
