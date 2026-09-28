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
        orcamento.CriadoEm);
}
