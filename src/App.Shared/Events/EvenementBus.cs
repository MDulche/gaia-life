using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Shared.Events;

/// <summary>Implémentation in-mémoire thread-safe de <see cref="IEvenementBus"/>.</summary>
public sealed class EvenementBus : IEvenementBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();
    private readonly ILogger<EvenementBus> _logger;

    public EvenementBus()
        : this(NullLogger<EvenementBus>.Instance)
    {
    }

    public EvenementBus(ILogger<EvenementBus> logger)
    {
        _logger = logger ?? NullLogger<EvenementBus>.Instance;
    }

    public async Task PublierAsync<T>(T evenement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evenement);

        if (!_handlers.TryGetValue(typeof(T), out var bag))
        {
            return;
        }

        foreach (var handler in bag)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (handler is not Func<T, Task> typed)
            {
                continue;
            }

            try
            {
                await typed(evenement).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Échec du handler d'événement {EventType} ; les autres handlers continuent.",
                    typeof(T).Name);
            }
        }
    }

    public void Abonner<T>(Func<T, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var bag = _handlers.GetOrAdd(typeof(T), _ => []);
        bag.Add(handler);
    }
}
