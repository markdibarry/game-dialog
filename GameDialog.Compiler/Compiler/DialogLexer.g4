lexer grammar DialogLexer;

tokens { INDENT, DEDENT }
options { superClass=DialogLexerBase; }

COMMENT: '//' ~[\r\n]* -> skip;
MULTILINE_COMMENT: '/*' .*? '*/' -> skip;
NEWLINE: ('\r'?'\n'|'\r') WS?;

TITLE: '--' NAME '--';

CHOICE: '?' ' '+ -> pushMode(Line);
IF: 'if' -> pushMode(ExpressionMode);
ELSEIF: 'else if' -> pushMode(ExpressionMode);
ELSE: 'else';
TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(ExpressionMode);
SPEAKER_NAME: (NAME | UNDERSCORE) -> type(NAME), pushMode(Speaker);
ANY: .;

mode Speaker;
NAME_SEPARATOR: ',' WS?;
EXTRA_NAME: NAME -> type(NAME);
LINE_ENTER: ':' SPACE? -> popMode, pushMode(Line);
ML_ENTER: ':' SPACE? '^^' -> popMode, pushMode(MultiLine);
SPEAKER_ANY: ANY -> type(ANY);

mode Line;
LINE_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(ExpressionMode);
LINE_COMMENT: COMMENT;
LINE_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
LINE_TEXT: (ESC | ~[\r\n/[] | ('/' ~[\r\n/[]) )+;
LINE_ANY: ANY -> type(ANY);

mode MultiLine;
ML_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(ExpressionMode);
ML_COMMENT: COMMENT;
ML_NEWLINE: NEWLINE -> type(NEWLINE);
ML_CLOSE: '^^' -> popMode;
ML_TEXT: (ESC | ~[\r\n/[^] | ('/' ~[\r\n/[^]) )+ -> type(LINE_TEXT);
ML_ANY: ANY -> type(ANY);

mode BBCode;
BBCODE_EXTRA_TEXT: ~']'+;
BBCODE_TAG_EXIT: ']' -> type(CLOSE_BRACKET), popMode;
BBCODE_ANY: ANY -> type(ANY);

mode ExpressionMode;
BBCODE_NAME: ('b'|'i'|'u'|'s'|'code'|'char'|'p'|'center'|'right'|'left'|'fill'|'indent'|'url'|'hint'
	|'img'|'font'|'font_size'|'dropcap'|'opentype_features'|'lang'|'table'|'cell'|'ul'
	|'ol'|'lb'|'rb'|'color'|'bgcolor'|'fgcolor'|'outline_size'|'outline_color'
	|'wave'|'tornado'|'fade'|'rainbow'|'shake') -> popMode, pushMode(BBCode);
OPEN_PAREN : '(';
CLOSE_PAREN : ')';
EXP_WS: WS -> skip;
BOOL: ('true'|'false');
AWAIT: 'await';
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
HASH: '#';
NAME: NAME_START NAME_CONTINUE*;
UNDERSCORE: '_';
OPEN_BRACKET: '[';
CLOSE_BRACKET: ']' -> popMode;
EXP_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
EXP_ANY: ANY -> type(ANY);

fragment ESC: '\\' .;
fragment NAME_START: [A-Za-z];
fragment NAME_CONTINUE: NAME_START | UNDERSCORE | DIGIT;
fragment INT: DIGIT+;
fragment DIGIT: [0-9];
fragment SPACE: [ \t];
fragment WS: SPACE+;