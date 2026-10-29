FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/ChonRieng.Domain/ChonRieng.Domain.csproj backend/src/ChonRieng.Domain/
COPY backend/src/ChonRieng.Application/ChonRieng.Application.csproj backend/src/ChonRieng.Application/
COPY backend/src/ChonRieng.Infrastructure/ChonRieng.Infrastructure.csproj backend/src/ChonRieng.Infrastructure/
COPY backend/src/ChonRieng.Api/ChonRieng.Api.csproj backend/src/ChonRieng.Api/
RUN dotnet restore backend/src/ChonRieng.Api/ChonRieng.Api.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/ChonRieng.Api/ChonRieng.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChonRieng.Api.dll"]
