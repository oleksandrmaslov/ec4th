\ dictionary.fs  data space: here allot unused c, , align          ec4th

\ Owns the bounds of the RAM dictionary. HERE may grow up to
\ dictionary-end-address - /pad - /hold. Above HERE are the transient areas:
\ the pictured numeric output buffer (/hold, growing down from PAD), PAD
\ itself (/pad) and the lower case copy of a name that find-name-in writes
\ to HERE. ALLOT and UNUSED share that limit, so UNUSED never goes negative
\ and the transient areas never run into the input buffer behind the
\ dictionary.

decimal

\ size of the pictured numeric output string buffer, in characters
\ https://forth-standard.org/standard/usage#usage:env
32 Constant /hold

\ size of the scratch area pointed to by PAD, in characters,
\ according to standard at least 84
\ https://forth-standard.org/standard/usage#usage:env
84 Constant /pad

unlock ram-dictionary borders nip lock
Constant dictionary-end-address

\ the pointer to the current dictionary pointer
Variable dp

: here ( -- addr ) \ core
    dp @ ;

: unused ( -- u ) \ core-ext
\G Return the amount of free space remaining (in address units) in
\G the region addressed by @code{here}.
\ https://forth-standard.org/standard/core/UNUSED
    [ dictionary-end-address /pad - /hold - ] literal here - ;

\ check dictionary region 10mar26jaw
: allot ( n -- ) \ core
\G Reserve @i{n} address units of data space without
\G initialization. @i{n} is a signed number, passing a negative
\G @i{n} releases memory. Throws -8 if HERE would leave the dictionary
\G region or run into the /hold and /pad areas.
    here +
    \ inverted WITHIN: true if here'-1 is outside [start, limit)
    dup 1- [ dictionary-end-address /pad - /hold - ] literal
           [ unlock ram-dictionary borders drop lock ] literal
    within -8 and throw
    dp ! ;

: c,    ( c -- ) \ core c-comma
\G Reserve data space for one char and store @i{c} in the space.
    here 1 chars allot c! ;

: ,     ( w -- ) \ core comma
\G Reserve data space for one cell and store @i{w} in the space.
    here cell allot ! ;

\ aligned is defined in primitives/memory.fs

: align ( -- ) \ core
\G If the data-space pointer is not aligned, reserve enough space to align it.
    here dup aligned swap ?DO  bl c,  LOOP ;
