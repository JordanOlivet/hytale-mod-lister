FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG VERSION=0.0.0
WORKDIR /src
COPY . .
RUN dotnet publish HytaleModLister.Api.csproj -c Release -o /app -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Create directories for mods and data
RUN mkdir -p /app/mods /app/data

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
ENTRYPOINT ["dotnet", "HytaleModLister.Api.dll"]
