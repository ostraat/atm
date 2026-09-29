# syntax=docker/dockerfile:1.7

FROM node:20.18.1-alpine AS frontend
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ ./
RUN dotnet restore Atm.sln --locked-mode \
 && dotnet publish src/Atm.Api/Atm.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update  && apt-get install -y --no-install-recommends curl  && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Atm="Data Source=/data/atm.db"
RUN mkdir -p /data
COPY --from=build /app/publish ./
COPY --from=frontend /frontend/dist ./wwwroot
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "Atm.Api.dll"]
