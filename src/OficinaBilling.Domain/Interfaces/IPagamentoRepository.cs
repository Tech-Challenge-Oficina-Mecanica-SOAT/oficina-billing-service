using OficinaBilling.Domain.Entities;

namespace OficinaBilling.Domain.Interfaces;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Pagamento?> ObterPorMercadoPagoIdAsync(string mercadoPagoPaymentId, CancellationToken ct = default);

    /// <summary>Pagamento mais recente da OS (qualquer status) — usado na consulta.</summary>
    Task<Pagamento?> ObterUltimoPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default);

    /// <summary>Pagamento Pendente ou Confirmado da OS — evita cobrar duas vezes.</summary>
    Task<Pagamento?> ObterAtivoPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default);

    Task<IReadOnlyList<Pagamento>> ListarPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default);
    Task SalvarAlteracoesAsync(CancellationToken ct = default);
}
