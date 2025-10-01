using System;
using System.Collections.Generic;

namespace XBase.Demo.Domain.Diagnostics;

/// <summary>
/// Represents a telemetry event produced by the demo experience.
/// </summary>
/// <param name="Name">Event identifier.</param>
/// <param name="Timestamp">UTC timestamp when the event occurred.</param>
/// <param name="Payload">Structured payload for diagnostics dashboards.</param>
public sealed record DemoTelemetryEvent(string Name, DateTimeOffset Timestamp, IReadOnlyDictionary<string, object?> Payload);

/// <summary>
/// Abstraction for publishing telemetry events to interested listeners.
/// </summary>
public interface IDemoTelemetrySink
{
  void Publish(DemoTelemetryEvent telemetryEvent);
}
