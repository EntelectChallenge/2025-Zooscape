FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y curl
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV HUSKY=0
WORKDIR /src
COPY . .
RUN dotnet restore ./Zooscape/Zooscape.csproj
WORKDIR /src/Zooscape
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -p Zooscape -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
EXPOSE 5001
HEALTHCHECK CMD curl http://localhost:5001/bothub
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Zooscape.dll"]
