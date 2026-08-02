#!/usr/bin/env bash
set -euo pipefail

# Configure aqui antes de publicar.
# Deixe valores sensiveis em branco no repositorio e preencha apenas localmente.

WEBDEPLOY_SERVER="https://win8133.site4now.net:8172/MsDeploy.axd?site=wlsolutions-002-site10"
WEBDEPLOY_SITE="wlsolutions-002-site10"
WEBDEPLOY_USERNAME="wlsolutions-002"
WEBDEPLOY_PASSWORD="WL2015S0lut22"
WEBDEPLOY_AUTH_TYPE="Basic"
WEBDEPLOY_ALLOW_UNTRUSTED_CERT="true"

ASPNETCORE_ENVIRONMENT="Production"
FRONTEND_ORIGIN="http://monstrinho.local:81"

CONNECTION_STRING="Data Source=SQL8011.site4now.net;Initial Catalog=db_a8eb93_lessie;User Id=db_a8eb93_lessie_admin;Password=Theo45@Izadora;Encrypt=True;TrustServerCertificate=True;"
JWT_SECRET="dev-only-hardcoded-secret-with-32-chars-minimum"
JWT_ISSUER="Lessie"
JWT_AUDIENCE="Lessie.FrontEnd"
JWT_ACCESS_TOKEN_MINUTES="15"
REFRESH_TOKEN_DAYS="30"
PROVIDER_KEY_ENCRYPTION_KEY="dev-only-provider-key-encryption-secret"
GOOGLE_CLIENT_ID="465394213386-0kqcqu1ialvno3st14ifd8h82fc4ga23.apps.googleusercontent.com"

MERCADO_PAGO_PUBLIC_KEY="APP_USR-65820e23-73ea-429e-9662-2f90634cb075"
MERCADO_PAGO_ACCESS_TOKEN="APP_USR-994637805861911-072218-7b693f287c34dc94457b1e2301a8e989-3522374369"
MERCADO_PAGO_WEBHOOK_SECRET=""
MERCADO_PAGO_NOTIFICATION_URL="http://wlsolutions-002-site10.gtempurl.com/api/payments/mercado-pago/webhook"

GROQ_MODEL="openai/gpt-oss-120b"
POLLINATIONS_MODEL="gpt-5.4"
DEV_ADMIN_ACCESS_KEY="LessieDevAdminKey_2026!"

CONFIGURATION="Release"
PROJECT_PATH="Backend/src/Api/Lessie.Api.csproj"
PUBLISH_DIR=".publish/backend"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Comando obrigatorio nao encontrado: $1" >&2
    exit 1
  fi
}

require_value() {
  local name="$1"
  local value="$2"

  if [[ -z "$value" ]]; then
    echo "Preencha a variavel $name no inicio deste script." >&2
    exit 1
  fi
}

xml_escape() {
  local value="${1-}"
  value="${value//&/&amp;}"
  value="${value//</&lt;}"
  value="${value//>/&gt;}"
  value="${value//\"/&quot;}"
  value="${value//\'/&apos;}"
  printf '%s' "$value"
}

write_env_var() {
  local name="$1"
  local value="$2"
  printf '          <environmentVariable name="%s" value="%s" />\n' "$name" "$(xml_escape "$value")"
}

