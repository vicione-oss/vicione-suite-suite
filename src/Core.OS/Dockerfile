#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update -qq && \
    apt-get install -y -qq libgdiplus libc6-dev
WORKDIR /app

EXPOSE 80
EXPOSE 443
EXPOSE 1883
EXPOSE 5000

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG TARGETARCH
ARG TARGETOS

RUN arch=$TARGETARCH \
    && if [ "$arch" = "amd64" ]; then arch="x64"; fi \
    && echo $TARGETOS-$arch > /tmp/rid

WORKDIR /src
COPY . .
COPY ["NuGet.Config", "."]
RUN rm -rf "tests/"
WORKDIR "/src/src/Core.OS/"
RUN dotnet restore  -a $TARGETARCH "Core.OS.csproj"
RUN dotnet build "Core.OS.csproj" -c Release -o "/app/core.os" -a $TARGETARCH

FROM build AS publish
WORKDIR "/src"
RUN ./build/publish-for-docker.sh "/app/core.os/publish" "$(cat /tmp/rid)"

FROM base AS final
RUN apt-get update -qq && \
    apt-get install -y -qq curl
WORKDIR /app
COPY --from=publish /app/core.os/publish .
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -f http://localhost/hc || exit 1
ENTRYPOINT ["dotnet", "ViciOne.Suite.Core.OS.dll"]
