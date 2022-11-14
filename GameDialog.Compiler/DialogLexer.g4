lexer grammar DialogLexer;
@lexer::header {#pragma warning disable 3021}
tokens { INDENT, DEDENT }

channels { WHITESPACE, COMMENTS}

options { superClass=DialogLexerBase; }

WS : SPACE+ -> channel(HIDDEN);
COMMENT: '//' ~[\r|\n]* -> channel(COMMENTS);
NEWLINE: ('\r'?'\n'|'\r') SPACE*;
TITLE_EDGE: '--' -> pushMode(TitleMode);
CHOICE: '?' ' '+ -> pushMode(ChoiceMode);
IF: 'if' -> pushMode(ExpressionMode);
ELSEIF: 'else if' -> pushMode(ExpressionMode);
ELSE: 'else';
MAIN_TAG_ENTER: TAG_ENTER -> type(TAG_ENTER), pushMode(ExpressionMode);
SPEAKER_NAME: NAME -> type(NAME), pushMode(SpeakerMode);
ANY: . ;

mode TitleMode;
TITLE_NAME: NAME -> type(NAME);
TITLE_END: TITLE_EDGE -> type(TITLE_EDGE), popMode;
TITLE_ANY: ANY -> type(ANY);

mode SpeakerMode;
NAME_SEPARATOR: ',' WS;
EXTRA_NAME: NAME -> type(NAME);
ML_EDGE: ':' ' '+ '^^' -> popMode, pushMode(MLTextMode);
LINE_ENTER: ':' ' '+ -> popMode, pushMode(LineTextMode);
SPEAKER_ANY: ANY -> more, popMode, pushMode(ExpressionMode);

mode LineTextMode;
LINE_TAG_ENTER: TAG_ENTER (WS* '/')? -> type(TAG_ENTER), pushMode(ExpressionMode);
TEXT: (STRING_ESCAPE_SEQ | ~[[\\\r\n])+;
TEXT_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
TEXT_ANY: ANY -> type(ANY);

mode MLTextMode;
ML_TAG_ENTER: TAG_ENTER (WS* '/')? -> type(TAG_ENTER), pushMode(ExpressionMode);
ML_EXIT: '^^' -> type(ML_EDGE), popMode;
ML_TEXT: (STRING_ESCAPE_SEQ | ~[[^\\\r\n])+ -> type(TEXT);
ML_NEWLINE: NEWLINE -> skip;
ML_ANY: ANY -> type(ANY);

mode ChoiceMode;
CHOICE_TEXT: (STRING_ESCAPE_SEQ | ~[[\\\r\n])+ -> type(TEXT);
CHOICE_TAG_ENTER: TAG_ENTER -> type(TAG_ENTER), pushMode(ExpressionMode);
CHOICE_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
CHOICE_ANY: ANY -> type(ANY);

mode ExpressionMode;
OPEN_PAREN : '(';
CLOSE_PAREN : ')';
EXP_WS: WS -> skip;
BOOL: ('true'|'false');
STRING: '"' (~('"'|'\\'|'\r'|'\n') | '\\'('"'|'\\'))* '"';
FLOAT: INT ('.'INT)*;
OP_ASSIGN: '=';
OP_MULT_ASSIGN: '*=';
OP_DIVIDE_ASSIGN: '/=';
OP_ADD_ASSIGN: '+=';
OP_SUB_ASSIGN: '-=';
OP_LESS_EQUALS: '<=';
OP_GREATER_EQUALS: '>=';
OP_EQUALS: '==';
OP_LESS: '<';
OP_GREATER: '>';
OP_NOT_EQUALS: '!=';
OP_AND: '&&';
OP_OR: '||';
OP_NOT: '!';
OP_MULT: '*';
OP_DIVIDE: '/';
OP_ADD: '+';
OP_SUB: '-';
COMMA: ',';
NAME: NAME_START NAME_CONTINUE*;
TAG_ENTER: '[';
TAG_EXIT: ']' -> popMode;
EXP_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
EXP_ANY: ANY -> type(ANY);

fragment STRING_ESCAPE_SEQ
	: '\\' .
	| '\\' NEWLINE;
fragment UNDERSCORE: '_';
fragment NAME_START: [A-Za-z];
fragment NAME_CONTINUE: NAME_START | '_' | DIGIT;
fragment INT: DIGIT+;
fragment DIGIT: [0-9];
fragment SPACE: [ \t];