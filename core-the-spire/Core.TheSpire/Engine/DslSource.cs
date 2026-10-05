using System.Globalization;
using System.Text;

namespace CoreTheSpire;

public readonly record struct DslSourceLocation(string Source, int Line, int Column)
{
    public override string ToString() => $"{Source}({Line},{Column})";
}

public sealed class DslSourceException(DslSourceLocation location, string message)
    : FormatException($"{location}: {message}")
{
    public DslSourceLocation Location { get; } = location;
}

public sealed record DslPackageNode(string Name, IReadOnlyList<DslMemberNode> Members, DslSourceLocation Location);

public abstract record DslMemberNode(DslSourceLocation Location);

public sealed record DslDeclarationNode(string Kind, string Name, IReadOnlyList<DslMemberNode> Members,
    DslSourceLocation Location) : DslMemberNode(Location);

public sealed record DslPropertyNode(string Name, DslValueNode Value, DslSourceLocation Location)
    : DslMemberNode(Location);

public sealed record DslBlockNode(IReadOnlyList<DslMemberNode> Members, DslSourceLocation Location)
    : DslValueNode(Location);

public abstract record DslValueNode(DslSourceLocation Location);

public sealed record DslNameNode(string Name, DslSourceLocation Location) : DslValueNode(Location);
public sealed record DslStringNode(string Value, DslSourceLocation Location) : DslValueNode(Location);
public sealed record DslNumberNode(string Literal, DslSourceLocation Location) : DslValueNode(Location)
{
    public bool TryGetInt32(out int value) => int.TryParse(Literal, NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture, out value);
    public bool TryGetDecimal(out decimal value) => decimal.TryParse(Literal, NumberStyles.Float,
        CultureInfo.InvariantCulture, out value);
}
public sealed record DslBooleanNode(bool Value, DslSourceLocation Location) : DslValueNode(Location);
public sealed record DslListNode(IReadOnlyList<DslValueNode> Items, DslSourceLocation Location)
    : DslValueNode(Location);
public sealed record DslCallArgumentNode(string? Name, DslValueNode Value, DslSourceLocation Location);
public sealed record DslCallNode(string Name, IReadOnlyList<DslCallArgumentNode> Arguments, DslSourceLocation Location)
    : DslValueNode(Location);

/// <summary>Parses the package-oriented .spire source syntax into a location-preserving syntax tree.</summary>
public static class DslSourceParser
{
    private enum TokenKind { Identifier, Number, String, LeftBrace, RightBrace, LeftParen, RightParen,
        LeftBracket, RightBracket, Comma, Equals, Semicolon, End }

    private readonly record struct Token(TokenKind Kind, string Text, DslSourceLocation Location);

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _index;

        public Parser(string text, string sourceName)
        {
            _tokens = Lex(text, sourceName);
        }

        public DslPackageNode Parse()
        {
            if (Current.Kind == TokenKind.Identifier && Current.Text == "using")
                return ParseNamespacePackage();

            var packageToken = ExpectIdentifier("package");
            var name = Expect(TokenKind.Identifier, "package name");
            Expect(TokenKind.LeftBrace, "'{' after package name");
            var members = ParseMembers();
            Expect(TokenKind.RightBrace, "'}' after package body");
            if (Current.Kind == TokenKind.Semicolon) Advance();
            Expect(TokenKind.End, "end of source");
            return new DslPackageNode(name.Text, Array.AsReadOnly(members.ToArray()), packageToken.Location);
        }

        private DslPackageNode ParseNamespacePackage()
        {
            var usingToken = ExpectIdentifier("using");
            ExpectIdentifier("namespace");
            var name = Expect(TokenKind.Identifier, "namespace name");
            Expect(TokenKind.Semicolon, "';' after namespace");
            var members = ParseMembers(untilEnd: true);
            Expect(TokenKind.End, "end of source");
            return new DslPackageNode(name.Text, Array.AsReadOnly(members.ToArray()), usingToken.Location);
        }

