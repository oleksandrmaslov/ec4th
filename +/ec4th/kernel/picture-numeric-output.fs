\ Pictured numeric output

UNDEF-WORDS
decimal

\ /hold /pad and dictionary-end-address are defined in dictionary.fs

Variable hld

: pad ( -- c-addr )
\G @var{c-addr} is the address of a transient region that can be
\G used as temporary data storage. At least 84 characters of space
\G is available. The actual space can be determined by /pad
\G allot keeps /hold and /pad free above here, so no check is needed
    here /hold + ;

\ : todigit ( u -- c )
\    9 over < 7 and + [char] 0 + ;

: <# ( -- )
\G Initialise/clear the pictured numeric output string.
  pad hld ! ;

: hold ( c -- )
\G Used within @code{<#} and @code{#>}. Append the character
\G @var{char} to the pictured numeric output string.
    hld @ char- dup here u< -&17 and throw tuck c! hld ! ;

: # ( d -- d )
\G Used within @code{<#} and @code{#>}. Add the next
\G least-significant digit to the pictured numeric output
\G string. This is achieved by dividing @var{ud1} by the number in
\G @code{base} to leave quotient @var{ud2} and remainder @var{n};
\G @var{n} is converted to the appropriate display code (eg ASCII
\G code) and appended to the string. If the number has been fully
\G converted, @var{ud1} will be 0 and @code{#} will append a ``0''
\G to the string.
  base @ ud/mod rot todigit hold ;

: #s ( d -- d )
\G Used within @code{<#} and @code{#>}. Convert all remaining digits
\G using the same algorithm as for @code{#}. @code{#s} will convert
\G at least one digit. Therefore, if @var{ud} is 0, @code{#s} will append
\G a ``0'' to the pictured numeric output string.
   BEGIN # 2dup or 0= UNTIL ;

: #> ( d -- a u )
\G Complete the pictured numeric output string by discarding
\G @var{xd} and returning @var{addr u}; the address and length of
\G the formatted string. A Standard program may modify characters
\G within the string.
   2drop hld @ pad over - ;

: sign ( n -- )
    0< IF [char] - hold THEN ;

: d. ( d -- )
    dup -rot dabs <# #s rot sign #> type space ;

: . ( n -- )
    dup abs s>d <# #s rot sign #> type space ;

: u. ( u -- )
    s>d <# #s #> type space ;

ALL-WORDS
