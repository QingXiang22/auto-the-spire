grammar CoreSpire;

// Syntax only. Name resolution and game-independent semantics belong to the compiler.
document
    : 'using' 'namespace' Identifier ';' members EOF
    | 'package' Identifier '{' members '}' ';'? EOF
    ;
members : member*;
member
    : Identifier Identifier '{' members '}' ';'? # declarationMember
    | Identifier '=' value ';'?                  # propertyMember
    | Identifier (',' | ';')?                    # flagMember
    | ';'                                       # emptyMember
    ;
value
    : Identifier '(' arguments? ')'             # callValue
    | Identifier                                # nameValue
    | Number                                    # numberValue
    | String                                    # stringValue
    | ('true' | 'false')                         # boolValue
    | '{' members '}'                            # blockValue
    | '[' (value (',' value)* ','?)? ']'          # listValue
    ;
arguments : argument (',' argument)* ','?;
argument : (Identifier '=')? value;

Identifier : [a-zA-Z_] [a-zA-Z0-9_.:@/\-]*;
Number : '-'? [0-9]+ ('.' [0-9]+)?;
String : '"' ( '\\' ["\\nrt] | ~["\\\r\n] )* '"';
Comment : '#' ~[\r\n]* -> skip;
Whitespace : [ \t\r\n]+ -> skip;
