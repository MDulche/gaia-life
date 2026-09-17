using System.Collections.Concurrent;

namespace App.Shared.Events;

/// <summary>Implémentation in-mémoire thread-safe de <see cref="IEvenementBus"/>.</summary>
public sealed class EvenementBus : IEvenementBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();

    public void Publier<T>(T evenement)
    {
        ArgumentNullException.ThrowIfNull(evenement);

        if (!_handlers.TryGetValue(typeof(T), out var bag))
        {
            return;
        }

        foreach (var handler in bag)
        {
            if (handler is Action<T> typed)
            {
                typed(evenement);
            }
        }
    }

    public void Abonner<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var bag = _handlers.GetOrAdd(typeof(T), _ => []);
        bag.Add(handler);
    }
}
