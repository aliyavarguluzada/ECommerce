FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Cart.Api/ECommerce.Cart.Api.csproj", "src/ECommerce.Cart.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Cart.Domain/ECommerce.Cart.Domain.csproj", "src/ECommerce.Cart.Domain/"]
COPY ["src/ECommerce.Cart.Application/ECommerce.Cart.Application.csproj", "src/ECommerce.Cart.Application/"]
COPY ["src/ECommerce.Cart.Infrastructure/ECommerce.Cart.Infrastructure.csproj", "src/ECommerce.Cart.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Cart.Api/ECommerce.Cart.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Cart.Api"
RUN dotnet build "ECommerce.Cart.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Cart.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Cart.Api.dll"]
