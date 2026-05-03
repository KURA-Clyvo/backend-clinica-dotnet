# Azure Deploy — Kura API

Pré-requisito: [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) instalado e autenticado.

```bash
az login
```

## 1. Criar Resource Group

Agrupa todos os recursos do projeto em uma única unidade gerenciável na região Brazil South.

```bash
az group create --name rg-kura-api --location brazilsouth
```

## 2. Criar Azure Container Registry

Repositório privado de imagens Docker. `--admin-enabled true` permite autenticação por usuário/senha no App Service.

```bash
az acr create --resource-group rg-kura-api --name kuracr --sku Basic --admin-enabled true
```

## 3. Build e push da imagem

Faz o build da imagem diretamente na nuvem a partir do contexto local (`.`) e a publica no ACR sem precisar do Docker Desktop.

```bash
az acr build --registry kuracr --image kura-api:latest .
```

## 4. Criar App Service Plan

Plano Linux com tier F1 (gratuito para estudantes). Suporta containers Docker.

```bash
az appservice plan create --name plan-kura --resource-group rg-kura-api --is-linux --sku F1
```

## 5. Criar Web App com a imagem do ACR

Cria a Web App e aponta para a imagem publicada no ACR.

```bash
az webapp create \
  --resource-group rg-kura-api \
  --plan plan-kura \
  --name kura-api-fiap \
  --deployment-container-image-name kuracr.azurecr.io/kura-api:latest
```

## 6. Configurar variáveis de ambiente

Injeta as configurações sensíveis como App Settings (equivalente a variáveis de ambiente no container).

```bash
az webapp config appsettings set \
  --resource-group rg-kura-api \
  --name kura-api-fiap \
  --settings \
    ConnectionStrings__OracleConnection="User Id=RM562999;Password=081105;Data Source=oracle.fiap.com.br:1521/orcl" \
    Jwt__Key="kura-api-secret-key-fiap-2026-clyvovet" \
    Jwt__Issuer="kura-api" \
    Jwt__Audience="kura-client" \
    IoT__ApiKey="kura-iot-device-key-2026"
```

## 7. Validar deploy

Aguarde ~2 minutos após o deploy e execute:

```bash
curl https://kura-api-fiap.azurewebsites.net/health
# Esperado: { "status": "ok", "timestamp": "..." }
```

Swagger UI: `https://kura-api-fiap.azurewebsites.net/swagger`

## URL de produção

`https://kura-api-fiap.azurewebsites.net`
