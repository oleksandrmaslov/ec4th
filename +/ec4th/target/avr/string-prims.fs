\ string-prims.fs  string primitives for the interpreter and compiler   ec4th

\ (tolower) copies a name for the dictionary search, scan finds the end of
\ comments and strings. Both accept strings in RAM and in ROM.

decimal

start-macros
\ convert a masked program memory address in Z back to a forth ROM address
: zh-pm-forth,
  rom-start IF ZH rom-start 8 rshift ori, THEN ;
end-macros

| Code (tolower) ( c-addr1 u c-addr2 -- c-addr2 u )
\G Copy the string c-addr1 u to the RAM address c-addr2, A-Z become a-z
    IPH push, IPL push,       \ X becomes the destination pointer
    XL tosl movw,
    loadtos                   \ tos = u, which is also the result
    ZH ld-Y, ZL ld-Y,         \ Z = c-addr1, Y points to its stack cell
    XL stY+, XH stY+,         \ which now gets c-addr2
    WL tosl movw,             \ W counts down
    WL zero cp,
    WH zero cpc,
    3 $ breq,
    ?rom-address-cp,
    7 $ brcs,
1 $:                          \ RAM source
    temp0 ldZ+,
    temp1 temp0 mov,
    temp1 65 subi,            \ 'A'
    temp1 26 cpi,
    2 $ brcc,
    temp0 $20 ori,
2 $:
    temp0 stX+,
    WL 1 subi,
    WH zero sbc,
    1 $ brne,
3 $:
    IPL pop, IPH pop,
    do_next rjmp,
7 $:                          \ ROM source
    zh-mask-rom-address,
8 $:
    temp0 lpmZ+,
    temp1 temp0 mov,
    temp1 65 subi,
    temp1 26 cpi,
    9 $ brcc,
    temp0 $20 ori,
9 $:
    temp0 stX+,
    WL 1 subi,
    WH zero sbc,
    8 $ brne,
    3 $ rjmp,
End-Code+

Code scan ( addr1 n1 char -- addr2 n2 )
\G Skip all characters not equal to char. addr2 is the first char equal
\G to char, or addr1+n1 with n2 = 0 if there is none
\G Compatibility GForth, F83
    temp2 tosl movw,          \ temp2:temp3 = char
    loadtos                   \ tos = n1, counts down
    ZH ld-Y, ZL ld-Y,         \ Z = addr1, Y points to its stack cell
    tosl zero cp,
    tosh zero cpc,
    4 $ breq,                 \ empty string: addr1 0
    temp3 zero cp,
    5 $ brne,                 \ a char above 255 never matches
    ?rom-address-cp,
    7 $ brcs,
1 $:                          \ RAM
    temp0 ldZ+,
    temp0 temp2 cp,
    2 $ breq,
    tosl 1 sbiw,
    1 $ brne,
4 $:                          \ Z = addr2, tos = n2
    ZL stY+, ZH stY+,
    do_next rjmp,
2 $:                          \ found, back to the char
    ZL 1 sbiw,
    4 $ rjmp,
5 $:
    ZL tosl add,
    ZH tosh adc,
    tosl clr,
    tosh clr,
    4 $ rjmp,
7 $:                          \ ROM
    zh-mask-rom-address,
8 $:
    temp0 lpmZ+,
    temp0 temp2 cp,
    9 $ breq,
    tosl 1 sbiw,
    8 $ brne,
    zh-pm-forth,
    4 $ rjmp,
9 $:
    ZL 1 sbiw,
    zh-pm-forth,
    4 $ rjmp,
End-Code+
