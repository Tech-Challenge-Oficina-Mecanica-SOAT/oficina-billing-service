namespace OficinaBilling.Infrastructure.Messaging.Consumers;

// BackgroundService que faz long-polling na fila SQS "billing-queue",
// assinada ao topic "diagnostico.concluido" (dono: Execução Service).
// A subscription é criada no terraform/subscriptions.tf deste repositório,
// referenciando o ARN do topic via Parameter Store — o Execução Service
// não precisa saber que o Billing existe.
//
// TODO: implementar como IHostedService/BackgroundService:
//   1. ReceiveMessage no SQS
//   2. desserializar o payload de "diagnostico.concluido"
//   3. chamar GerarOrcamentoUseCase.ExecutarAsync(...)
//   4. DeleteMessage em caso de sucesso (senão a mensagem volta pra fila / DLQ)
public class DiagnosticoConcluidoConsumer
{
}
