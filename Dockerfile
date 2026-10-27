FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG PROJECT
ARG SERVICE_DLL

COPY services services
RUN dotnet publish "$PROJECT" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ARG SERVICE_DLL
ENV SERVICE_DLL=$SERVICE_DLL
ENTRYPOINT ["sh", "-c", "exec dotnet \"$SERVICE_DLL.dll\""]
