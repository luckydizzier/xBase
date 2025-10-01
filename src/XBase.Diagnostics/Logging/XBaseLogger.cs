using System;
using Microsoft.Extensions.Logging;

namespace XBase.Diagnostics.Logging;

public sealed class XBaseLogger : ILogger
{
  private readonly string _categoryName;

  public XBaseLogger(string categoryName)
  {
    _categoryName = categoryName;
  }

  public IDisposable BeginScope<TState>(TState state)
    where TState : notnull
  {
    return NullScope.Instance;
  }

  public bool IsEnabled(LogLevel logLevel)
  {
    return logLevel >= LogLevel.Information;
  }

  public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
  {
    if (!IsEnabled(logLevel))
    {
      return;
    }

    var message = formatter(state, exception);
    Console.WriteLine($"[{logLevel}] {_categoryName}: {message}");
    if (exception is not null)
    {
      Console.WriteLine(exception);
    }
  }

  private sealed class NullScope : IDisposable
  {
    public static readonly NullScope Instance = new();

    public void Dispose()
    {
    }
  }
}
