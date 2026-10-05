using System.Globalization;

namespace CoreTheSpire.Engine;

/// <summary>Stable, session-unique instance identity. Never reused once an instance is destroyed.</summary>
public readonly struct InstanceId : IEquatable<InstanceId>, IComparable<InstanceId>
{
    public static readonly InstanceId None = default;

    public InstanceId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }

    public bool IsNone => Value == 0;

    public bool Equals(InstanceId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is InstanceId other && Equals(other);

    public override int GetHashCode() => Value;

    public int CompareTo(InstanceId other) => Value.CompareTo(other.Value);

    public static bool operator ==(InstanceId left, InstanceId right) => left.Value == right.Value;

    public static bool operator !=(InstanceId left, InstanceId right) => left.Value != right.Value;

    public override string ToString() => Value == 0 ? "<none>" : "#" + Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Opaque participant identity. The engine only requires it to be session-stable and opaque.</summary>
public readonly struct ParticipantId : IEquatable<ParticipantId>, IComparable<ParticipantId>
{
    public static readonly ParticipantId None = default;

    public ParticipantId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }

    public bool IsNone => Value == 0;

    public bool Equals(ParticipantId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ParticipantId other && Equals(other);

    public override int GetHashCode() => Value;

    public int CompareTo(ParticipantId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ParticipantId left, ParticipantId right) => left.Value == right.Value;

    public static bool operator !=(ParticipantId left, ParticipantId right) => left.Value != right.Value;

    public override string ToString() => Value == 0 ? "<none>" : "P" + Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identity of one runtime instance of a package-declared scope type.</summary>
public readonly struct ScopeId : IEquatable<ScopeId>, IComparable<ScopeId>
{
    public static readonly ScopeId None = default;

    public ScopeId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public int Value { get; }

    public bool IsNone => Value == 0;

    public bool Equals(ScopeId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ScopeId other && Equals(other);

    public override int GetHashCode() => Value;

    public int CompareTo(ScopeId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ScopeId left, ScopeId right) => left.Value == right.Value;

    public static bool operator !=(ScopeId left, ScopeId right) => left.Value != right.Value;

    public override string ToString() => Value == 0 ? "<none>" : "S" + Value.ToString(CultureInfo.InvariantCulture);
}

public enum ValueKind : byte
{
    None = 0,
    Bool = 1,
    Int = 2,
    Fixed = 3,
    Enum = 4,
    String = 5,
    Ref = 6,
    Participant = 7,
    RefList = 8,
    IntList = 9,
    Scope = 10,
}

/// <summary>
/// A typed engine value. The kind is part of the value; numeric kinds never convert implicitly.
/// Strings, enum ordinals and instance references are validated against the declared field type when stored.
/// </summary>
public readonly struct Value : IEquatable<Value>
{
    private readonly long _number;
    private readonly decimal _fixed;
    private readonly bool _bool;
    private readonly string? _text;
    private readonly InstanceId _reference;
    private readonly InstanceId[]? _references;
    private readonly long[]? _integers;
    private readonly IReadOnlyList<InstanceId>? _referenceView;
    private readonly IReadOnlyList<long>? _integerView;

    private Value(ValueKind kind, long number = 0, decimal fixedValue = 0m, bool boolValue = false,
        string? text = null, InstanceId reference = default, InstanceId[]? references = null, long[]? integers = null)
    {
        Kind = kind;
        _number = number;
        _fixed = fixedValue;
        _bool = boolValue;
        _text = text;
        _reference = reference;
        _references = references;
        _integers = integers;
        _referenceView = references is null ? null : Array.AsReadOnly(references);
        _integerView = integers is null ? null : Array.AsReadOnly(integers);
    }

    public ValueKind Kind { get; }

    public bool IsNone => Kind == ValueKind.None;

    public static readonly Value None = new(ValueKind.None);

    public static Value Of(bool value) => new(ValueKind.Bool, boolValue: value);

    public static Value Of(long value) => new(ValueKind.Int, number: value);

    public static Value Of(decimal value) => new(ValueKind.Fixed, fixedValue: value);

    public static Value OfEnum(int ordinal) => new(ValueKind.Enum, number: ordinal);

    public static Value Of(string value) => new(ValueKind.String, text: value ?? throw new ArgumentNullException(nameof(value)));

    public static Value OfRef(InstanceId reference) => new(ValueKind.Ref, reference: reference);

    public static Value OfParticipant(ParticipantId participant) => new(ValueKind.Participant, number: participant.Value);

    public static Value OfScope(ScopeId scope) => new(ValueKind.Scope, number: scope.Value);

    public static Value OfRefs(IEnumerable<InstanceId> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var array = references.ToArray();
        return new Value(ValueKind.RefList, references: array);
    }

    public static Value OfRefs(params InstanceId[] references) => OfRefs((IEnumerable<InstanceId>)references);

    public static Value OfInts(IEnumerable<long> integers)
    {
        ArgumentNullException.ThrowIfNull(integers);
        return new Value(ValueKind.IntList, integers: integers.ToArray());
    }

    public static Value OfInts(params long[] integers) => OfInts((IEnumerable<long>)integers);

    public bool AsBool() => Kind == ValueKind.Bool
        ? _bool
        : throw TypeError(ValueKind.Bool);

    public long AsInt() => Kind == ValueKind.Int
        ? _number
        : throw TypeError(ValueKind.Int);

    public int AsEnumOrdinal() => Kind == ValueKind.Enum
        ? checked((int)_number)
        : throw TypeError(ValueKind.Enum);

    public decimal AsDecimal() => Kind == ValueKind.Fixed
        ? _fixed
        : throw TypeError(ValueKind.Fixed);

    public string AsString() => Kind == ValueKind.String
        ? _text!
        : throw TypeError(ValueKind.String);

    public InstanceId AsRef() => Kind == ValueKind.Ref
        ? _reference
        : throw TypeError(ValueKind.Ref);

    public ParticipantId AsParticipant() => Kind == ValueKind.Participant
        ? new ParticipantId(checked((int)_number))
        : throw TypeError(ValueKind.Participant);

    public ScopeId AsScope() => Kind == ValueKind.Scope
        ? new ScopeId(checked((int)_number))
        : throw TypeError(ValueKind.Scope);

    public IReadOnlyList<InstanceId> AsRefList() => Kind == ValueKind.RefList
        ? _referenceView!
        : throw TypeError(ValueKind.RefList);

    public IReadOnlyList<long> AsIntList() => Kind == ValueKind.IntList
        ? _integerView!
        : throw TypeError(ValueKind.IntList);

    private EngineFaultException TypeError(ValueKind expected) =>
        Faults.TypeError($"expected {expected} but the value is {Kind}");

    public bool Equals(Value other)
    {
        if (Kind != other.Kind) return false;
        return Kind switch
        {
            ValueKind.None => true,
            ValueKind.Bool => _bool == other._bool,
            ValueKind.Int => _number == other._number,
            ValueKind.Fixed => _fixed == other._fixed,
            ValueKind.Enum => _number == other._number,
            ValueKind.String => string.Equals(_text, other._text, StringComparison.Ordinal),
            ValueKind.Ref => _reference == other._reference,
            ValueKind.Participant => _number == other._number,
            ValueKind.Scope => _number == other._number,
            ValueKind.RefList => ReferenceListEquals(_references!, other._references!),
            ValueKind.IntList => IntegerListEquals(_integers!, other._integers!),
            _ => false,
        };
    }

    private static bool IntegerListEquals(long[] left, long[] right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    private static bool ReferenceListEquals(InstanceId[] left, InstanceId[] right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    public override int GetHashCode() => Kind switch
    {
        ValueKind.Bool => _bool ? 1 : 0,
        ValueKind.Int or ValueKind.Enum or ValueKind.Participant or ValueKind.Scope => _number.GetHashCode(),
        ValueKind.Fixed => _fixed.GetHashCode(),
        ValueKind.String => _text!.GetHashCode(StringComparison.Ordinal),
        ValueKind.Ref => _reference.GetHashCode(),
        ValueKind.RefList => _references!.Length,
        ValueKind.IntList => _integers!.Length,
        _ => 0,
    };

    public override string ToString() => Kind switch
    {
        ValueKind.None => "<none>",
        ValueKind.Bool => _bool ? "true" : "false",
        ValueKind.Int => _number.ToString(CultureInfo.InvariantCulture),
        ValueKind.Fixed => _fixed.ToString(CultureInfo.InvariantCulture),
        ValueKind.Enum => "enum#" + _number.ToString(CultureInfo.InvariantCulture),
        ValueKind.String => "\"" + _text + "\"",
        ValueKind.Ref => _reference.ToString(),
        ValueKind.Participant => new ParticipantId(checked((int)_number)).ToString(),
        ValueKind.Scope => new ScopeId(checked((int)_number)).ToString(),
        ValueKind.RefList => "[" + string.Join(",", _references!.Select(r => r.ToString())) + "]",
        ValueKind.IntList => "[" + string.Join(",", _integers!) + "]",
        _ => "?",
    };

    public static bool operator ==(Value left, Value right) => left.Equals(right);

    public static bool operator !=(Value left, Value right) => !left.Equals(right);
}

/// <summary>
/// Declared numeric semantics. Fixed-point arithmetic keeps an explicit scale and rounding mode;
/// binary64 never appears implicitly. Integer arithmetic is checked when <see cref="CheckedIntegers"/> is set.
/// </summary>
public sealed record NumericProfile
{
    public static readonly NumericProfile Default = new(2, MidpointRounding.ToEven, true);

    public NumericProfile(int fixedScale, MidpointRounding rounding, bool checkedIntegers)
    {
        if (fixedScale is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedScale), fixedScale, "fixed-point scale must be within 0..12.");
        }

        if (!Enum.IsDefined(rounding))
        {
            throw new ArgumentOutOfRangeException(nameof(rounding), rounding, "unknown midpoint rounding mode.");
        }

        FixedScale = fixedScale;
        Rounding = rounding;
        CheckedIntegers = checkedIntegers;
    }

    public int FixedScale { get; init; }

    public MidpointRounding Rounding { get; init; }

    public bool CheckedIntegers { get; init; }

    public decimal Quantize(decimal value)
    {
        try
        {
            return Math.Round(value, FixedScale, Rounding);
        }
        catch (OverflowException exception)
        {
            throw Faults.Arithmetic("fixed-point quantize overflowed", exception.Message);
        }
    }

    public decimal AddFixed(decimal left, decimal right) => Quantize(GuardFixed(() => left + right, "add"));

    public decimal SubtractFixed(decimal left, decimal right) => Quantize(GuardFixed(() => left - right, "subtract"));

    public decimal MultiplyFixed(decimal left, decimal right) => Quantize(GuardFixed(() => left * right, "multiply"));

    public decimal DivideFixed(decimal left, decimal right)
    {
        if (right == 0m) throw Faults.DivisionByZero("fixed-point division by zero");
        return Quantize(GuardFixed(() => left / right, "divide"));
    }

    public long AddInt(long left, long right) => CheckedIntegers
        ? GuardInt(() => checked(left + right), "add")
        : unchecked(left + right);

    public long SubtractInt(long left, long right) => CheckedIntegers
        ? GuardInt(() => checked(left - right), "subtract")
        : unchecked(left - right);

    public long MultiplyInt(long left, long right) => CheckedIntegers
        ? GuardInt(() => checked(left * right), "multiply")
        : unchecked(left * right);

    public long DivideInt(long left, long right)
    {
        if (right == 0) throw Faults.DivisionByZero("integer division by zero");
        if (CheckedIntegers && left == long.MinValue && right == -1)
            throw Faults.Arithmetic("integer division overflowed");
        return left / right;
    }

    public long ModuloInt(long left, long right)
    {
        if (right == 0) throw Faults.DivisionByZero("integer modulo by zero");
        return left % right;
    }

    /// <summary>Fixed-point to integer conversion. The rounding rule must be explicit at the call site.</summary>
    public long FixedToInt(decimal value, FixedRounding rounding) => rounding switch
    {
        FixedRounding.Truncate => GuardInt(() => decimal.ToInt64(decimal.Truncate(value)), "truncate"),
        FixedRounding.Floor => GuardInt(() => decimal.ToInt64(decimal.Floor(value)), "floor"),
        FixedRounding.Ceiling => GuardInt(() => decimal.ToInt64(decimal.Ceiling(value)), "ceiling"),
        FixedRounding.Round => GuardInt(() => decimal.ToInt64(Math.Round(value, 0, Rounding)), "round"),
        _ => throw Faults.Arithmetic("unknown fixed-point rounding mode"),
    };

    public decimal IntToFixed(long value) => value;

    private static decimal GuardFixed(Func<decimal> operation, string name)
    {
        try
        {
            return operation();
        }
        catch (OverflowException exception)
        {
            throw Faults.Arithmetic($"fixed-point {name} overflowed", exception.Message);
        }
    }

    private long GuardInt(Func<long> operation, string name)
    {
        try
        {
            return operation();
        }
        catch (OverflowException exception)
        {
            throw Faults.Arithmetic($"integer {name} overflowed", exception.Message);
        }
    }

    private decimal Guard(decimal value, string operation) => GuardFixed(() => value, operation);

    private long GuardInt(long value, string operation) => GuardInt(() => value, operation);
}

public enum FixedRounding
{
    Truncate = 0,
    Floor = 1,
    Ceiling = 2,
    Round = 3,
}

public enum EngineFaultCode
{
    SchemaInvalid = 0,
    ReferenceUnresolved = 1,
    DuplicateDeclaration = 2,
    MissingDependency = 3,
    DependencyCycle = 4,
    UnauthorizedOverride = 5,
    PackageAlreadyBuilt = 6,
    TypeError = 7,
    ValueOutOfRange = 8,
    InvalidReference = 9,
    CapacityExceeded = 10,
    MembershipConflict = 11,
    UnknownEntity = 12,
    UnknownProcedure = 13,
    UnknownDeclaration = 14,
    PurityViolation = 15,
    IllegalAction = 16,
    UnauthorizedAction = 17,
    StaleDecision = 18,
    InvalidDecisionResponse = 19,
    BudgetExceeded = 20,
    ComputeFailure = 21,
    ComputeOutputInvalid = 22,
    ComputeLimitExceeded = 23,
    ArithmeticOverflow = 24,
    DivisionByZero = 25,
    SessionStateInvalid = 26,
    TerminalReached = 27,
    SnapshotInvalid = 28,
    NoProjection = 29,
    Unsupported = 30,

    /// <summary>
    /// An instance still has a non-optional reference field without a live target, and the session reached a stable
    /// boundary (command end, decision, terminal result, projection or state hash) that must never publish it.
    /// </summary>
    RequiredRefUnassigned = 31,
}

/// <summary>Structured engine fault. A fault is a host diagnostics event, never a package-declared result.</summary>
public sealed record EngineFault(EngineFaultCode Code, string Message, string? Context = null)
{
    public override string ToString() => Context is null
        ? $"{Code}: {Message}"
        : $"{Code}: {Message} ({Context})";
}

public sealed class EngineFaultException : Exception
{
    public EngineFaultException(EngineFault fault, Exception? inner = null)
        : base(fault.ToString(), inner)
    {
        Fault = fault;
    }

    public EngineFault Fault { get; }

    public EngineFaultCode Code => Fault.Code;
}

public static class Faults
{
    public static EngineFaultException Of(EngineFaultCode code, string message, string? context = null) =>
        new(new EngineFault(code, message, context));

    public static EngineFaultException Schema(string message, string? context = null) =>
        Of(EngineFaultCode.SchemaInvalid, message, context);

    public static EngineFaultException Unresolved(string message, string? context = null) =>
        Of(EngineFaultCode.ReferenceUnresolved, message, context);

    public static EngineFaultException Type(string message, string? context = null) =>
        Of(EngineFaultCode.TypeError, message, context);

    public static EngineFaultException TypeError(string message, string? context = null) =>
        Of(EngineFaultCode.TypeError, message, context);

    public static EngineFaultException Range(string message, string? context = null) =>
        Of(EngineFaultCode.ValueOutOfRange, message, context);

    public static EngineFaultException Purity(string message, string? context = null) =>
        Of(EngineFaultCode.PurityViolation, message, context);

    public static EngineFaultException Illegal(string message, string? context = null) =>
        Of(EngineFaultCode.IllegalAction, message, context);

    public static EngineFaultException Arithmetic(string message, string? context = null) =>
        Of(EngineFaultCode.ArithmeticOverflow, message, context);

    public static EngineFaultException DivisionByZero(string message, string? context = null) =>
        Of(EngineFaultCode.DivisionByZero, message, context);

    public static EngineFaultException Budget(string message, string? context = null) =>
        Of(EngineFaultCode.BudgetExceeded, message, context);

    public static EngineFaultException Compute(string message, string? context = null) =>
        Of(EngineFaultCode.ComputeFailure, message, context);

    public static EngineFaultException RequiredRef(string message, string? context = null) =>
        Of(EngineFaultCode.RequiredRefUnassigned, message, context);
}
