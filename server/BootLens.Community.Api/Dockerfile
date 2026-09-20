FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY BootLens.Community.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV BOOTLENS_DB_PATH=/data/community.db
VOLUME ["/data"]
COPY --from=build /out .
ENTRYPOINT ["dotnet", "BootLens.Community.Api.dll"]
