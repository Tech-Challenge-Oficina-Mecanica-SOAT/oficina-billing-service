.PHONY: deps up down logs flow test build migrate

# Só as dependências (Postgres, LocalStack, WireMock). A API roda no IDE ou com:
#   Messaging__Enabled=true dotnet run --project src/OficinaBilling.API
deps:
	docker compose up -d db localstack wiremock

# Tudo, incluindo a API em container (Development, MP fake, endpoints /dev/*)
up:
	docker compose up -d --build

down:
	docker compose down
	
down-v:
	docker compose down -v

logs:
	docker compose logs -f billing-api

flow:
	./kit/scripts/fluxo-feliz.sh

test:
	dotnet test

build:
	dotnet build

migrate:
	dotnet ef database update --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API
