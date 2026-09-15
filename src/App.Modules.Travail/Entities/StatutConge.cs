namespace App.Modules.Travail.Entities;

/// <summary>Cycle de vie d'une demande. Seul un Admin peut passer de EnAttente à Valide ou Refuse.</summary>
public enum StatutConge
{
    EnAttente = 0,
    Valide = 1,
    Refuse = 2
}
