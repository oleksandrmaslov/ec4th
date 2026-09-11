\ 

\ Stripped down compiler extracted from GForth around 2008.

 Variable state ( -- a-addr ) \ core,tools-ext
 \G @code{User} variable -- @i{a-addr} is the address of a cell
 \G containing the compilation state flag. 0 => interpreting, -1 =>
 \G compiling. 
 0 state !

\ dp here allot unused c, , align are in dictionary.fs

Variable current
Variable last    \ link field of the header under construction, 0 after reveal
Variable lastnt  \ name token of the most recently revealed header
Variable lastcfa

\G GForth EC / ec4th switch that the next defining word data goes to ROM
' noop ALIAS const

: string, ( c-addr u -- ) \ gforth
\G puts down string as cstring, the resulting DP may be not aligned
    dup c, here swap chars dup allot move ;

| : name, ( c-addr u -- ) \ gforth
\G puts down string as cstring, converting to lowercase the resulting DP may be not aligned
    dup c, bounds DO I c@ lwc c, LOOP ;

\ https://forth-standard.org/standard/core/COMPILEComma
' , Alias compile,

\ \ literals							17dec92py

: Literal  ( compilation n -- ; run-time -- n ) \ core
\G Compilation semantics: compile the run-time semantics.@*
\G Run-time Semantics: push @i{n}.@*
\G Interpretation semantics: undefined.
[ [IFDEF] lit, ]
    lit,
