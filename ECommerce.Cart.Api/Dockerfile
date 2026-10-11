FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommerce.Cart.Api/ECommerce.Cart.Api.csproj", "ECommerce.Cart.Api/"]
COPY ["ECommerce.Security/ECommerce.Security.csproj", "ECommerce.Security/"]
COPY ["ECommerce.Cart.Domain/ECommerce.Cart.Domain.csproj", "ECommerce.Cart.Domain/"]
COPY ["ECommerce.Cart.Application/ECommerce.Cart.Application.csproj", "ECommerce.Cart.Application/"]
COPY ["ECommerce.Cart.Infrastructure/ECommerce.Cart.Infrastructure.csproj", "ECommerce.Cart.Infrastructure/"]
RUN dotnet restore "ECommerce.Cart.Api/ECommerce.Cart.Api.csproj"
COPY . .
WORKDIR "/src/ECommerce.Cart.Api"
RUN dotnet build "ECommerce.Cart.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Cart.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Cart.Api.dll"]
