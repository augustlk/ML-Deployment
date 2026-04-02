# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first (better Docker layer caching)
COPY ShopApp.sln .
COPY src/ShopApp.Web/ShopApp.Web.csproj src/ShopApp.Web/
COPY tests/ShopApp.Tests/ShopApp.Tests.csproj tests/ShopApp.Tests/

RUN dotnet restore

# Copy the rest of the source
COPY . .

# Run tests (CI gate)
RUN dotnet test tests/ShopApp.Tests/ShopApp.Tests.csproj \
    --configuration Release \
    --no-restore \
    --logger "console;verbosity=normal"

# Publish the web app
RUN dotnet publish src/ShopApp.Web/ShopApp.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: Runtime ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# No SQLite dependency needed — database is hosted on Supabase (PostgreSQL).
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

# DATABASE_URL must be supplied at runtime — never bake credentials into the image.
# Set it in your deployment platform:
#   Vercel:  Project Settings → Environment Variables → DATABASE_URL
#   Railway: Variables tab → DATABASE_URL
#   Fly.io:  fly secrets set DATABASE_URL="..."
#   Docker:  docker run -e DATABASE_URL="..." shopapp
#
# Supabase transaction pooler URL (recommended for containers):
#   postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:6543/postgres

EXPOSE 8080

ENTRYPOINT ["dotnet", "ShopApp.Web.dll"]