[ [ELSE] ]
    \ did not work: 
    postpone lit ,
    \ ['] lit , ,
[ [THEN] ] ; immediate restrict

: [char] ( compilation '<spaces>ccc' -- ; run-time -- c ) \ core bracket-char
\G Compilation: skip leading spaces. Parse the string
\G @i{ccc}. Run-time: return @i{c}, the display code
\G representing the first character of @i{ccc}.  Interpretation
\G semantics for this word are undefined.
    char postpone Literal ; immediate restrict

: [']  ( compilation. "name" -- ; run-time. -- xt ) \ core      bracket-tick
\g @i{xt} represents @i{name}'s interpretation
\g semantics. Perform @code{-14 throw} if the word has no
\g interpretation semantics.
    ' postpone Literal ; immediate restrict

: postpone ( "name" -- ) \ core
\g Compiles the compilation semantics of @i{name}: an immediate word is
\g compiled, for any other word code is compiled that compiles it.
    (') name>xt immediate-mask and
    IF compile, ELSE postpone Literal ['] compile, compile, THEN ; immediate restrict

\ \ compiler loop

: compiler ( c-addr u -- )
    2dup find-name dup
    IF  ( c-addr u nt )
	    nip nip name>xt
        immediate-mask and IF execute ELSE compile, THEN
    ELSE
        drop
        2dup snumber? dup
        IF
            0> IF swap postpone Literal THEN
            postpone Literal
            2drop
        ELSE
            drop notfound
        THEN
    THEN ;

\ GForth also uses the defined word parser and prompt,
\ we use only state here for simplicity ;jw

: [ ( -- ) \  core	left-bracket
\G Enter interpretation state. Immediate word.
    state off ; immediate

: ] ( -- ) \ core	right-bracket
\G Enter compilation state.
    state on ;

\ \ Strings							22feb93py 10mar27jaw

\ scan and parse is needed here by the compiler, so keep the definition
\ here, although it is related to the parsing

[IFUNDEF] scan
: scan   ( addr1 n1 char -- addr2 n2 )
\G Skip all characters not equal to char
\G Compatibility GForth, F83
   >r 
   BEGIN dup WHILE over c@ r@ <> WHILE 1 /string
   REPEAT THEN rdrop ;
[THEN]

: parse ( delim -- c-addr len )
\ Return the next string from the input buffer delimited by delim.
\ do not skip white space, since string may have leading spaces, e.g. ."  hello"
\ Compatibity Open Boot
  >r source >in @ /string over -rot r> scan 
  dup IF 1- THEN source nip swap - >in !  over -
\  [ \ this operation only works if 1 chars == 1, so warn
\    1 chars 1 <> ERROR" Addressunit must be one char"
\  ]
   ;

: ," ( "string"<"> -- ) 
    [char] " parse string, align ;

: SLiteral ( Compilation c-addr1 u ; run-time -- c-addr2 u ) \ string
\G Compilation: compile the string specified by @i{c-addr1},
\G @i{u} into the current definition. Run-time: return
\G @i{c-addr2 u} describing the address and length of the
\G string.
    postpone (S") string, ; immediate restrict

: \ ( compilation 'ccc<newline>' -- ; run-time -- ) \ thisone- core-ext,block-ext backslash
    source >in ! drop ; immediate

: ( ( compilation 'ccc<close-paren>' -- ; run-time -- ) \ thisone- core,file	paren
    [char] ) parse 2drop ; immediate

\ \ abort"							22feb93py

: (abort")
    "lit >r IF r> "error ! -2 throw THEN rdrop ;

: ." postpone (.") ," ; immediate restrict

: s" ( compilation "ccc<quote>" -- ; run-time -- c-addr u )  \ core s-quote 
\G Compiles a string literal and returns it at runtime
\G The standard does not define interpretation semantics. ec4th returns parses
\G the string and returns it at interpreation time. The string is directly
\G from the input buffer and may be overwritten with the next line.
\G GForth copies the string to a temporary area, however, in EC targets
\G we will not reserve extra memory.
    state @ IF postpone (s") ," ELSE [char] " parse THEN ; immediate

: abort" ( compilation 'ccc"' -- ; run-time f -- ) \ core,exception-ext	abort-quote
\G If any bit of @i{f} is non-zero, perform the function of @code{-2 throw},
\G displaying the string @i{ccc} if there is no exception frame on the
\G exception stack.
    postpone (abort") ," ;        immediate restrict

: recurse ( compilation -- ; run-time ?? -- ?? ) \ core
    \g Call the current definition.
    lastcfa @ , ; immediate restrict

\ \ Header states						23feb93py

: cset ( bmask c-addr -- )
    tuck c@ or swap c! ; 

: creset ( bmask c-addr -- )
    tuck c@ swap invert and swap c! ; 

: ctoggle ( bmask c-addr -- )
    tuck c@ xor swap c! ; 

: ?last-cset ( bmask -- )
\G Set bmask in the count byte of the header under construction, or of
\G the most recently revealed one, so : foo ; immediate works
  last @ ?dup IF cell+ ELSE lastnt @ THEN
  ?dup IF cset ELSE drop THEN ;

: immediate ( -- ) \ core
    \G Make the compilation semantics of a word be to @code{execute}
    \G the execution semantics.
    immediate-mask ?last-cset ;

: restrict ( -- ) \ gforth
    \G A synonym for @code{compile-only}
    restrict-mask ?last-cset ;

| : current-bucket ( nfa -- wid-bucket )
\ find hash bucket for a newly created word
    [ [IFDEF] hash-bucket ] 
        name>string hash-bucket cells current @ +
    [ [ELSE] ]
        drop current @
    [ [THEN] ] ;

: header ( "name" -- )
    parse-word name-too-short? name-too-long?
    \ header, code field and two body cells must fit, so Create, Variable,
    \ Constant and Alias can not run out of space after the word is revealed:
    \ align 1, link 2, count 1, name, align 1, cfa 4, body 4
    dup 13 + unused u> -8 and throw
    \ on 8 bit systems there is no real need for alignment
    \ however, its good practise to have it cell aligned, sinc
    \ otherwise dumps are very confusing ;jw
    align here last ! 0 , \ link field
    \ place string, patch link field with the link in the bucket
    here >r name, r> current-bucket @ last @ ! ;

\ Create Variable User Constant                        	17mar93py

: Alias    ( xt "name" -- ) \ gforth
    \ name>xt expects the xt in the cell aligned after the name
    Header align , alias-mask ?last-cset reveal ;

0 Constant defstart

\ defined in flow-control.fs
\ : ?struc ( colon-sys -- )
\ \G expects 0 to check for imablanced conditionals
\     abort" unstructured " ;

: cfa,
    align here lastcfa ! ,  0 , ;

: colon-cf, ( -- colon-sys )
    \ common factor of : and :noname
     lit :docol cfa, defstart ] ;

: : ( "name" -- colon-sys ) \ core	colon
    Header colon-cf, ;

: :noname ( -- xt colon-sys ) \ core-ext	colon-no-name
    0 last ! colon-cf, lastcfa @ swap ;

: check-shadow  ( addr count wid -- )
\G prints a warning if the string is already present in the wordlist
    >r 2dup 2dup r> find-name-in  ?dup if
    	." redefined " name>string 2dup type
	    comparedict 0<> if
	        ."  with " type
	    else
	        2drop
	    then
	    space space EXIT
    then
    2drop 2drop ;

: reveal ( -- ) \ gforth
    last @ ?dup
    IF \ the last word has a header
        dup cell+ dup lastnt ! dup name>string current @ check-shadow
        current-bucket ! 
        last off
    THEN ;

: ; ?struc reveal postpone ;s postpone [ ; immediate restrict

\ field support needs to go somewhere else

\ doer? :dofield [IF]
\     : (Field)  Header reveal dofield: cfa, ;
\ [ELSE]
\     : (Field)  Create DOES> @ + ;
\ [THEN]

\ \ Create Variable User Constant                        	17mar93py

doer? :dovar [IF]

: Create ( "name" -- ) \ core
    Header reveal lit :dovar cfa, ;
[ELSE]

: Create ( "name" -- ) \ core
    Header reveal 0 cfa, DOES> ;
[THEN]

: Variable ( "name" -- ) \ core
    Create 0 , ;

: 2Variable ( "name" -- ) \ double two-variable
    create 0 , 0 , ;

doer? :docon [IF]
    : Constant  Header reveal lit :docon cfa, , ;
[ELSE]
    : Constant  Create , DOES> @ ;
[THEN]

\ FIXME: cross.fs compiles doesjump at the start of does> section, remove or keep it for see?
: :doesjump ;

: (does>2)
\ patch CFA to point to DOES> part
    2 cells + \ FIXME: skip doesjump: remove it
    lastcfa @ lit :dodoes over ! cell+ ! ;

\ FIXME: value / defer

\ TODO move to double?

: 2,	( w1 w2 -- ) \ gforth
\G Reserve data space for two cells and store the double @i{w1
\G w2} there, @i{w2} first (lower address).
    here 2 cells allot 2! ;

: 2Constant ( w1 w2 "name" -- ) \ double two-constant
    Create ( w1 w2 "name" -- ) 2,
    DOES> 2@ ( -- w1 w2 ) ;

: DOES> ( compilation colon-sys -- colon-sys ; run-time nest-sys -- ) \ core does
\G Compiles the same layout as the cross compiler's DOES>, so (does>2)
\G and :dodoes serve both: lit <handler> (does>2) ;s :doesjump 0 <does-code>
    dup ?struc \ colon-sys of : is 0, anything else is an open IF/BEGIN/DO
    here [ 4 cells ] literal + postpone Literal
    postpone (does>2) postpone ;s
    ['] :doesjump , 0 , ; immediate restrict
