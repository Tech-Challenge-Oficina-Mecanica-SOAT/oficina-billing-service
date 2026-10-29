using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.DTOs.Responses;
using OficinaBilling.Application.UseCases.Orcamento.AprovarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.ConsultarOrcamento;
using OficinaBilling.Application.UseCases.Orcamento.RecusarOrcamento;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("orcamentos")]
[Produces("application/json")]
public class OrcamentosController : ControllerBase
{
    /// <summary>Consulta o orçamento de uma ordem de serviço.</summary>
    [HttpGet("{osId:guid}")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Consultar(Guid osId, [FromServices] ConsultarOrcamentoUseCase useCase, CancellationToken ct) =>
        (await useCase.ExecutarAsync(osId, ct)).ParaActionResult(this);

    /// <summary>Aprova o orçamento (idempotente). Publica orcamento.aprovado, que dispara a execução.</summary>
    [HttpPost("{orcamentoId:guid}/aprovar")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Aprovar(Guid orcamentoId, [FromServices] AprovarOrcamentoUseCase useCase, CancellationToken ct) =>
        (await useCase.ExecutarAsync(orcamentoId, ct)).ParaActionResult(this);

    /// <summary>Recusa o orçamento (idempotente). Publica orcamento.recusado, que aciona a compensação do Saga.</summary>
    [HttpPost("{orcamentoId:guid}/recusar")]
    [ProducesResponseType(typeof(OrcamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recusar(Guid orcamentoId, [FromServices] RecusarOrcamentoUseCase useCase, CancellationToken ct) =>
        (await useCase.ExecutarAsync(orcamentoId, ct)).ParaActionResult(this);
}
