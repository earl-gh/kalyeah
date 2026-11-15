# Kalyeah dedicated game server (.NET 8 WebSocket server on $PORT).
# Build:  docker build -t kalyeah-server .
# Run:    docker run -p 8080:8080 kalyeah-server
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY unity/Kalyeah.Sim/Kalyeah.Sim.csproj unity/Kalyeah.Sim/
COPY unity/Kalyeah.Net/Kalyeah.Net.csproj unity/Kalyeah.Net/
COPY unity/Kalyeah.Server/Kalyeah.Server.csproj unity/Kalyeah.Server/
RUN dotnet restore unity/Kalyeah.Server/Kalyeah.Server.csproj

COPY unity/Kalyeah.Sim/ unity/Kalyeah.Sim/
COPY unity/Kalyeah.Net/ unity/Kalyeah.Net/
COPY unity/Kalyeah.Server/ unity/Kalyeah.Server/
COPY unity/Data/ unity/Data/
RUN dotnet publish unity/Kalyeah.Server/Kalyeah.Server.csproj -c Release --no-restore -o /app \
    && test -f /app/Data/rules__game_rules.json

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV KALYEAH_DATA=/app/Data
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "/app/Kalyeah.Server.dll"]
