namespace App.Modules.Course.Entities;

/// <summary>Paramètres du module Courses (une seule ligne attendue).</summary>
public class CourseParametre
{
    public int Id { get; set; }

    /// <summary>Compte Finance utilisé pour les sorties automatiques (liaison Course|Finance).</summary>
    public int? CompteCoursesParDefautId { get; set; }
}
