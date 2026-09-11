

Undef-words

\ F83 word headers, using one byte as length and flags.

hex
\ class F83 header flags
080 Constant alias-mask
040 Constant immediate-mask
020 Constant restrict-mask
01f Constant lcount-mask
decimal

\ schema of a word

\ LFA	<link to next word / cell>
\ NFA	<length+flags / byte> n * <name chars> n * <padding>
\ CFA	<link to doer / cell>
\ BODY	<link to does part / cell> n <xt / cell>

: name>string ( nfa -- addr count )
  dup char+ swap c@ lcount-mask and ;

: name>xt ( nfa -- xt 1/flags ) 
\G Move from name to xt of a defintion, 
\G Returning flags or 1 
\G returning -1 if immediate and 1 if not
  dup c@ swap name>string + aligned swap ( xt flags )
  dup alias-mask and IF swap @ swap THEN
  lcount-mask invert and 1 or
  \ old version: immediate-mask and IF -1 ELSE 1 THEN 
  ;

: words
\G Print defined words. This is not in a standard wordset, however, its useful
\G for debugging the target
    forth-wordlist
    [ 1 has? hash-bits lshift ] literal 0 DO dup
        BEGIN @ dup WHILE dup cell+ name>string type space REPEAT drop 
        cell+ 
    LOOP drop ;

: upc ( c1 -- c2 )
\G Convert ASCII c1 to uppercase. Input values from ASCII a-z are converted to A-Z,
\G all other values are unchanged
\G Needed for dictionary search and number conversion
    dup [char] a [ char z 1+ ] literal within [ char a char A - ] literal and - ;

: lwc ( c1 -- c2 )
\G Convert ASCII c1 to uppercase. Input values from ASCII a-z are converted to A-Z,
\G all other values are unchanged
\G Needed for dictionary search and number conversion
    dup [char] A [ char Z 1+ ] literal within [ char a char A - ] literal and + ;

: tolower ( c-addr1 len -- c-addr2 len )
\ convert search string to lower case once and put it to here

    here (tolower) ;

: comparedict ( adr1 len1 adr2 len2 -- flag )
  rot over <> IF 2drop drop false EXIT THEN
  ( adr1 adr2 len )
  bounds ?DO dup c@ I c@ <> 
             IF drop UNLOOP false EXIT THEN 
  char+ LOOP
  drop true ;

bigendian 0= 1 cells 2 = and [IF]

\ optimised dictionary traversal for search. asuming:
\
\ - little endian
\ - 16 bit
\ - words in dictionary are lower case



: (compare) ( adr1 adr2 len -- flag )
  bounds ?DO dup c@ I c@ <> 
             IF drop UNLOOP false EXIT THEN 
  char+ LOOP
  drop true ;

\ This implementation keeps a 16 bit value on the return stack with the containing
\ the length and the first character. Cuts traversal time approximately in half.
: find-name-in ( adr len lfa -- nfa | 0 )
  -rot 
  tolower
  \ compute word with char and length
  over c@ 8<< over or >r 
  rot
  BEGIN @ dup WHILE cell+ dup @ $ff1f and r@ = IF
            >r 2dup r@ char+ swap (compare)
            IF 2drop r> rdrop EXIT THEN r>
        THEN cell -
  REPEAT rdrop nip nip ;

[ELSE]

\ Slow variant should work on any architecture

: comparedict ( adr1 len1 adr2 len2 -- flag )
  rot over <> IF 2drop drop false EXIT THEN
  ( adr1 adr2 len )
  bounds ?DO dup c@ upc I c@ upc <> 
             IF drop UNLOOP false EXIT THEN 
  char+ LOOP
  drop true ;

: find-name-in ( adr len dict -- nfa | 0 )
\G Forth 2012 suggestion, https://forth-standard.org/proposals/find-name#contribution-58
  BEGIN @ dup WHILE >r 2dup r@ cell+ name>string comparedict-ignore-case
        IF 2drop r> cell+ EXIT THEN r>
  REPEAT nip nip ;
[THEN]

[IFUNDEF] forth-wordlist
\G points to the last defined word
Variable forth-wordlist
[THEN]

: find-name ( c-addr u -- nt | 0 ) 
  forth-wordlist find-name-in ;

\ : sfind ( c-addr u -- 0 / xt +-1  ) \ GFORTH
\ \ used in the interpreter loop
\   find-name dup IF name>xt THEN ;

