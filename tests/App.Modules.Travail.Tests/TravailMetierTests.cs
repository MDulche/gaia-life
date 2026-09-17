using App.Modules.Travail;
using App.Modules.Travail.Entities;
using App.Modules.Travail.Services;

namespace App.Modules.Travail.Tests;

public sealed class TravailMetierTests
{
    [Fact]
    public async Task AjouterFichePaie_persiste_et_apparait_dans_historique()
    {
        await TravailTestHarness.WithServiceAsync(async (service, factory) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            await service.AjouterFichePaieAsync(new FichePaie
            {
                EmployeurId = emp.Id,
                Mois = new DateTime(2026, 1, 1),
                SalaireBrut = 3000,
                SalaireNet = 2300,
                TotalCotisations = 700
            });

            var fiches = await service.ListerFichesPaieAsync(emp.Id);
            Assert.Single(fiches);
            Assert.Equal(2300m, fiches[0].SalaireNet);
        });
    }

    [Fact]
    public async Task Solde_deduit_uniquement_conges_Paye_pas_Maladie_ni_RTT()
    {
        await TravailTestHarness.WithServiceAsync(async (service, _) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            var annee = DateTime.Today.Year;
            await service.EnregistrerJoursAcquisAsync(emp.Id, annee, 25);

            await service.DemanderCongeAsync(new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.Paye,
                DateDebut = new DateTime(annee, 6, 2),
                DateFin = new DateTime(annee, 6, 2),
                NombreJours = 1
            });
            await service.DemanderCongeAsync(new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.Maladie,
                DateDebut = new DateTime(annee, 6, 3),
                DateFin = new DateTime(annee, 6, 4),
                NombreJours = 2
            });
            await service.DemanderCongeAsync(new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.RTT,
                DateDebut = new DateTime(annee, 6, 5),
                DateFin = new DateTime(annee, 6, 5),
                NombreJours = 1
            });

            var solde = await service.SoldeCongesActuelAsync(emp.Id, annee);
            Assert.Equal(25m, solde.JoursAcquis);
            Assert.Equal(1m, solde.JoursPris);
            Assert.Equal(24m, solde.JoursRestants);
            Assert.True(TravailService.TypeConsommeSolde(TypeConge.Paye));
            Assert.False(TravailService.TypeConsommeSolde(TypeConge.Maladie));
            Assert.False(TravailService.TypeConsommeSolde(TypeConge.SansSolde));
            Assert.False(TravailService.TypeConsommeSolde(TypeConge.RTT));
        });
    }

    [Fact]
    public async Task DemanderConge_bloque_si_solde_insuffisant()
    {
        await TravailTestHarness.WithServiceAsync(async (service, _) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            var annee = DateTime.Today.Year;
            await service.EnregistrerJoursAcquisAsync(emp.Id, annee, 1);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DemanderCongeAsync(new Conge
                {
                    EmployeurId = emp.Id,
                    Type = TypeConge.Paye,
                    DateDebut = new DateTime(annee, 9, 1),
                    DateFin = new DateTime(annee, 9, 5),
                    NombreJours = 5
                }));

            Assert.Contains("Solde insuffisant", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Congé_a_cheval_reparti_proportionnellement_par_annee()
    {
        await TravailTestHarness.WithServiceAsync(async (service, _) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            // Lun 29 déc 2025 → mer 31 déc 2025 = 3 ouvrés ; jeu 1 jan 2026 → ven 2 jan = 2 ouvrés.
            var debut = new DateTime(2025, 12, 29);
            var fin = new DateTime(2026, 1, 2);
            Assert.Equal(DayOfWeek.Monday, debut.DayOfWeek);

            var conge = new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.Paye,
                DateDebut = debut,
                DateFin = fin,
                NombreJours = 5,
                Statut = StatutConge.Valide
            };

            var pris2025 = TravailService.JoursConsommesDansAnnee(conge, 2025);
            var pris2026 = TravailService.JoursConsommesDansAnnee(conge, 2026);
            Assert.Equal(3m, pris2025);
            Assert.Equal(2m, pris2026);
            Assert.Equal(5m, pris2025 + pris2026);
        });
    }

    [Fact]
    public async Task DemanderConge_detecte_chevauchement_a_la_creation()
    {
        await TravailTestHarness.WithServiceAsync(async (service, _) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            var annee = DateTime.Today.Year;
            await service.EnregistrerJoursAcquisAsync(emp.Id, annee, 20);

            await service.DemanderCongeAsync(new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.Paye,
                DateDebut = new DateTime(annee, 10, 5),
                DateFin = new DateTime(annee, 10, 7),
                NombreJours = 3
            });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DemanderCongeAsync(new Conge
                {
                    EmployeurId = emp.Id,
                    Type = TypeConge.Paye,
                    DateDebut = new DateTime(annee, 10, 6),
                    DateFin = new DateTime(annee, 10, 8),
                    NombreJours = 3
                }));

            Assert.Contains("chevauche", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void JoursFeries_et_ponts_2026_correspondent_aux_cas_connus()
    {
        var feries = TravailCalendrier.ObtenirJoursFeries(2026);
        Assert.Contains(feries, f => f.Date == new DateTime(2026, 4, 6) && f.Libelle.Contains("Pâques"));
        Assert.Contains(feries, f => f.Date == new DateTime(2026, 5, 14) && f.Libelle == "Ascension");
        Assert.Contains(feries, f => f.Date == new DateTime(2026, 5, 25) && f.Libelle.Contains("Pentecôte"));

        var ponts = TravailCalendrier.ObtenirOpportunitesPont(2026);
        Assert.Contains(ponts, p =>
            p.LibelleJourFerie == "Ascension"
            && p.JoursAPoser.Count == 1
            && p.JoursAPoser[0] == new DateTime(2026, 5, 15)
            && p.Efficacite == 4m);
        Assert.DoesNotContain(ponts, p => p.LibelleJourFerie == "1er mai");
    }

    [Fact]
    public async Task DemanderConge_cree_directement_Statut_Valide()
    {
        await TravailTestHarness.WithServiceAsync(async (service, _) =>
        {
            var emp = await service.AjouterEmployeurAsync("Acme", null, new DateTime(2024, 1, 1), null);
            var annee = DateTime.Today.Year;
            await service.EnregistrerJoursAcquisAsync(emp.Id, annee, 10);

            await service.DemanderCongeAsync(new Conge
            {
                EmployeurId = emp.Id,
                Type = TypeConge.Paye,
                DateDebut = new DateTime(annee, 4, 7),
                DateFin = new DateTime(annee, 4, 7),
                NombreJours = 1
            });

            var conges = await service.ListerCongesAsync(emp.Id);
            Assert.Single(conges);
            Assert.Equal(StatutConge.Valide, conges[0].Statut);
        });
    }
}
