# ADR 0005 — SNS + SQS como broker de mensageria

- **Status:** Aceito
- **Escopo:** global (todos os serviços da Fase 4)
- **Data:** 2026-10

## Contexto

Os serviços precisam trocar eventos de forma assíncrona e desacoplada (PDF: "RabbitMQ, Kafka, SQS, etc."),
com retry, tratamento de mensagens venenosas e baixo custo operacional. A infraestrutura já está na AWS (EKS, RDS, Secrets Manager, SSM).

## Decisão

**SNS Topics (fan-out) + SQS Queues (uma fila por serviço consumidor) + DLQ por fila.**

- Cada evento é um *topic* `oficina-{env}-{evento-com-hifens}` (ex.: `oficina-dev-orcamento-aprovado`). O serviço **dono** do evento cria o topic e publica o ARN no SSM
  (`/oficina/{env}/messaging/topics/{evento}/arn`). Quem consome apenas lê o parâmetro e cria a assinatura — nenhum serviço edita o repositório do outro.
- Cada serviço consome de **uma fila própria** (`oficina-{env}-billing-queue`) assinada nos topics de que precisa. Entrega SNS→SQS **sem** *raw message delivery*:
  o envelope do SNS traz o `TopicArn`, usado para rotear a mensagem ao handler certo.
- **Retry nativo:** a mensagem só é apagada se o handler terminar com sucesso; senão volta após o *visibility timeout* (60 s) e, após 5 recebimentos, vai para a **DLQ** (retenção de 14 dias).
- **Entrega *at-least-once*:** todo consumidor é idempotente; o envelope carrega `eventId` único do evento.
- **Publicação via Outbox** (ADR 0006) para não perder eventos se o SNS estiver indisponível.
- Envelope padrão: `{ eventId, eventType, eventVersion, occurredAt, correlationId, producer, data }` (ver `docs/events.md`).

## Consequências

**Positivas:** serviço gerenciado (zero operação), retry/DLQ/visibilidade prontos, custo por requisição (centavos neste volume), IAM por serviço (IRSA), LocalStack para dev.

**Negativas:** sem ordenação global (aceitável: cada etapa do Saga é causalmente ordenada pelo próprio fluxo; filas FIFO se tornarem necessárias);
sem replay histórico (Kafka teria); entrega duplicada exige idempotência em todos os consumidores.

## Alternativas descartadas

- **Kafka (MSK):** poderoso (replay, ordenação por partição), mas caro e operacionalmente pesado para o volume e o prazo do projeto.
- **RabbitMQ:** bom roteamento, porém precisaria ser operado dentro do cluster (HA, upgrades, persistência) — custo de operação sem ganho funcional para este caso.
- **Chamadas REST síncronas para tudo:** acopla disponibilidade dos serviços e inviabiliza compensação confiável.

## Referências
- AWS — Fanout to SQS: https://docs.aws.amazon.com/sns/latest/dg/sns-sqs-as-subscriber.html
- AWS — SQS dead-letter queues: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-dead-letter-queues.html
