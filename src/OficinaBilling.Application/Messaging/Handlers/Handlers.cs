using OficinaBilling.Application.Common;
using OficinaBilling.Application.DTOs.Messages;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;
using OficinaBilling.Application.UseCases.Pagamento.CancelarPorOs;
using OficinaBilling.Application.UseCases.Pagamento.CriarCobranca;

namespace OficinaBilling.Application.Messaging.Handlers;

/// <summary>diagnostico.concluido → gera o orçamento.</summary>
public sealed class DiagnosticoConcluidoHandler(GerarOrcamentoUseCase useCase) : MensagemHandler<GerarOrcamentoRequest>
{
    public override string Topico => "diagnostico.concluido";

    protected override async Task<Result<bool>> ExecutarAsync(GerarOrcamentoRequest mensagem, CancellationToken ct)
    {
        var r = await useCase.ExecutarAsync(mensagem, ct);
        return r.Sucesso ? Result<bool>.Ok(true) : Result<bool>.Falha(r.Erro!, r.Tipo);
    }
}

/// <summary>execucao.finalizada → cria a cobrança no Mercado Pago.</summary>
public sealed class ExecucaoFinalizadaHandler(CriarCobrancaUseCase useCase) : MensagemHandler<ExecucaoFinalizadaMessage>
{
    public override string Topico => "execucao.finalizada";

    protected override async Task<Result<bool>> ExecutarAsync(ExecucaoFinalizadaMessage mensagem, CancellationToken ct)
    {
        var r = await useCase.ExecutarAsync(mensagem, ct);
        return r.Sucesso ? Result<bool>.Ok(true) : Result<bool>.Falha(r.Erro!, r.Tipo);
    }
}

/// <summary>os.cancelada → compensação (cancela orçamento/cobrança, estorna se já pago).</summary>
public sealed class OsCanceladaHandler(CancelarPorOsUseCase useCase) : MensagemHandler<OsCanceladaMessage>
{
    public override string Topico => "os.cancelada";

    protected override Task<Result<bool>> ExecutarAsync(OsCanceladaMessage mensagem, CancellationToken ct) =>
        useCase.ExecutarAsync(mensagem, ct);
}
