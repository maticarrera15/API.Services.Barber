FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Api.Services.Barber.csproj ./
RUN dotnet restore Api.Services.Barber.csproj

COPY . ./
RUN dotnet publish Api.Services.Barber.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Render enruta al puerto indicado en la variable PORT; por defecto 8080.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet Api.Services.Barber.dll"]
