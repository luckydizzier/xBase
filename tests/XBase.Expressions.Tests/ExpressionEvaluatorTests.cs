using XBase.Expressions.Evaluation;

namespace XBase.Expressions.Tests;

public sealed class ExpressionEvaluatorTests
{
  [Theory]
  [InlineData("UPPER", "test", "TEST")]
  [InlineData("TRIM", " test ", "test")]
  [InlineData("LOWER", "TEST", "test")]
  public void Invoke_KnownFunction_ReturnsTransformedValue(string function, string input, string expected)
  {
    var evaluator = new ExpressionEvaluator();

    string? result = evaluator.Invoke(function, input);

    Assert.Equal(expected, result);
  }
}
