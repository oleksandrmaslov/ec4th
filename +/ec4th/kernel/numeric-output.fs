\ Basic numeric output                                          15mar26jaw

\ Recursive numeric output implementation for arbitrary base
\ For .error we should be able to output the exception number
\ although there is a dictionary overflow. 

[IFUNDEF] todigit
 : todigit ( u -- c ) 9 over < 7 and + [char] 0 + ;
[THEN]

| : u.1x ( u base -- )
    tuck u/mod ?dup IF rot RECURSE ELSE nip THEN todigit emit ;

: u.x ( u base -- ) \ ec4th
\G Output unsigned number with arbirary base. Does not add a trailing space.
\G Only uses stack for the output and does not rely on available dictionary space
\G as pictured numeric output would do.
    over 0= IF 2drop '0 emit ELSE u.1x THEN ;

: .x ( n base -- ) \ ec4th
\G Output signed number with arbirary base
\G Only uses stack for the output and does not rely on available dictionary space
\G as pictured numeric output would do.
    over 0< IF '- emit swap negate swap THEN u.x space ;

: u. ( u -- )
\G Only uses stack for the output and does not rely on available dictionary space
\G as pictured numeric output would do.
    base @ u.x space ;

: . ( n -- )
\G Only uses stack for the output and does not rely on available dictionary space
\G as pictured numeric output would do.
    base @ .x ;

: .s
\G print the content of the stack
  depth
  dup [char] < emit base @ u.x [char] > emit space dup
  0 ?DO dup pick . 1- LOOP drop ;
