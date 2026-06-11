using System.Data;
using System.Text;

namespace dbfview;

public static class FilterHelper
{
    /// <summary>
    /// Enhances a user-friendly filter expression into DataView.RowFilter syntax.
    /// For string columns, unquoted values are automatically wrapped in single quotes.
    /// E.g. "A=123" stays as-is if A is numeric; "NAME=hello" becomes "NAME='hello'" if NAME is string.
    /// Supports: =, >, <, >=, <=, <>, LIKE, AND, OR
    /// </summary>
    public static string BuildRowFilter(string expression, DataTable table)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        var parts = SplitByAndOr(expression);
        var result = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var (field, op, value) = ParseCondition(trimmed);
            if (field == null)
            {
                result.Add(trimmed);
                continue;
            }

            var col = table.Columns[field];
            if (col == null)
            {
                result.Add(trimmed);
                continue;
            }

            if (col.DataType == typeof(string) && !string.IsNullOrEmpty(value))
            {
                if (!value.StartsWith("'") && !value.StartsWith("\""))
                    value = $"'{value}'";
            }

            result.Add($"{field} {op} {value}");
        }

        return string.Join(" ", result);
    }

    private static List<string> SplitByAndOr(string expression)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        var tokens = new[] { " AND ", " OR ", " and ", " or " };

        for (var i = 0; i < expression.Length; i++)
        {
            if (!inQuote && (expression[i] == '\'' || expression[i] == '"'))
            {
                inQuote = true;
                quoteChar = expression[i];
                current.Append(expression[i]);
            }
            else if (inQuote && expression[i] == quoteChar)
            {
                inQuote = false;
                current.Append(expression[i]);
            }
            else
            {
                current.Append(expression[i]);
                var currentStr = current.ToString();
                foreach (var token in tokens)
                {
                    if (currentStr.EndsWith(token))
                    {
                        parts.Add(currentStr[..^token.Length].Trim());
                        parts.Add(token.Trim());
                        current.Clear();
                        break;
                    }
                }
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString().Trim());

        return parts;
    }

    private static (string? field, string op, string value) ParseCondition(string condition)
    {
        var operators = new[] { ">=", "<=", "<>", "=", ">", "<", " LIKE ", " like " };

        foreach (var op in operators)
        {
            var index = condition.IndexOf(op, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                var field = condition[..index].Trim();
                var value = condition[(index + op.Length)..].Trim();
                return (field, op.Trim(), value);
            }
        }

        return (null, "", "");
    }
}
