using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.UseCases.Pagamento;

namespace OficinaBilling.API.Controllers;

[ApiController]
[Route("pagamentos")]
public class PagamentosController : ControllerBase
{
    private readonly ConsultarPagamentoUseCase _consultarUseCase;

    public PagamentosController(ConsultarPagamentoUseCase consultarUseCase) => _consultarUseCase = consultarUseCase;

    // GET /pagamentos/{osId}
    [HttpGet("{osId:guid}")]
    public async Task<IActionResult> ObterPorOs(Guid osId, CancellationToken ct)
    {
        var resultado = await _consultarUseCase.ExecutarAsync(osId, ct);
        return resultado.Sucesso ? Ok(resultado.Valor) : NotFound(new { erro = resultado.Erro });
    }
}
