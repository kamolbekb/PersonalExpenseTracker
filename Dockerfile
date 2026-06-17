# --- Frontend build ---
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY server/ ./server/
COPY ExpenseTracker.sln ./
RUN dotnet restore server/ExpenseTracker.Api/ExpenseTracker.Api.csproj
RUN dotnet publish server/ExpenseTracker.Api/ExpenseTracker.Api.csproj -c Release -o /app
COPY --from=web /web/dist /app/wwwroot

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
