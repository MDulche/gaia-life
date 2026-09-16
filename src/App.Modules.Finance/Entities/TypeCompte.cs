namespace App.Modules.Finance.Entities;

/// <summary>Nature d'un compte. Persisté en chaîne par EF (plus lisible en SQL).</summary>
public enum TypeCompte
{
    Courant = 0,
    Epargne = 1
}
