FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Identity.Api/ECommerce.Identity.Api.csproj", "src/ECommerce.Identity.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Identity.Domain/ECommerce.Identity.Domain.csproj", "src/ECommerce.Identity.Domain/"]
COPY ["src/ECommerce.Identity.Application/ECommerce.Identity.Application.csproj", "src/ECommerce.Identity.Application/"]
COPY ["src/ECommerce.Identity.Infrastructure/ECommerce.Identity.Infrastructure.csproj", "src/ECommerce.Identity.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Identity.Api/ECommerce.Identity.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Identity.Api"
RUN dotnet build "ECommerce.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Identity.Api.dll"]
