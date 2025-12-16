FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["hOps.web/hOps.web.csproj", "hOps.web/"]
RUN dotnet restore hOps.web/hOps.web.csproj
COPY . .
WORKDIR /src/hOps.web
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
CMD ["dotnet", "hOps.web.dll"]
