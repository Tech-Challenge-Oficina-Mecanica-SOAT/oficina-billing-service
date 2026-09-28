using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.UseCases.Orcamento.GerarOrcamento;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("orcamentos")]
public class OrcamentosController : ControllerBase
{
    private readonly GerarOrcamentoUseCase _gerarOrcamentoUseCase;

    public OrcamentosController(GerarOrcamentoUseCase gerarOrcamentoUseCase)
    {
        _gerarOrcamentoUseCase = gerarOrcamentoUseCase;
    }

    // Endpoint manual/de teste — em produção este fluxo normalmente é
    // disparado pelo DiagnosticoConcluidoConsumer, não por chamada HTTP direta.
    [HttpPost]
    public async Task<IActionResult> Gerar([FromBody] Application.DTOs.Requests.GerarOrcamentoRequest request, CancellationToken ct)
    {
        var resultado = await _gerarOrcamentoUseCase.ExecutarAsync(request, ct);
        return resultado.Sucesso ? Ok(resultado.Valor) : BadRequest(resultado.Erro);
    }

    // TODO: GET /orcamentos/{id}, POST /orcamentos/{id}/aprovar, POST /orcamentos/{id}/recusar
}
