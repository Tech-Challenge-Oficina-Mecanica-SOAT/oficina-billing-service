using OficinaBilling.Domain.Enums;
using OficinaBilling.Domain.ValueObjects;

namespace OficinaBilling.Domain.Entities;

// Espelha o schema do contrato openapi/billing.yaml (Orcamento.pecas / Orcamento.servicos).
public class OrcamentoItem
{
    public Guid Id { get; private set; }
    public TipoItem Tipo { get; private set; }
    public Guid ReferenciaId { get; private set; } // pecaId ou servicoId (Cadastros Service)
    public string Nome { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }

    public Dinheiro Subtotal => new(PrecoUnitario * Quantidade);

    private OrcamentoItem() { }

    public static OrcamentoItem Criar(TipoItem tipo, Guid referenciaId, string nome, int quantidade, decimal precoUnitario)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        if (precoUnitario < 0)
            throw new ArgumentException("Preço não pode ser negativo.", nameof(precoUnitario));

        return new OrcamentoItem
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            ReferenciaId = referenciaId,
            Nome = nome,
            Quantidade = quantidade,
            PrecoUnitario = precoUnitario
        };
    }
}
