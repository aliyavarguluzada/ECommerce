FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ECommerce.Payment.Api/ECommerce.Payment.Api.csproj", "src/ECommerce.Payment.Api/"]
COPY ["src/ECommerce.Security/ECommerce.Security.csproj", "src/ECommerce.Security/"]
COPY ["src/ECommerce.Payment.Domain/ECommerce.Payment.Domain.csproj", "src/ECommerce.Payment.Domain/"]
COPY ["src/ECommerce.Payment.Application/ECommerce.Payment.Application.csproj", "src/ECommerce.Payment.Application/"]
COPY ["src/ECommerce.Payment.Infrastructure/ECommerce.Payment.Infrastructure.csproj", "src/ECommerce.Payment.Infrastructure/"]
RUN dotnet restore "src/ECommerce.Payment.Api/ECommerce.Payment.Api.csproj"
COPY . .
WORKDIR "/src/src/ECommerce.Payment.Api"
RUN dotnet build "ECommerce.Payment.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Payment.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Payment.Api.dll"]
