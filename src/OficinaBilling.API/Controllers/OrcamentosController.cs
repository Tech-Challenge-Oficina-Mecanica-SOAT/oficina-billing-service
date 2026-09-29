using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.DTOs.Requests;
using OficinaBilling.Application.UseCases.Orcamento;

namespace OficinaBilling.API.Controllers;

// Espelha openapi/billing.yaml — paths /orcamentos/*.
[ApiController]
[Route("orcamentos")]
public class OrcamentosController : ControllerBase
{
    private readonly ConsultarOrcamentoUseCase _consultarUseCase;
    private readonly AprovarOrcamentoUseCase _aprovarUseCase;
    private readonly RecusarOrcamentoUseCase _recusarUseCase;

    public OrcamentosController(
        ConsultarOrcamentoUseCase consultarUseCase,
        AprovarOrcamentoUseCase aprovarUseCase,
        RecusarOrcamentoUseCase recusarUseCase)
    {
        _consultarUseCase = consultarUseCase;
        _aprovarUseCase = aprovarUseCase;
        _recusarUseCase = recusarUseCase;
    }

    // GET /orcamentos/{osId}
    [HttpGet("{osId:guid}")]
    public async Task<IActionResult> ObterPorOs(Guid osId, CancellationToken ct)
    {
        var resultado = await _consultarUseCase.ExecutarAsync(osId, ct);
        return resultado.Sucesso ? Ok(resultado.Valor) : NotFound(new { erro = resultado.Erro });
    }

    // POST /orcamentos/{orcamentoId}/aprovar
    [HttpPost("{orcamentoId:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid orcamentoId, CancellationToken ct)
    {
        var resultado = await _aprovarUseCase.ExecutarAsync(orcamentoId, ct);
        return resultado.Sucesso ? NoContent() : BadRequest(new { erro = resultado.Erro });
    }

    // POST /orcamentos/{orcamentoId}/recusar
    [HttpPost("{orcamentoId:guid}/recusar")]
    public async Task<IActionResult> Recusar(Guid orcamentoId, [FromBody] RecusarOrcamentoRequest request, CancellationToken ct)
    {
        var resultado = await _recusarUseCase.ExecutarAsync(orcamentoId, request, ct);
        return resultado.Sucesso ? NoContent() : BadRequest(new { erro = resultado.Erro });
    }
}
