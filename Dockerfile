FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["hOps.web/hOps.web.csproj", "hOps.web/"]
RUN dotnet restore hOps.web/hOps.web.csproj
COPY . .
WORKDIR /src/hOps.web
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src /src
COPY --from=build /src/hOps.web/scripts/start-with-migrations.sh ./start-with-migrations.sh
ENV ASPNETCORE_URLS=http://+:8080
ENV PATH="$PATH:/root/.dotnet/tools"
RUN chmod +x ./start-with-migrations.sh \
    && dotnet tool install --global dotnet-ef
CMD ["./start-with-migrations.sh"]