        private List<DslMemberNode> ParseMembers(bool untilEnd = false)
        {
            var members = new List<DslMemberNode>();
            while (untilEnd ? Current.Kind != TokenKind.End : Current.Kind != TokenKind.RightBrace)
            {
                if (Current.Kind == TokenKind.End)
                {
                    if (untilEnd) break;
                    throw Error(Current, "Expected '}' to close this block.");
                }
                if (Current.Kind == TokenKind.Semicolon)
                {
                    Advance();
                    continue;
                }
                if (Current.Kind != TokenKind.Identifier)
                    throw Error(Current, "Expected a declaration or property name.");

                if (Peek(1).Kind is not TokenKind.Equals
                    && !(Peek(1).Kind == TokenKind.Identifier && Peek(2).Kind == TokenKind.LeftBrace))
                {
                    var item = Advance();
                    members.Add(new DslPropertyNode(item.Text, new DslBooleanNode(true, item.Location), item.Location));
                    if (Current.Kind == TokenKind.Comma || Current.Kind == TokenKind.Semicolon) Advance();
                    continue;
                }

                if (Peek(1).Kind == TokenKind.Identifier && Peek(2).Kind == TokenKind.LeftBrace)
                {
                    var kind = Advance();
                    var name = Advance();
                    Advance();
                    var nestedMembers = ParseMembers();
                    Expect(TokenKind.RightBrace, "'}' after declaration body");
                    members.Add(new DslDeclarationNode(kind.Text, name.Text,
                        Array.AsReadOnly(nestedMembers.ToArray()), kind.Location));
                    if (Current.Kind == TokenKind.Semicolon) Advance();
                    continue;
                }

                var property = Advance();
                Expect(TokenKind.Equals, "'=' after property name");
                members.Add(new DslPropertyNode(property.Text, ParseValue(), property.Location));
                if (Current.Kind == TokenKind.Semicolon) Advance();
            }
            return members;
        }

