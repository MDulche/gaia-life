namespace App.Shared.Events;

/// <summary>Bus d'événements domaine in-process (pas de broker externe).</summary>
public interface IEvenementBus
{
    void Publier<T>(T evenement);

    void Abonner<T>(Action<T> handler);
}