\ new standard word is find-name
\ : find ( c-addr -- xt +-1 | c-addr 0 )
\ \G Find the definition named in the counted string at c-addr. 6.1.1550 CORE
\   dup count sfind dup IF rot drop THEN ;

require simple-accept.fs

require catch-throw.fs

decimal
User base 10 base !

: decimal 10 base ! ;
: hex 16 base ! ;

User dpl

decimal

: digit   ( char base -- n true | char false )
\G Convert a single character to a number in the given base.
\G Compatibility F83, Open Boot (0xa3)
  >r upc
  dup dup [ char A 1- ] literal u>
  IF   [ char A 10 - ] literal ( S: c n R: base )
  ELSE \ chars between 9 and a (exclusive) are wrong
       dup [char] 9 u> or
       [char] 0
  THEN - dup r> u< IF nip true EXIT THEN drop false ;

0 [IF]
\ first version, taken from gforth and reworked
\ the eforth version is shorter
| : accumulate ( +d0 digit base - +d1 )
  >r swap  r@  um* drop rot  r>  um* d+ ;

: (>number) ( ud1 c-addr1 u1 base -- ud2 c-addr2 u2 )
  >r 0 
  ?DO    count J digit
  WHILE  swap J swap >r accumulate r>
  LOOP   0 rdrop EXIT
  THEN   drop 1- I' I - unloop rdrop ;
[ELSE]

: (>number) ( ud1 c-addr1 u1 base -- ud2 c-addr2 u2 )
  >r 
  BEGIN dup
  WHILE >r dup >r c@ J digit
  WHILE swap J um* drop rot J um* d+ r> char+ r> 1-
  REPEAT drop r> r> THEN r> drop ;

[THEN]

: >number ( ud1 c-addr1 u1 -- ud2 c-addr2 u2 )
\ convert to double number until bad char
\ Compatibility AnsForth (6.1.0570)
  base @ (>number) ;

\ : $number ( c-addr1 u1 -- u2 )
\ \G Convert a string to a number
\ \G Compatibility Open Boot
\   >r >r 0 0 r> r> >number 2drop drop ;

hex
const Create bases   10 ,   2 ,   A ,
\                     16     2    10
\ character literals 'c' are handled in snumber?

\ FIXME: !! protect BASE saving wrapper against exceptions
\ FIXME: change s>number? impl to save base internally
: getbase ( addr u -- addr' u' )
    over c@ [char] $ - dup 3 u<
    IF
        cells bases + @ base ! 1 /string
    ELSE
        drop
    THEN ;

: sign? ( addr u -- addr u flag )
    over c@ [char] - =  dup >r
    IF
        1 /string
    THEN
    r> ;

: s>unumber? ( addr u -- ud flag )
    base @ >r  dpl on  getbase
    0. 2swap
    BEGIN ( d addr len )
        dup >r >number dup
    WHILE \ there are characters left
        dup r> -
    WHILE \ the last >number parsed something
        dup 1- dpl ! over c@ [char] . =
    WHILE \ the current char is '.'
        1 /string
    REPEAT  THEN \ there are unparseable characters left
        2drop false
    ELSE
        rdrop 2drop true
    THEN
    r> base ! ;

: s>number? ( addr len -- d f )
\ converts string addr len into d, flag indicates success
    sign? >r s>unumber? IF  
        r> IF dnegate THEN true
    ELSE \ no characters left, all ok
        rdrop false
    THEN ;

: snumber? ( c-addr u -- 0 / n -1 / d 0> )
    \ Forth-2012 3.4.1.3 character literal <cnum> := '<char>'
    dup 3 = IF
        over count [char] ' = swap char+ c@ [char] ' = and
        IF drop char+ c@ true EXIT THEN
    THEN
    s>number? 0=
    IF
        2drop false  EXIT
    THEN
    dpl @ dup 0< IF
        nip
    ELSE
        1+
    THEN ;

: notfound  ( addr u -- )
    ." ?! " type space -&13 throw ;

: compile-only ( addr u -- )
    ." compile only: " type space -&14 throw ;

: interpreter ( c-addr u -- ) 
    2dup find-name
    ?dup IF   
        name>xt restrict-mask and IF
            drop compile-only
        ELSE
            nip nip execute
        THEN
    ELSE
        2dup 2>r
        snumber? ( n -1 | d >0)
        \ snumber? has variable stack effect
	    IF
	        2rdrop
	    ELSE
	        2r> notfound
	    THEN
    THEN ;

