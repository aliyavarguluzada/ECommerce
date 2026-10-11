FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommerce.Payment.Api/ECommerce.Payment.Api.csproj", "ECommerce.Payment.Api/"]
COPY ["ECommerce.Security/ECommerce.Security.csproj", "ECommerce.Security/"]
COPY ["ECommerce.Payment.Domain/ECommerce.Payment.Domain.csproj", "ECommerce.Payment.Domain/"]
COPY ["ECommerce.Payment.Application/ECommerce.Payment.Application.csproj", "ECommerce.Payment.Application/"]
COPY ["ECommerce.Payment.Infrastructure/ECommerce.Payment.Infrastructure.csproj", "ECommerce.Payment.Infrastructure/"]
RUN dotnet restore "ECommerce.Payment.Api/ECommerce.Payment.Api.csproj"
COPY . .
WORKDIR "/src/ECommerce.Payment.Api"
RUN dotnet build "ECommerce.Payment.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build -p:Platform=AnyCPU

FROM build AS publish
RUN dotnet publish "ECommerce.Payment.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -p:Platform=AnyCPU

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ECommerce.Payment.Api.dll"]
