# ---- AbfuhrPilot API (.NET 8) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/AbfuhrPilot.Api/AbfuhrPilot.Api.csproj
RUN dotnet publish src/AbfuhrPilot.Api/AbfuhrPilot.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AbfuhrPilot.Api.dll"]
