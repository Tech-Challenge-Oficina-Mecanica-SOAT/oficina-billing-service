using System.Text.Json;
using OficinaBilling.Application.Common;

namespace OficinaBilling.Application.Messaging;

public interface IMensagemHandler
{
    /// <summary>Nome lógico do tópico tratado, ex.: "execucao.finalizada".</summary>
    string Topico { get; }

    Task<Result<bool>> ProcessarAsync(string payloadJson, CancellationToken ct = default);
}

public abstract class MensagemHandler<TMensagem> : IMensagemHandler where TMensagem : class
{
    public abstract string Topico { get; }

    public async Task<Result<bool>> ProcessarAsync(string payloadJson, CancellationToken ct = default)
    {
        TMensagem? mensagem;
        try
        {
            mensagem = JsonSerializer.Deserialize<TMensagem>(payloadJson, JsonDefaults.Options);
        }
        catch (JsonException ex)
        {
            return Result<bool>.Falha($"Payload inválido para '{Topico}': {ex.Message}");
        }

        if (mensagem is null)
            return Result<bool>.Falha($"Payload vazio para '{Topico}'.");

        return await ExecutarAsync(mensagem, ct);
    }

    protected abstract Task<Result<bool>> ExecutarAsync(TMensagem mensagem, CancellationToken ct);
}
