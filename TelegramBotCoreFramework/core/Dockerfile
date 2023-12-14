FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

#client
#ENV ClientName "dev-env"
##tg
#ENV TelegramBotToken "...."
##web
#ENV SiteURL "...."
##google
#ENV GoogleProjectId "..."
#ENV BigQueryDatasetId "...."

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["core/core.csproj", "core/"]
RUN dotnet restore "core/core.csproj"
COPY . .
WORKDIR "/src/core"
RUN dotnet build "core.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "core.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "core.dll"]