: ?stack ( ?? -- ?? ) \ gforth
    [ e? stack-grows-upwards [IF] ]
        sp@ sp0 @ u< IF sp0 @ sp! -4 throw  THEN
        \ overflow: both stacks share one region and the next word may take
        \ up to 52 bytes (measured: number conversion, .s, 2constant) plus 9
        \ for an interrupt frame and pushes inside primitives
        stack-space &62 < IF sp0 @ sp! -3 throw  THEN
    [ [ELSE] ]
        sp@ sp0 @ u> IF sp0 @ sp! -4 throw  THEN
    [ [THEN] ]
    [ has? floating [IF] ]
        fp@ fp0 @ u> IF  -&45 throw  THEN
    [ [THEN] ] ; 

require tib.fs
require parse-word.fs

: interpret ( ?? -- ?? ) \ gforth
\ interpret/compile the (rest of the) input buffer
    BEGIN ?stack parse-word dup 
    WHILE  
        [ defined? state [IF] ] 
                    state @ IF compiler ELSE interpreter THEN
        [ [ELSE] ]  
                    interpreter
        [ [THEN] ]
    REPEAT 2drop ;  

\ Allow quit-interpret to be redefined with other useful things
[IFUNDEF] quit-interpret

: .elapsed ( u -- ) 
    '~ emit 10 u.x ." ms " ;

: .eaten ( u -- ) 
    ?dup IF '% emit '- emit 10 u.x 'b emit unused 10 u.x 'b emit space THEN ;

| Variable burst-here \ HERE when the current input burst started, 0 when idle
| Variable burst-ms   \ millis when the current input burst started

: quit-interpret
\G Decorated interpret when called within quit. Adds timing display.
\G While more input is already waiting the display is held back, so a
\G pasted block reports its totals once, after its last line.
    burst-here @ 0= IF here burst-here ! millis burst-ms ! THEN
    interpret key? IF EXIT THEN
    here burst-here @ - .eaten millis burst-ms @ - .elapsed burst-here off ;

[THEN]

: refill ( -- flag ) \ core-ext,block-ext,file-ext
\G refill the input buffer
    tib dup >tib ! /line accept #tib ! 0 >in ! true ;

: quit-error ( ... n -- ... )
\ target for throw if no catch handler is defined
\ we don't use catch in quit, so the stack contents keep
\ intact when user has a typo ;jw
      .error quit ;

: quit ( -- ) \ CORE
\G Empty the return stack, make the user input device
\G the input source, enter interpret state and start
\G the text interpreter.
    \ don't reset sp. user might have a typo
    \ [ unlock data-stack borders nip lock ] literal sp!
    \ rp by convention points
    [ unlock return-stack borders nip lock cell - ] literal rp!
    handler off [ defined? state [IF] ] state off [ [THEN] ]
    \ a header that was never revealed means an error aborted the
    \ definition, give its header and code back
    last @ ?dup IF dp ! last off THEN
    [ defined? burst-here [IF] ] burst-here off [ [THEN] ]
    \ exits only through THROW etc.
    BEGIN
        [ defined? state  [IF] ] 
                    state @ IF ."  compiled" ELSE ."  ok" THEN cr
        [ [ELSE] ]  \ interpreter only version
                    ."  ok" cr
        [ [THEN] ]
        refill drop 
        quit-interpret
    AGAIN ;

: evaluate ( c-addr u -- ) \ core,block
\G Save the current input source specification. Set @code{>IN} to
\G @code{0} and make the string @i{c-addr u} the input source
\G and input buffer. Interpret. When the parse area is empty,
\G restore the input source specification.
    >tib @ >r #tib @ >r >in @ >r
    #tib ! >tib ! >in off
    ['] interpret catch
    r> >in ! r> #tib ! r> >tib !
    throw ;

: char   ( '<spaces>ccc' -- c ) \ core
\G Skip leading spaces. Parse the string @i{ccc} and return @i{c}, the
\G display code representing the first character of @i{ccc}.
    parse-word IF c@ ELSE drop -&13 throw THEN ;

: name?int ( nt -- xt ) \ gforth
\G Like @code{name>int}, but perform @code{-14 throw} if @i{nt}
\G has no interpretation semantics.
    name>xt restrict-mask and IF compile-only-error ( does not return ) THEN ;

: (') ( "name" -- nt ) \ gforth
    parse-word name-too-short? find-name dup 0= IF drop -&13 throw THEN  ;

: '    ( "name" -- xt ) \ core	tick
    (') name?int ;

All-words
