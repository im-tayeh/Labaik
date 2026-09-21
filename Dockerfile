FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Labaik.Domain/*.csproj          src/Labaik.Domain/
COPY src/Labaik.Application/*.csproj      src/Labaik.Application/
COPY src/Labaik.Infrastructure/*.csproj   src/Labaik.Infrastructure/
COPY src/Labaik.Api/*.csproj              src/Labaik.Api/
RUN dotnet restore src/Labaik.Api/Labaik.Api.csproj

COPY . .
RUN dotnet publish src/Labaik.Api/Labaik.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "Labaik.Api.dll"]