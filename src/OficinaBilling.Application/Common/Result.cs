namespace OficinaBilling.Application.Common;

public enum TipoErro
{
    Nenhum,
    Invalido,
    NaoEncontrado,
    Conflito
}

// Use cases não lançam exceção de negócio, devolvem Result<T> para o Controller
// decidir o status HTTP (Invalido=400/422, NaoEncontrado=404, Conflito=409).
public class Result<T>
{
    public bool Sucesso { get; }
    public T? Valor { get; }
    public string? Erro { get; }
    public TipoErro Tipo { get; }

    private Result(bool sucesso, T? valor, string? erro, TipoErro tipo)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erro = erro;
        Tipo = tipo;
    }

    public static Result<T> Ok(T valor) => new(true, valor, null, TipoErro.Nenhum);
    public static Result<T> Falha(string erro, TipoErro tipo = TipoErro.Invalido) => new(false, default, erro, tipo);
}
