lexer grammar DialogLexer;
@lexer::header {#pragma warning disable 3021}
tokens { INDENT, DEDENT }

channels { WHITESPACE, COMMENTS }

options { superClass=DialogLexerBase; }

WS: SPACE+ -> channel(HIDDEN);
COMMENT: '//' ~[\r\n]* -> skip;
MULTILINE_COMMENT: '/*' .*? '*/' ~[\r\n]* -> skip;
NEWLINE: ('\r'?'\n'|'\r');

TITLE_ENTER: '--' -> pushMode(Title);
CHOICE: '?' ' '+ -> pushMode(Choice);
IF: 'if' -> pushMode(Expression);
ELSEIF: 'else if' -> pushMode(Expression);
ELSE: 'else';
TAG_ENTER: OPEN_BRACKET -> type(OPEN_BRACKET), pushMode(Expression);
SPEAKER_NAME: (NAME | UNDERSCORE) -> type(NAME), pushMode(Speaker);
ANY: . -> channel(HIDDEN);

mode Title;
TITLE_NAME: NAME -> type(NAME);
TITLE_EXIT: '--' -> popMode;
TITLE_ANY: ANY -> type(ANY);

mode Speaker;
NAME_SEPARATOR: ',' WS;
EXTRA_NAME: NAME -> type(NAME);
ML_ENTER: ':' SPACE? '^^' -> popMode, pushMode(MultiLineFirst);
LINE_ENTER: ':' SPACE? -> popMode, pushMode(Line);
SPEAKER_ANY: ANY -> type(ANY);

mode Line;
LINE_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(Expression);
TEXT: (ESC | ~[\r\n[] | ('/' ~[\r\n/]) )+;
LINE_COMMENT: COMMENT -> skip;
TEXT_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
TEXT_ANY: ANY -> type(ANY);

mode MultiLineFirst;
ML_FIRST_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(Expression);
ML_FIRST_EXIT: '^^' -> popMode;
ML_FIRST_TEXT: (ESC | ~[\r\n[^] | ('/' ~[\r\n/^]) )+ -> type(TEXT);
ML_FIRST_COMMENT: COMMENT -> skip;
ML_FIRST_NEWLINE: NEWLINE -> type(NEWLINE), popMode, pushMode(MultiLineExtra);
ML_FIRST_ANY: ANY -> type(ANY);

mode MultiLineExtra;
ML_EXTRA_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(Expression);
ML_EXTRA_EXIT: '^^' -> popMode;
ML_EXTRA_TEXT: (ESC | ~[ \t\r\n[^] | ('/' ~[\r\n/^]) | ('^' ~[\r\n^]))+ -> type(TEXT);
ML_EXTRA_COMMENT: COMMENT -> skip;
ML_EXTRA_NEWLINE: NEWLINE -> type(NEWLINE);
ML_EXTRA_WS: WS -> type(WS), channel(HIDDEN);
ML_EXTRA_ANY: ANY -> type(ANY);

mode Choice;
CHOICE_TAG_ENTER: OPEN_BRACKET (WS* '/')? -> type(OPEN_BRACKET), pushMode(Expression);
CHOICE_TEXT: (ESC | ~[\r\n/[] | ('/' ~[\r\n/[]) )+ -> type(TEXT);
CHOICE_COMMENT: COMMENT -> skip;
CHOICE_NEWLINE: NEWLINE -> type(NEWLINE), popMode;
CHOICE_ANY: ANY -> type(ANY);

mode BBCode;
BBCODE_EXTRA_TEXT: ~']'+;
BBCODE_TAG_EXIT: ']' -> type(CLOSE_BRACKET), popMode;
BBCODE_ANY: ANY -> type(ANY);

mode Expression;
BBCODE_NAME: ('b'|'i'|'u'|'s'|'code'|'char'|'p'|'center'|'right'|'left'|'fill'|'indent'|'url'|'hint'
	|'img'|'font'|'font_size'|'dropcap'|'opentype_features'|'lang'|'table'|'cell'|'ul'
	|'ol'|'lb'|'rb'|'color'|'bgcolor'|'fgcolor'|'outline_size'|'outline_color'
	|'wave'|'tornado'|'fade'|'rainbow'|'shake') -> popMode, pushMode(BBCode);
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