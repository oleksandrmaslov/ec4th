\ catch and throw

decimal

\ sp! and rp! fillers                          8mar26jw

\ FIXME: move to primitives?

[IFUNDEF] sp!

e? stack-grows-upwards [IF]

: sp! ( addr -- )
\G Reset stack pointer to the given address.
  cell+ sp@ swap - 
  cell/ dup 0> IF 
    0 DO drop LOOP
  ELSE
    \ stack underflow
    negate 0 ?DO false LOOP
  THEN ;

[ELSE]

: sp! ( addr -- )
\G Reset stack pointer to the given address.
  sp@ cell+ - 
  cell/ dup 0> IF 
    0 DO drop LOOP
  ELSE
    \ happens on an underflow
    \ however, we should keep the contents intact via sp@
    negate 0 ?DO sp@ 1 cells - @ LOOP
  THEN ;

[THEN]

[THEN]

[IFUNDEF] rp!
: rp! ( addr -- )
\G Reset return stack, expects that its always reduced
  r> swap BEGIN dup rp@ u> WHILE rdrop REPEAT drop >r ;
[THEN]

User "error

: .error ( throw-code -- )
\G Display exception information to the user. The default implementation
\G just writes the code or the counted string from "error
\G https://forth-standard.org/standard/exception
\G Courtesy to Heinz Schnitter we print KO for an error
\G -1 (ABORT) prints nothing, see 9.6.1.2275 THROW
  dup -2 = 
  IF 	  "error @ ?dup IF count type THEN drop
  ELSE	dup 1+ IF ." KO#" &10 ( n base ) .x ELSE drop THEN
  THEN ;

\ straight forward implementation also described in the standard"
\ https://forth-standard.org/standard/exception/THROW
\ https://forth-standard.org/standard/exception/CATCH

User handler

: catch ( ct -- .... 0 / error )
  sp@ >r handler @ >r
  rp@ handler ! execute
  r> handler ! rdrop 0 ;

: throw
  ?dup IF handler @ 
    dup 0= IF drop .error quit THEN
    rp! r> handler !
    r> swap >r sp! drop r>
  THEN ;

: abort ( ?? -- ?? ) \ core,exception-ext
    -1 throw ;

: name-too-short? ( c-addr u -- c-addr u )
    dup 0= -&16 and throw ;

: name-too-long? ( c-addr u -- c-addr u )
    dup lcount-mask u> -&19 and throw ;

: compile-only-error ( ... -- )
    -&14 throw ;

