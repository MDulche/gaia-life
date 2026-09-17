namespace App.Modules.Travail.Entities;

/// <summary>Employeur du foyer. <see cref="DateFin"/> nulle = emploi encore en cours.</summary>
public class Employeur
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Adresse { get; set; }

    public DateTime DateDebut { get; set; }

    public DateTime? DateFin { get; set; }

    public ICollection<FichePaie> FichePaies { get; set; } = new List<FichePaie>();

    public ICollection<Conge> Conges { get; set; } = new List<Conge>();

    public ICollection<SoldeConges> SoldesConges { get; set; } = new List<SoldeConges>();

    public ICollection<HeureSupplementaire> HeuresSupplementaires { get; set; } = new List<HeureSupplementaire>();
}
