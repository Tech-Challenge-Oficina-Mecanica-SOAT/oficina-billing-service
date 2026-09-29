.PHONY: up down test build

up:
	docker compose up -d --build

down:
	docker compose down

test:
	dotnet test

build:
	dotnet build

migrate:
	dotnet ef database update --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API
