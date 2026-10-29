using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Application.Mappers;

public static class OrcamentoMapper
{
    public static OrcamentoResponse ParaResponse(this Orcamento orcamento) => new(
        orcamento.Id,
        orcamento.OrdemServicoId,
        orcamento.Status.ToString(),
        orcamento.ValorTotal.Valor,
        orcamento.CriadoEm,
        orcamento.AprovadoEm,
        orcamento.Itens
            .Select(i => new ItemOrcamentoResponse(i.Descricao, i.Quantidade, i.PrecoUnitario.Valor, i.Subtotal.Valor))
            .ToList());

    public static PagamentoResponse ParaResponse(this Pagamento p) => new(
        p.Id, p.OrcamentoId, p.OrdemServicoId, p.Status.ToString(), p.Valor.Valor,
        p.LinkPagamento, p.MercadoPagoPaymentId, p.CriadoEm, p.ConfirmadoEm);
}
