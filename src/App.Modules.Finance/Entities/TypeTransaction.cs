namespace App.Modules.Finance.Entities;

/// <summary>Sens d'un mouvement. Persisté en chaîne par EF (plus lisible en SQL).</summary>
public enum TypeTransaction
{
    Entree = 0,
    Sortie = 1
}
