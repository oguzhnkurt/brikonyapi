FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BrikonYapi.Web/BrikonYapi.Web.csproj", "BrikonYapi.Web/"]
RUN dotnet restore "BrikonYapi.Web/BrikonYapi.Web.csproj"
COPY . .
WORKDIR "/src/BrikonYapi.Web"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN apt-get update && apt-get install -y --no-install-recommends ghostscript && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/wwwroot/uploads
# Oturum (cookie) şifreleme anahtarları. Coolify'da kalıcı volume olarak bağlanmalıdır,
# aksi halde her deploy sonrası tüm kullanıcılar çıkış yapmış olur.
RUN mkdir -p /app/dp-keys
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DataProtection__KeysPath=/app/dp-keys
ENTRYPOINT ["dotnet", "BrikonYapi.Web.dll"]
