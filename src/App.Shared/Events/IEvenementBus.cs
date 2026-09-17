namespace App.Shared.Events;

/// <summary>Bus d'événements domaine in-process (pas de broker externe).</summary>
public interface IEvenementBus
{
    /// <summary>
    /// Publie l'événement aux handlers abonnés, séquentiellement et en await.
    /// Les exceptions d'un handler sont journalisées et n'interrompent pas les suivants ni l'appelant.
    /// </summary>
    Task PublierAsync<T>(T evenement, CancellationToken cancellationToken = default);

    /// <summary>Abonne un handler asynchrone pour le type d'événement <typeparamref name="T"/>.</summary>
    void Abonner<T>(Func<T, Task> handler);
}
