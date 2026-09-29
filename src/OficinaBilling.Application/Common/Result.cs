namespace OficinaBilling.Application.Common;

// Mesmo padrão usado no oficina-mecanica: use cases não lançam exceção de
// negócio, devolvem Result<T> para o Controller decidir o status HTTP.
public class Result<T>
{
    public bool Sucesso { get; }
    public T? Valor { get; }
    public string? Erro { get; }

    private Result(bool sucesso, T? valor, string? erro)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erro = erro;
    }

    public static Result<T> Ok(T valor) => new(true, valor, null);
    public static Result<T> Falha(string erro) => new(false, default, erro);
}
