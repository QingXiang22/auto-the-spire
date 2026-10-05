using Antlr4.Runtime;
using CoreTheSpire.Engine.Compilation.Generated;
using System.Text.Json;

namespace CoreTheSpire.Engine;

/// <summary>The versioned core grammar produces the shared, source-located AST. It has no rule semantics.</summary>
public static class EngineDslParser
{
    public static DslPackageNode Parse(string source, string sourceName = "<memory>")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        var errors = new SyntaxErrors(sourceName);
        var lexer = new CoreSpireLexer(new AntlrInputStream(source));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errors);
        var parser = new CoreSpireParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errors);
        var root = parser.document();
        return new DslPackageNode(root.Identifier().GetText(), Members(root.members()), At(root.Start));

        DslSourceLocation At(IToken token) => new(sourceName, token.Line, token.Column + 1);

        IReadOnlyList<DslMemberNode> Members(CoreSpireParser.MembersContext block) =>
            Array.AsReadOnly(block.member().Where(member => member is not CoreSpireParser.EmptyMemberContext)
                .Select(Member).ToArray());

        DslMemberNode Member(CoreSpireParser.MemberContext member) => member switch
        {
            CoreSpireParser.DeclarationMemberContext declaration => new DslDeclarationNode(
                declaration.Identifier(0).GetText(), declaration.Identifier(1).GetText(),
                Members(declaration.members()), At(declaration.Start)),
            CoreSpireParser.PropertyMemberContext property => new DslPropertyNode(
                property.Identifier().GetText(), Value(property.value()), At(property.Start)),
            CoreSpireParser.FlagMemberContext flag => new DslPropertyNode(
                flag.Identifier().GetText(), new DslBooleanNode(true, At(flag.Start)), At(flag.Start)),
            _ => throw new InvalidOperationException("Unknown grammar member."),
        };

        DslValueNode Value(CoreSpireParser.ValueContext value) => value switch
        {
            CoreSpireParser.NameValueContext name => new DslNameNode(name.GetText(), At(name.Start)),
            CoreSpireParser.NumberValueContext number => new DslNumberNode(number.GetText(), At(number.Start)),
            CoreSpireParser.StringValueContext text => new DslStringNode(
                JsonSerializer.Deserialize<string>(text.GetText())!, At(text.Start)),
            CoreSpireParser.BoolValueContext boolean => new DslBooleanNode(
                boolean.GetText() == "true", At(boolean.Start)),
            CoreSpireParser.BlockValueContext block => new DslBlockNode(Members(block.members()), At(block.Start)),
            CoreSpireParser.ListValueContext list => new DslListNode(
                Array.AsReadOnly(list.value().Select(Value).ToArray()), At(list.Start)),
            CoreSpireParser.CallValueContext call => new DslCallNode(call.Identifier().GetText(),
                Array.AsReadOnly((call.arguments()?.argument() ?? []).Select(argument =>
                    new DslCallArgumentNode(argument.Identifier()?.GetText(), Value(argument.value()),
                        At(argument.Start))).ToArray()), At(call.Start)),
            _ => throw new InvalidOperationException("Unknown grammar value."),
        };
    }

    private sealed class SyntaxErrors(string sourceName) : BaseErrorListener, IAntlrErrorListener<int>
    {
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            throw new DslSourceException(new DslSourceLocation(sourceName, line, charPositionInLine + 1), msg);

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            throw new DslSourceException(new DslSourceLocation(sourceName, line, charPositionInLine + 1), msg);
    }
}
