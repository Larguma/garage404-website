FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the .csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy the remaining source code and build the application
COPY . ./
RUN dotnet publish -c Release -o /app/out --no-restore

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Run as the image's non-root user; it must own the folder where the bike images are written.
RUN mkdir -p /app/wwwroot/images && chown -R "$APP_UID" /app/wwwroot
USER $APP_UID

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD ["dotnet", "garage404-backend.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "garage404-backend.dll"]
