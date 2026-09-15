namespace App.Modules.Travail.Entities;

/// <summary>Nature du congé. Persisté en chaîne par EF.</summary>
public enum TypeConge
{
    Paye = 0,
    SansSolde = 1,
    Maladie = 2,
    RTT = 3
}
