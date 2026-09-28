namespace OficinaBilling.Application.Interfaces;

public interface IAppLogger<T>
{
    void LogInformation(string mensagem, params object[] args);
    void LogWarning(string mensagem, params object[] args);
    void LogError(Exception ex, string mensagem, params object[] args);
}
