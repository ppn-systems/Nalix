FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /Source
COPY . .
RUN msbuild /p:Configuration=Release

FROM mcr.microsoft.com/windows/servercore
WORKDIR /app
COPY --from=build /Source/build/bin .

# Open ports for UDP, TCP, and HTTPS
EXPOSE 8080/udp 8081/tcp 443/tcp

CMD ["Notio.exe"]