        private DslValueNode ParseValue()
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Identifier:
                    Advance();
                    if (Current.Kind == TokenKind.LeftParen) return ParseCall(token);
                    return token.Text switch
                    {
                        "true" => new DslBooleanNode(true, token.Location),
                        "false" => new DslBooleanNode(false, token.Location),
                        _ => new DslNameNode(token.Text, token.Location)
                    };
                case TokenKind.Number:
                    Advance();
                    return new DslNumberNode(token.Text, token.Location);
                case TokenKind.String:
                    Advance();
                    return new DslStringNode(token.Text, token.Location);
                case TokenKind.LeftBrace:
                    Advance();
                    var members = ParseMembers();
                    Expect(TokenKind.RightBrace, "'}' after block");
                    return new DslBlockNode(Array.AsReadOnly(members.ToArray()), token.Location);
                case TokenKind.LeftBracket:
                    Advance();
                    var items = new List<DslValueNode>();
                    if (Current.Kind != TokenKind.RightBracket)
                    {
                        do
                        {
                            items.Add(ParseValue());
                            if (Current.Kind != TokenKind.Comma) break;
                            Advance();
                        } while (Current.Kind != TokenKind.RightBracket);
                    }
                    Expect(TokenKind.RightBracket, "']' after list items");
                    return new DslListNode(Array.AsReadOnly(items.ToArray()), token.Location);
                default:
                    throw Error(token, "Expected a value, call, or list.");
            }
        }

        private DslCallNode ParseCall(Token name)
        {
            Expect(TokenKind.LeftParen, "'('");
            var arguments = new List<DslCallArgumentNode>();
            if (Current.Kind != TokenKind.RightParen)
            {
                while (true)
                {
                    if (Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Equals)
                    {
                        var argumentName = Advance();
                        Advance();
                        arguments.Add(new DslCallArgumentNode(argumentName.Text, ParseValue(), argumentName.Location));
                    }
                    else
                    {
                        var value = ParseValue();
                        arguments.Add(new DslCallArgumentNode(null, value, value.Location));
                    }

                    if (Current.Kind != TokenKind.Comma) break;
                    Advance();
                    if (Current.Kind == TokenKind.RightParen) break;
                }
            }
            Expect(TokenKind.RightParen, "')' after call arguments");
            return new DslCallNode(name.Text, Array.AsReadOnly(arguments.ToArray()), name.Location);
        }

        private Token ExpectIdentifier(string expected)
        {
            if (Current.Kind != TokenKind.Identifier || Current.Text != expected)
                throw Error(Current, $"Expected '{expected}'.");
            return Advance();
        }

        private Token Expect(TokenKind kind, string expected)
        {
            if (Current.Kind != kind) throw Error(Current, $"Expected {expected}.");
            return Advance();
        }

        private DslSourceException Error(Token token, string message) => new(token.Location, message);
        private Token Current => _tokens[_index];
        private Token Peek(int offset) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];
        private Token Advance() => _tokens[_index++];
    }

    public static DslPackageNode Parse(string source, string sourceName = "<memory>")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        return new Parser(source, sourceName).Parse();
    }

    private static List<Token> Lex(string text, string sourceName)
    {
        var tokens = new List<Token>();
        var offset = 0;
        var line = 1;
        var column = 1;
        while (offset < text.Length)
        {
            var current = text[offset];
            if (char.IsWhiteSpace(current))
            {
                AdvancePosition(current, ref line, ref column);
                offset++;
                continue;
            }
            if (current == '#')
            {
                while (offset < text.Length && text[offset] is not ('\r' or '\n'))
                {
                    AdvancePosition(text[offset], ref line, ref column);
                    offset++;
                }
                continue;
            }

            var location = new DslSourceLocation(sourceName, line, column);
            if (IsIdentifierStart(current))
            {
                var start = offset++;
                column++;
                while (offset < text.Length && IsIdentifierPart(text[offset]))
                {
                    offset++;
                    column++;
                }
                tokens.Add(new Token(TokenKind.Identifier, text[start..offset], location));
                continue;
            }
            if (char.IsAsciiDigit(current) || (current == '-' && offset + 1 < text.Length && char.IsAsciiDigit(text[offset + 1])))
            {
                var start = offset++;
                column++;
                while (offset < text.Length && char.IsAsciiDigit(text[offset]))
                {
                    offset++;
                    column++;
                }
                if (offset + 1 < text.Length && text[offset] == '.' && text[offset + 1] != '.')
                {
                    offset++;
                    column++;
                    while (offset < text.Length && char.IsAsciiDigit(text[offset]))
                    {
                        offset++;
                        column++;
                    }
                }
                tokens.Add(new Token(TokenKind.Number, text[start..offset], location));
                continue;
            }
            if (current == '"')
            {
                tokens.Add(ReadString(text, sourceName, ref offset, ref line, ref column));
                continue;
            }

            var kind = current switch
            {
                '{' => TokenKind.LeftBrace,
                '}' => TokenKind.RightBrace,
                '(' => TokenKind.LeftParen,
                ')' => TokenKind.RightParen,
                '[' => TokenKind.LeftBracket,
                ']' => TokenKind.RightBracket,
                ',' => TokenKind.Comma,
                '=' => TokenKind.Equals,
                ';' => TokenKind.Semicolon,
                _ => throw new DslSourceException(location, $"Unexpected character '{current}'.")
            };
            tokens.Add(new Token(kind, current.ToString(), location));
            offset++;
            column++;
        }
        tokens.Add(new Token(TokenKind.End, string.Empty, new DslSourceLocation(sourceName, line, column)));
        return tokens;
    }

    private static Token ReadString(string text, string sourceName, ref int offset, ref int line, ref int column)
    {
        var location = new DslSourceLocation(sourceName, line, column);
        offset++;
        column++;
        var value = new StringBuilder();
        while (offset < text.Length)
        {
            var current = text[offset++];
            column++;
            if (current == '"') return new Token(TokenKind.String, value.ToString(), location);
            if (current is '\r' or '\n') throw new DslSourceException(location, "String literals cannot contain raw newlines.");
            if (current != '\\')
            {
                value.Append(current);
                continue;
            }
            if (offset >= text.Length) break;
            var escaped = text[offset++];
            column++;
            value.Append(escaped switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => throw new DslSourceException(location, $"Unsupported string escape '\\{escaped}'.")
            });
        }
        throw new DslSourceException(location, "Unterminated string literal.");
    }

    private static bool IsIdentifierStart(char value) => char.IsAsciiLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => char.IsAsciiLetterOrDigit(value)
        || value is '_' or '-' or ':' or '.';

    private static void AdvancePosition(char value, ref int line, ref int column)
    {
        if (value == '\n')
        {
            line++;
            column = 1;
        }
        else if (value != '\r') column++;
    }
}
