using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using XBase.Expressions.Evaluation;

namespace XBase.Demo.Infrastructure.Indexes;

internal static class IndexKeyExpressionCompiler
{
  public static Func<IReadOnlyDictionary<string, object?>, string?> Compile(
    string expression,
    IEnumerable<string> columns,
    ExpressionEvaluator evaluator)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(expression);
    ArgumentNullException.ThrowIfNull(columns);
    ArgumentNullException.ThrowIfNull(evaluator);

    var columnSet = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
    var root = ParseExpression(expression.AsSpan(), evaluator, columnSet);

    return row =>
    {
      ArgumentNullException.ThrowIfNull(row);
      var value = root.Evaluate(row);
      return FormatValue(value);
    };
  }

  private static Node ParseExpression(
    ReadOnlySpan<char> expression,
    ExpressionEvaluator evaluator,
    ISet<string> columns)
  {
    expression = expression.Trim();
    if (expression.IsEmpty)
    {
      throw new FormatException("Index expression cannot be empty.");
    }

    while (IsWrappedByParentheses(expression))
    {
      expression = expression[1..^1].Trim();
    }

    var operatorIndex = FindTopLevelOperator(expression, '+');
    if (operatorIndex >= 0)
    {
      var left = ParseExpression(expression[..operatorIndex], evaluator, columns);
      var right = ParseExpression(expression[(operatorIndex + 1)..], evaluator, columns);
      return new ConcatNode(left, right);
    }

    if (expression.Length >= 2 &&
        (expression[0] == '\'' && expression[^1] == '\'' || expression[0] == '"' && expression[^1] == '"'))
    {
      return new LiteralNode(UnescapeLiteral(expression));
    }

    var openParenIndex = FindTopLevelOpenParenthesis(expression);
    if (openParenIndex > 0 && expression[^1] == ')')
    {
      var functionName = expression[..openParenIndex].Trim();
      if (functionName.IsEmpty)
      {
        throw new FormatException("Function name cannot be empty in index expression.");
      }

      var argument = expression[(openParenIndex + 1)..^1];
      var argumentNode = ParseExpression(argument, evaluator, columns);
      return new FunctionNode(functionName.ToString(), argumentNode, evaluator);
    }

    var columnName = expression.ToString();
    if (!columns.Contains(columnName))
    {
      throw new InvalidOperationException($"Column '{columnName}' referenced in index expression was not found.");
    }

    return new FieldNode(columnName);
  }

  private static bool IsWrappedByParentheses(ReadOnlySpan<char> expression)
  {
    if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
    {
      return false;
    }

    var depth = 0;
    for (var index = 0; index < expression.Length; index++)
    {
      var current = expression[index];
      if (current == '(')
      {
        depth++;
      }
      else if (current == ')')
      {
        depth--;
        if (depth == 0 && index < expression.Length - 1)
        {
          return false;
        }
      }
    }

    return depth == 0;
  }

  private static int FindTopLevelOperator(ReadOnlySpan<char> expression, char operatorToken)
  {
    var depth = 0;
    for (var index = 0; index < expression.Length; index++)
    {
      var current = expression[index];
      if (current == '(')
      {
        depth++;
      }
      else if (current == ')')
      {
        depth--;
      }
      else if (depth == 0 && current == operatorToken)
      {
        return index;
      }
    }

    return -1;
  }

  private static int FindTopLevelOpenParenthesis(ReadOnlySpan<char> expression)
  {
    var depth = 0;
    for (var index = 0; index < expression.Length; index++)
    {
      var current = expression[index];
      if (current == '(')
      {
        if (depth == 0)
        {
          return index;
        }

        depth++;
      }
      else if (current == ')')
      {
        depth--;
      }
    }

    return -1;
  }

  private static string UnescapeLiteral(ReadOnlySpan<char> literal)
  {
    if (literal.Length < 2)
    {
      return string.Empty;
    }

    var body = literal[1..^1];
    var builder = new StringBuilder(body.Length);
    for (var index = 0; index < body.Length; index++)
    {
      var current = body[index];
      if ((current == '\'' || current == '"') && index + 1 < body.Length && body[index + 1] == current)
      {
        builder.Append(current);
        index++;
      }
      else
      {
        builder.Append(current);
      }
    }

    return builder.ToString();
  }

  private static string? FormatValue(object? value)
  {
    if (value is null)
    {
      return null;
    }

    return value switch
    {
      string text => text,
      bool flag => flag ? "T" : "F",
      DateTime date => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
      IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
      _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };
  }

  private abstract class Node
  {
    public abstract object? Evaluate(IReadOnlyDictionary<string, object?> row);
  }

  private sealed class FieldNode : Node
  {
    private readonly string _columnName;

    public FieldNode(string columnName)
    {
      _columnName = columnName;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> row)
    {
      row.TryGetValue(_columnName, out var value);
      return value;
    }
  }

  private sealed class LiteralNode : Node
  {
    private readonly string _value;

    public LiteralNode(string value)
    {
      _value = value;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> row)
      => _value;
  }

  private sealed class FunctionNode : Node
  {
    private readonly string _name;
    private readonly Node _argument;
    private readonly ExpressionEvaluator _evaluator;

    public FunctionNode(string name, Node argument, ExpressionEvaluator evaluator)
    {
      _name = name;
      _argument = argument;
      _evaluator = evaluator;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> row)
    {
      var input = FormatValue(_argument.Evaluate(row));
      return _evaluator.Invoke(_name, input);
    }
  }

  private sealed class ConcatNode : Node
  {
    private readonly Node _left;
    private readonly Node _right;

    public ConcatNode(Node left, Node right)
    {
      _left = left;
      _right = right;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> row)
    {
      var leftValue = FormatValue(_left.Evaluate(row)) ?? string.Empty;
      var rightValue = FormatValue(_right.Evaluate(row)) ?? string.Empty;
      return leftValue + rightValue;
    }
  }
}
