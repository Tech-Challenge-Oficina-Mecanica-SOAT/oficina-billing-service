using Microsoft.EntityFrameworkCore;
using OficinaBilling.Domain.Entities;
using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.Interfaces;

namespace OficinaBilling.Infrastructure.Persistence.Repositories;

public class PagamentoRepository : IPagamentoRepository
{
    private readonly BillingDbContext _context;

    public PagamentoRepository(BillingDbContext context) => _context = context;

    public Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Pagamento?> ObterPorMercadoPagoIdAsync(string mercadoPagoPaymentId, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.MercadoPagoPaymentId == mercadoPagoPaymentId, ct);

    public Task<Pagamento?> ObterUltimoPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default) =>
        _context.Pagamentos.Where(p => p.OrdemServicoId == ordemServicoId)
            .OrderByDescending(p => p.CriadoEm).FirstOrDefaultAsync(ct);

    public Task<Pagamento?> ObterAtivoPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default) =>
        _context.Pagamentos.FirstOrDefaultAsync(p => p.OrdemServicoId == ordemServicoId
            && (p.Status == StatusPagamento.Pendente || p.Status == StatusPagamento.Confirmado), ct);

    public async Task<IReadOnlyList<Pagamento>> ListarPorOrdemServicoIdAsync(Guid ordemServicoId, CancellationToken ct = default) =>
        await _context.Pagamentos.Where(p => p.OrdemServicoId == ordemServicoId).ToListAsync(ct);

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken ct = default) =>
        await _context.Pagamentos.AddAsync(pagamento, ct);

    public Task SalvarAlteracoesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
