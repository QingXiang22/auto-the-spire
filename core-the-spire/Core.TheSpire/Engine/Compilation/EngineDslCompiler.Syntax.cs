namespace CoreTheSpire.Engine;

public static partial class EngineDslCompiler
{
    private sealed partial class CoreDslBinding
    {
        private sealed class Properties
        {
            private readonly Dictionary<string, DslPropertyNode> _values = new(StringComparer.Ordinal);
            private readonly DslSourceLocation _location;

            public Properties(IEnumerable<DslMemberNode> members, DslSourceLocation location, params string[] allowed)
            {
                _location = location;
                foreach (var member in members)
                {
                    if (member is not DslPropertyNode property)
                        throw Error(member.Location, "Expected a property, not a nested declaration.");
                    if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                        throw Error(property.Location, $"Unknown property '{property.Name}'.");
                    if (!_values.TryAdd(property.Name, property))
                        throw Error(property.Location, $"Duplicate property '{property.Name}'.");
                }
            }

            public DslValueNode? Get(string name) => _values.GetValueOrDefault(name)?.Value;
            public DslValueNode Required(string name) => Get(name) ?? throw Error(_location, $"Missing '{name}'.");

            public static Properties Of(DslValueNode value, params string[] allowed) =>
                new(Members(value), value.Location, allowed);

            public static IReadOnlyList<DslMemberNode> Members(DslValueNode value) => value is DslBlockNode block
                ? block.Members : throw Error(value.Location, "Expected a block {...}.");

            public static IReadOnlyList<DslPropertyNode> Items(DslValueNode value)
            {
                var properties = Members(value).Select(member => member is DslPropertyNode property ? property :
                    throw Error(member.Location, "Expected a named property.")).ToArray();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in properties)
                    if (!names.Add(property.Name)) throw Error(property.Location, $"Duplicate property '{property.Name}'.");
                return properties;
            }
        }

        private sealed class Arguments
        {
            private readonly DslCallNode _call;
            private readonly HashSet<int> _used = [];

            public Arguments(DslCallNode call)
            {
                _call = call;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var argument in call.Arguments)
                    if (argument.Name is { } name && !names.Add(name))
                        throw Error(argument.Location, $"Duplicate argument '{name}'.");
            }

            public DslValueNode Required(string name, int position = -1) => Optional(name, position) ??
                throw Error(_call.Location, $"'{_call.Name}' needs argument '{name}'.");

            public DslValueNode? Optional(string name, int position = -1)
            {
                var named = -1;
                for (var i = 0; i < _call.Arguments.Count; i++)
                    if (_call.Arguments[i].Name == name) named = i;
                var positional = position >= 0 && position < _call.Arguments.Count &&
                    _call.Arguments[position].Name is null ? position : -1;
                if (named >= 0 && positional >= 0)
                    throw Error(_call.Arguments[named].Location, $"Argument '{name}' was supplied twice.");
                var index = named >= 0 ? named : positional;
                if (index < 0) return null;
                _used.Add(index);
                return _call.Arguments[index].Value;
            }

            public IReadOnlyList<DslValueNode> Rest(int start)
            {
                var values = new List<DslValueNode>();
                for (var i = start; i < _call.Arguments.Count; i++)
                {
                    if (_used.Contains(i)) continue;
                    if (_call.Arguments[i].Name is not null)
                        throw Error(_call.Arguments[i].Location, "Remaining arguments must be positional.");
                    _used.Add(i);
                    values.Add(_call.Arguments[i].Value);
                }
                return values;
            }

            public void Finish()
            {
                for (var i = 0; i < _call.Arguments.Count; i++)
                    if (!_used.Contains(i)) throw Error(_call.Arguments[i].Location,
                        $"Unknown or excess argument '{_call.Arguments[i].Name ?? i.ToString()}' in '{_call.Name}'.");
            }
        }
    }
}
