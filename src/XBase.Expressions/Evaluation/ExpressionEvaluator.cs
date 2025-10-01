using System;
using System.Collections.Generic;

namespace XBase.Expressions.Evaluation;

public sealed class ExpressionEvaluator
{
  private readonly IDictionary<string, Func<string?, string?>> _functions;

  public ExpressionEvaluator()
    : this(new Dictionary<string, Func<string?, string?>>(StringComparer.OrdinalIgnoreCase)
    {
      ["UPPER"] = value => value?.ToUpperInvariant(),
      ["TRIM"] = value => value?.Trim(),
      ["LOWER"] = value => value?.ToLowerInvariant()
    })
  {
  }

  public ExpressionEvaluator(IDictionary<string, Func<string?, string?>> functions)
  {
    _functions = functions;
  }

  public string? Invoke(string functionName, string? argument)
  {
    if (!_functions.TryGetValue(functionName, out var handler))
    {
      throw new InvalidOperationException(FormattableString.Invariant($"Unknown function '{functionName}'."));
    }

    return handler(argument);
  }
}
