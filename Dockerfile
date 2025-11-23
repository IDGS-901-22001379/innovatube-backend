# Imagen base de ASP.NET (cambia 9.0 por 8.0 si tu proyecto es .NET 8)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# SDK para compilar
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiamos y restauramos
COPY ["InnovaTube.Api.csproj", "./"]
RUN dotnet restore "InnovaTube.Api.csproj"

# Copiamos todo el código
COPY . .

# Publicamos en Release
RUN dotnet publish "InnovaTube.Api.csproj" -c Release -o /app/publish

# Imagen final
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "InnovaTube.Api.dll"]
