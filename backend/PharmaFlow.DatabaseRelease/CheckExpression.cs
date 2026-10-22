using System.Text.RegularExpressions;

namespace PharmaFlow.DatabaseRelease;

// Fail-closed parser for the limited CHECK grammar in InitialCreate. Preserves boolean
// grouping; recognizes PostgreSQL's IN -> ANY and BETWEEN -> comparison deparsing.
// Unsupported expressions fail verification instead of being assumed equivalent.
internal static partial class CheckExpression
{
    internal static string Normalize(string sql)
    {
        var tokens = Tokens().Matches(sql).Select(m => m.Value).ToArray();
        if (string.Concat(tokens) != Regex.Replace(sql, @"\s+", ""))
            throw new ReleaseSafetyException("unsupported-check-expression");
        var position = 0;
        var result = Or();
        if (position != tokens.Length) throw new ReleaseSafetyException("unsupported-check-expression");
        return result;

        string Or()
        {
            var value = And();
            while (Take("OR")) value = "or(" + value + "," + And() + ")";
            return value;
        }
        string And()
        {
            var value = Comparison();
            while (Take("AND")) value = "and(" + value + "," + Comparison() + ")";
            return value;
        }
        string Comparison()
        {
            var left = Atom();
            if (Take("IS")) { var not = Take("NOT"); Require("NULL"); return (not ? "notnull(" : "null(") + left + ")"; }
            if (Take("BETWEEN"))
            {
                var low = Atom(); Require("AND"); var high = Atom();
                return "and(>=(" + left + "," + low + "),<=(" + left + "," + high + "))";
            }
            if (Take("IN")) { Require("("); return "in(" + left + "," + List(")") + ")"; }
            if (position < tokens.Length && tokens[position] is "=" or ">=" or "<=" or ">" or "<" or "<>")
            {
                var op = tokens[position++];
                if (op == "=" && Take("ANY"))
                {
                    Require("("); Require("ARRAY"); Require("["); var values = List("]"); Require(")");
                    return "in(" + left + "," + values + ")";
                }
                return op + "(" + left + "," + Atom() + ")";
            }
            return left;
        }
        string Atom()
        {
            string value;
            if (Take("(")) { value = Or(); Require(")"); }
            else
            {
                if (position >= tokens.Length || !AtomToken().IsMatch(tokens[position]))
                    throw new ReleaseSafetyException("unsupported-check-expression");
                value = tokens[position++];
                if (value.StartsWith('"'))
                {
                    var identifier = value[1..^1];
                    if (!Regex.IsMatch(identifier, @"\A[a-z_][a-z_0-9]*\z", RegexOptions.CultureInvariant))
                        throw new ReleaseSafetyException("unsupported-check-identifier");
                    value = identifier;
                }
            }
            if (Take("::"))
            {
                if (!(Take("text") || Take("bigint") || Take("integer")))
                    throw new ReleaseSafetyException("unsupported-check-cast");
            }
            return value;
        }
        string List(string end)
        {
            var values = new List<string> { Atom() };
            while (Take(",")) values.Add(Atom());
            Require(end);
            return string.Join(',', values);
        }
        bool Take(string value)
        {
            if (position >= tokens.Length || !string.Equals(tokens[position], value, StringComparison.OrdinalIgnoreCase)) return false;
            position++; return true;
        }
        void Require(string value) { if (!Take(value)) throw new ReleaseSafetyException("unsupported-check-expression"); }
    }

    [GeneratedRegex("'(?:''|[^'])*'|\"(?:\"\"|[^\"])*\"|[a-zA-Z_][a-zA-Z_0-9]*|[0-9]+|::|>=|<=|<>|[()\\[\\],=<>]", RegexOptions.CultureInvariant)]
    private static partial Regex Tokens();
    [GeneratedRegex("\\A(?:'(?:''|[^'])*'|\"(?:\"\"|[^\"])*\"|[a-zA-Z_][a-zA-Z_0-9]*|[0-9]+)\\z", RegexOptions.CultureInvariant)]
    private static partial Regex AtomToken();
}
