using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Application.Mappers;

public static class OrcamentoMapper
{
    public static OrcamentoResponse ParaResponse(this Orcamento orcamento) => new(
        orcamento.Id,
        orcamento.OrdemServicoId,
        orcamento.Status.ToString(),
        orcamento.Itens.Where(i => i.PecaId.HasValue)
            .Select(i => new OrcamentoPecaResponse(i.PecaId.GetValueOrDefault(), i.Quantidade, i.PrecoUnitario.Valor, i.Subtotal.Valor))
            .ToList(),
        orcamento.Itens.Where(i => i.ServicoId.HasValue)
            .Select(i => new OrcamentoServicoResponse(i.ServicoId.GetValueOrDefault(), i.Quantidade, i.PrecoUnitario.Valor, i.Subtotal.Valor))
            .ToList(),
        orcamento.Itens.Where(i => !i.PecaId.HasValue && !i.ServicoId.HasValue)
            .Select(i => new ItemOrcamentoLegadoResponse(i.Descricao, i.Quantidade, i.PrecoUnitario.Valor, i.Subtotal.Valor))
            .ToList(),
        orcamento.ValorTotal.Valor,
        orcamento.ValidadeAte,
        orcamento.CriadoEm);

    public static PagamentoResponse ParaResponse(this Pagamento p) => new(
        p.Id,
        p.OrdemServicoId,
        p.OrcamentoId,
        p.Valor.Valor,
        p.Status.ToString(),
        p.MercadoPagoPaymentId,
        p.LinkPagamento,
        p.CriadoEm);
}
