FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore --runtime alpine-x64
# Build and publish a release
RUN dotnet publish -c Release -o out \
    --no-restore \
    --runtime alpine-x64 \
    --self-contained true \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine
WORKDIR /App
EXPOSE 80
COPY --from=build-env /App/out .
ENTRYPOINT ["./RainApp"]