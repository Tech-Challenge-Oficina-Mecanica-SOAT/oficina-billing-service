.PHONY: up up-aws down build test coverage migration migrate

up:            ## API + Postgres + WireMock (MP fake, eventos só logados)
	docker compose up -d --build

up-aws:        ## idem + LocalStack (SNS/SQS/SSM): outbox publica e o consumidor lê a fila
	MESSAGING_PROVIDER=Aws MESSAGING_CONSUMER=true AWS_SERVICE_URL=http://localstack:4566 \
	docker compose --profile aws up -d --build

down:
	docker compose --profile aws down

build:
	dotnet build

test:          ## unitários + integração (precisa de Docker) + BDD
	dotnet test

coverage:      ## roda tudo com cobertura e gera coverage-report/index.html
	dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults
	reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:coverage-report \
	  -reporttypes:"TextSummary;HtmlInline" -assemblyfilters:"+OficinaBilling.*;-*Tests*" \
	  -filefilters:"-*Program.cs;-*Migrations*;-*obj*;-*.g.cs;-*MercadoPagoGateway.cs;-*SnsEventPublisher.cs;-*SqsConsumerService.cs;-*SecretsLoader.cs"
	@cat coverage-report/Summary.txt

migration:     ## cria a migration inicial (rode UMA vez e commite a pasta Migrations): make migration NAME=Inicial
	dotnet ef migrations add $(NAME) --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API --output-dir Persistence/Migrations

migrate:
	dotnet ef database update --project src/OficinaBilling.Infrastructure --startup-project src/OficinaBilling.API
