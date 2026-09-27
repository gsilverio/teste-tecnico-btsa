FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TesteTecnico.Api/TesteTecnico.Api.csproj TesteTecnico.Api/
RUN dotnet restore TesteTecnico.Api/TesteTecnico.Api.csproj

COPY TesteTecnico.Api/ TesteTecnico.Api/
WORKDIR /src/TesteTecnico.Api
RUN dotnet publish TesteTecnico.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TesteTecnico.Api.dll"]
