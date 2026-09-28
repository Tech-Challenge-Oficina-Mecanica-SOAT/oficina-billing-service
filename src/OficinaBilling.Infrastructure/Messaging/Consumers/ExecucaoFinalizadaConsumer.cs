namespace OficinaBilling.Infrastructure.Messaging.Consumers;

// TODO: espelha o DiagnosticoConcluidoConsumer, mas escutando "execucao.finalizada"
// e chamando CriarCobrancaUseCase.ExecutarAsync(...) — é aqui que a cobrança
// no Mercado Pago é efetivamente criada.
public class ExecucaoFinalizadaConsumer
{
}
