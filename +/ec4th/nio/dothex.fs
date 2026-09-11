\ dothex.fs simple number output for hex radix 02oct01jaw

\ This is a number output package which should be sufficient
\ for most embedded interactive debugging sessions.
\ 
\ Advantage over number IO with <# #s #>:
\
\ - Does not need additional memory
\ - Only uses bit shifting and does not need division

DECIMAL

[IFUNDEF] todigit
 : todigit ( u -- c )
  9 over < 7 and + [char] 0 + ;
[THEN]

: .hdigit ( n -- )
\G Print a hex digit (10 becomes A, and so on)
  $0f and todigit emit ;

\ : ERROR" [char] " parse 
\  rot 
\  IF cr ." *** " type ."  ***" -1 ABORT" CROSS: Target error, see text above" 
\  ELSE 2drop 
\  THEN ;

: .hstep ( n f -- n f )
\G Basic idea is print the highest digit and
\G shift left one digit (4 bits)
\G Since we want no leading 0 digits, the flag
\G indicates whether we started printing something
\G (and therefore print 0s also)
  over
  \ shift right 16, 32, 64 - 4 bits
  [ s" ADDRESS-UNIT-BITS" environment? 0= 
    ERROR" ADDRESS-UNIT-BITS undefined"
    cells
    4 - 
  ] Literal rshift
  swap over 0<> or 
  IF   .hdigit true
  ELSE drop false
  THEN 
  swap 4 lshift swap ;

: (.h) ( n f -- )  
\G Prints a hex number. If f is true we want leading 0s.
  [ cell 2* ] Literal
  0 DO .hstep LOOP
  0= IF .hdigit EXIT THEN drop ;

: .addr ( n -- ) 
\G Prints an address in hex representation
  true (.h) space ;

: .h ( n -- ) 
\G Prints a hex numer
  false (.h) space ;

: .byte ( n -- )
\G Prints a hex byte in fixed two digit format with a trailing space.
  dup 4 rshift .hdigit .hdigit space ;

: .$ ( n -- ) 
\G Prints a hex number with a leading $ sign and a trailing space.
  [char] $ emit .h ;

: .sh
  depth
  dup [char] < emit false (.h) [char] > emit space dup
  0 ?DO dup pick .h 1- LOOP drop ;

\ : n.x ( n -- ) 0 do .x loop ;