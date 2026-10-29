using Microsoft.AspNetCore.Mvc;
using OficinaBilling.Application.Common;

namespace OficinaBilling.API.Controllers;

public static class ResultExtensions
{
    public static IActionResult ParaActionResult<T>(this Result<T> resultado, ControllerBase controller)
    {
        if (resultado.Sucesso)
            return controller.Ok(resultado.Valor);

        return resultado.Tipo switch
        {
            TipoErro.NaoEncontrado => controller.Problem(detail: resultado.Erro, statusCode: StatusCodes.Status404NotFound),
            TipoErro.Conflito => controller.Problem(detail: resultado.Erro, statusCode: StatusCodes.Status409Conflict),
            _ => controller.Problem(detail: resultado.Erro, statusCode: StatusCodes.Status422UnprocessableEntity)
        };
    }
}