write_web_config() {
  local web_config="$PUBLISH_DIR/web.config"

  {
    printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>'
    printf '%s\n' '<configuration>'
    printf '%s\n' '  <location path="." inheritInChildApplications="false">'
    printf '%s\n' '    <system.webServer>'
    printf '%s\n' '      <handlers>'
    printf '%s\n' '        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />'
    printf '%s\n' '      </handlers>'
    printf '%s\n' '      <aspNetCore processPath="dotnet" arguments=".\Lessie.Api.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">'
    printf '%s\n' '        <environmentVariables>'
    write_env_var "ASPNETCORE_ENVIRONMENT" "$ASPNETCORE_ENVIRONMENT"
    write_env_var "FRONTEND_ORIGIN" "$FRONTEND_ORIGIN"
    write_env_var "CONNECTION_STRING" "$CONNECTION_STRING"
    write_env_var "JWT_SECRET" "$JWT_SECRET"
    write_env_var "JWT_ISSUER" "$JWT_ISSUER"
    write_env_var "JWT_AUDIENCE" "$JWT_AUDIENCE"
    write_env_var "JWT_ACCESS_TOKEN_MINUTES" "$JWT_ACCESS_TOKEN_MINUTES"
    write_env_var "REFRESH_TOKEN_DAYS" "$REFRESH_TOKEN_DAYS"
    write_env_var "PROVIDER_KEY_ENCRYPTION_KEY" "$PROVIDER_KEY_ENCRYPTION_KEY"
    write_env_var "GOOGLE_CLIENT_ID" "$GOOGLE_CLIENT_ID"
    write_env_var "MERCADO_PAGO_PUBLIC_KEY" "$MERCADO_PAGO_PUBLIC_KEY"
    write_env_var "MERCADO_PAGO_ACCESS_TOKEN" "$MERCADO_PAGO_ACCESS_TOKEN"
    write_env_var "MERCADO_PAGO_WEBHOOK_SECRET" "$MERCADO_PAGO_WEBHOOK_SECRET"
    write_env_var "MERCADO_PAGO_NOTIFICATION_URL" "$MERCADO_PAGO_NOTIFICATION_URL"
    write_env_var "GROQ_MODEL" "$GROQ_MODEL"
    write_env_var "POLLINATIONS_MODEL" "$POLLINATIONS_MODEL"
    write_env_var "DEV_ADMIN_ACCESS_KEY" "$DEV_ADMIN_ACCESS_KEY"
    printf '%s\n' '        </environmentVariables>'
    printf '%s\n' '      </aspNetCore>'
    printf '%s\n' '    </system.webServer>'
    printf '%s\n' '  </location>'
    printf '%s\n' '</configuration>'
  } > "$web_config"
}

write_sanitized_appsettings() {
  rm -f "$PUBLISH_DIR/appsettings.Development.json"

  cat > "$PUBLISH_DIR/appsettings.json" <<'JSON'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "Issuer": "",
    "Audience": "",
    "Secret": "",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30
  },
  "ProviderKeys": {
    "EncryptionKey": ""
  },
  "Google": {
    "ClientId": ""
  },
  "MercadoPago": {
    "PublicKey": "",
    "AccessToken": "",
    "WebhookSecret": "",
    "NotificationUrl": ""
  },
  "Cors": {
    "FrontendOrigin": "",
    "FrontendOrigins": []
  },
  "Groq": {
    "Model": "openai/gpt-oss-120b"
  },
  "Pollinations": {
    "Model": "gpt-5.4"
  }
}
JSON
}

require_command dotnet
require_value "WEBDEPLOY_SERVER" "$WEBDEPLOY_SERVER"
require_value "WEBDEPLOY_SITE" "$WEBDEPLOY_SITE"
require_value "WEBDEPLOY_USERNAME" "$WEBDEPLOY_USERNAME"
require_value "WEBDEPLOY_PASSWORD" "$WEBDEPLOY_PASSWORD"
require_value "FRONTEND_ORIGIN" "$FRONTEND_ORIGIN"
require_value "CONNECTION_STRING" "$CONNECTION_STRING"
require_value "JWT_SECRET" "$JWT_SECRET"
require_value "JWT_ISSUER" "$JWT_ISSUER"
require_value "JWT_AUDIENCE" "$JWT_AUDIENCE"
require_value "PROVIDER_KEY_ENCRYPTION_KEY" "$PROVIDER_KEY_ENCRYPTION_KEY"
require_value "GOOGLE_CLIENT_ID" "$GOOGLE_CLIENT_ID"
require_value "DEV_ADMIN_ACCESS_KEY" "$DEV_ADMIN_ACCESS_KEY"

rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT_PATH" --configuration "$CONFIGURATION" --output "$PUBLISH_DIR"

write_sanitized_appsettings
write_web_config

if ! command -v msdeploy >/dev/null 2>&1; then
  echo "Pacote gerado em: $ROOT_DIR/$PUBLISH_DIR"
  echo "msdeploy nao foi encontrado neste Linux. Instale/disponibilize msdeploy no PATH ou rode o deploy em um agente que tenha WebDeploy."
  exit 1
fi

MSDEPLOY_ARGS=(
  "-verb:sync"
  "-source:contentPath=$ROOT_DIR/$PUBLISH_DIR"
  "-dest:contentPath=$WEBDEPLOY_SITE,computerName=$WEBDEPLOY_SERVER,userName=$WEBDEPLOY_USERNAME,password=$WEBDEPLOY_PASSWORD,authType=$WEBDEPLOY_AUTH_TYPE"
  "-enableRule:AppOffline"
)

if [[ "$WEBDEPLOY_ALLOW_UNTRUSTED_CERT" == "true" ]]; then
  MSDEPLOY_ARGS+=("-allowUntrusted")
fi

msdeploy "${MSDEPLOY_ARGS[@]}"
echo "Backend publicado via WebDeploy em $WEBDEPLOY_SITE."
