FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommerce.Identity.Api/ECommerce.Identity.Api.csproj", "ECommerce.Identity.Api/"]
COPY ["ECommerce.Security/ECommerce.Security.csproj", "ECommerce.Security/"]
COPY ["ECommerce.Identity.Domain/ECommerce.Identity.Domain.csproj", "ECommerce.Identity.Domain/"]
COPY ["ECommerce.Identity.Application/ECommerce.Identity.Application.csproj", "ECommerce.Identity.Application/"]
COPY ["ECommerce.Identity.Infrastructure/ECommerce.Identity.Infrastructure.csproj", "ECommerce.Identity.Infrastructure/"]
RUN dotnet restore "ECommerce.Identity.Api/ECommerce.Identity.Api.csproj"
COPY . .
WORKDIR "/src/ECommerce.Identity.Api"
RUN dotnet build "ECommerce.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Identity.Api.dll"]
