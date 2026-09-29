using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Application.Mappers;

public static class PagamentoMapper
{
    public static PagamentoResponse ParaResponse(this Pagamento pagamento) => new(
        pagamento.Id, pagamento.OsId, pagamento.OrcamentoId, pagamento.Valor.Valor,
        pagamento.Status.ToString(), pagamento.MercadoPagoId, pagamento.LinkPagamento, pagamento.CriadoEm);
}
