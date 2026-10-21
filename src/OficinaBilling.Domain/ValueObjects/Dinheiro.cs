namespace OficinaBilling.Domain.ValueObjects;

public sealed record Dinheiro
{
    public decimal Valor { get; }

    public Dinheiro(decimal valor)
    {
        if (valor < 0)
            throw new ArgumentException("Valor monetário não pode ser negativo.", nameof(valor));
        Valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }

    public static Dinheiro Zero => new(0);
    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => new(a.Valor + b.Valor);
    public override string ToString() => Valor.ToString("C2");
}
