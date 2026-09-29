using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;

namespace OficinaBilling.Application.Mappers;

public static class OrcamentoMapper
{
    public static OrcamentoResponse ParaResponse(this Orcamento orcamento)
    {
        var pecas = orcamento.Itens.Where(i => i.Tipo == TipoItem.Peca).Select(ParaItemResponse).ToList();
        var servicos = orcamento.Itens.Where(i => i.Tipo == TipoItem.Servico).Select(ParaItemResponse).ToList();

        return new OrcamentoResponse(
            orcamento.Id, orcamento.OsId, orcamento.Status.ToString(),
            pecas, servicos, orcamento.ValorTotal.Valor, orcamento.ValidadeAte, orcamento.GeradoEm);
    }

    private static ItemResponse ParaItemResponse(OrcamentoItem item) =>
        new(item.ReferenciaId, item.Quantidade, item.PrecoUnitario, item.Subtotal.Valor);
}
