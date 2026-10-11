FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommerce.Order.Api/ECommerce.Order.Api.csproj", "ECommerce.Order.Api/"]
COPY ["ECommerce.Security/ECommerce.Security.csproj", "ECommerce.Security/"]
COPY ["ECommerce.Order.Domain/ECommerce.Order.Domain.csproj", "ECommerce.Order.Domain/"]
COPY ["ECommerce.Order.Application/ECommerce.Order.Application.csproj", "ECommerce.Order.Application/"]
COPY ["ECommerce.Order.Infrastructure/ECommerce.Order.Infrastructure.csproj", "ECommerce.Order.Infrastructure/"]
RUN dotnet restore "ECommerce.Order.Api/ECommerce.Order.Api.csproj"
COPY . .
WORKDIR "/src/ECommerce.Order.Api"
RUN dotnet build "ECommerce.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Order.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Order.Api.dll"]
