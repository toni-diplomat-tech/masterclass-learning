# Build context is the repo root, because DbUp ships the scripts from /database.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src/MasterclassQuiz.Api/MasterclassQuiz.Api.csproj backend/src/MasterclassQuiz.Api/
RUN dotnet restore backend/src/MasterclassQuiz.Api/MasterclassQuiz.Api.csproj
COPY backend/src/ backend/src/
COPY database/ database/
RUN dotnet publish backend/src/MasterclassQuiz.Api/MasterclassQuiz.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MasterclassQuiz.Api.dll"]
