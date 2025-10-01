using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Diagnostics;

namespace XBase.Demo.Diagnostics;

/// <summary>
/// Captures telemetry events in-memory for UI consumption.
/// </summary>
public sealed class InMemoryTelemetrySink : IDemoTelemetrySink
{
  private readonly ConcurrentQueue<DemoTelemetryEvent> _events = new();
  private readonly ILogger<InMemoryTelemetrySink> _logger;

  public InMemoryTelemetrySink(ILogger<InMemoryTelemetrySink> logger)
  {
    _logger = logger;
  }

  public void Publish(DemoTelemetryEvent telemetryEvent)
  {
    ArgumentNullException.ThrowIfNull(telemetryEvent);

    _events.Enqueue(telemetryEvent);
    while (_events.Count > 128 && _events.TryDequeue(out _))
    {
    }

    _logger.LogInformation("Telemetry event {Name} captured", telemetryEvent.Name);
  }

  /// <summary>
  /// Returns a snapshot of the buffered events for visualization.
  /// </summary>
  public IReadOnlyCollection<DemoTelemetryEvent> GetSnapshot()
      => _events.ToArray();
}
