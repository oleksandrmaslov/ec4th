require +/gforth/asm/basic.fs
also assembler definitions
decimal
include asm-conf.fs

[IFUNDEF] X : x ; [THEN]

\ ~**************************useful non-assembler commands******************************~

\ saves one byte in target space
: cdeb, ( n -- )
  X c, ;

\ saves a word (two byte) in target space
: 2cdeb, ( n n -- )
  cdeb, cdeb, ;

\ splits 16-bit opcode to two 8 bit parts and saves it into target space
: opc, ( n -- )
  dup 255 and swap 8 rshift swap 2cdeb, ;

\ checks if k is between n1 and n2, and leaves a true flag if it's between them
: check-operand ( k n1 n2 -- f )
	\ k have to be between n1 and n2
  2 pick < -rot < OR ( abort" Wrong operand value" following commands should handle their error message on their own way ) ;

\ checks if k is between zero and n1 through check-operand
: 0check-operand ( k n1 -- f )
	0 swap check-operand ;

\ checks if register r is between 0 and 31, aborts if not
: check-register ( r -- )
  0 31 2 pick < -rot < OR abort" Wrong register value" ;

\ checks if register r is between n1 and n2, aborts if not
: check-register-special ( r n1 n2 -- )
  2 pick < -rot < OR abort" Wrong register value" ;

\ help-word that returns the highbyte of 16 bit integer n
: highbyte ( S: n -- b ; R: -- )
  8 rshift 255 and ;

\ help-word that returns the lowbyte of 16 bit integer n
: lowbyte ( S: n -- b ; R: -- )
  255 and ;

\ ~**********???? ??RD DDDD RRRR**************~

: opcode-pattern-??????RDDDDDRRRR ( opc -- )
  create , does> @ ( d r opc -- )
  2 pick check-register
  over check-register
  rot 4 lshift or
  over 16 and 5 lshift or swap
  15 and or
  opc, ;
' opcode-pattern-??????RDDDDDRRRR to op-xt

all-models
	%0001110000000000 op: adc,
	%0000110000000000 op: add,
	%0010000000000000 op: and,
	%0001010000000000 op: cp,
	%0001000000000000 op: cpse,
	%0000010000000000 op: cpc,
	%0010010000000000 op: eor,
	%0010110000000000 op: mov,
	%0010100000000000 op: or,
	%0000100000000000 op: sbc,
	%0001100000000000 op: sub,
flag-mul to test-flag
	%1001110000000000 op: mul,

\ ~**********???? ???D DDDD ????**************~

: opcode-pattern-???????DDDDD???? ( opc1 opc2 -- )
  create , does> @ ( d opc -- )
  over check-register
  swap 4 lshift or
  opc, ;
' opcode-pattern-???????DDDDD???? to op-xt

all-models
	%1001010000000101 op: asr,
	%1001010000000000 op: com,
	%1001010000001010 op: dec,
	%1001010000000011 op: inc,
	%1001000000001100 op: ldx,
	%1001000000001101 op: ldx+,
	%1001000000001110 op: ld-x,
	%1000000000001000 op: ldy,
	%1001000000001001 op: ldy+,
	%1001000000001010 op: ld-y,
	%1000000000000000 op: ldz,
	%1001000000000001 op: ldz+,
	%1001000000000010 op: ld-z,
	%1001000000000100 op: lpmz,
	%1001000000000101 op: lpmz+,
	%1001010000000110 op: lsr,
	%1001010000000001 op: neg,
	%1001010000000111 op: ror,
	%1001001000001100 op: stx,
	%1001001000001101 op: stx+,
	%1001001000001110 op: st-x,
	%1000001000001000 op: sty,
	%1001001000001001 op: sty+,
	%1001001000001010 op: st-y,
	%1000001000000000 op: stz,
	%1001001000000001 op: stz+,
	%1001001000000010 op: st-z,
	%1001010000000010 op: swap,
flag-elpm to test-flag
  %1001000000000110 op: elpm-2,
  %1001000000000111 op: elpm-3,
flag-pop to test-flag
  %1001000000001111 op: pop,
flag-push to test-flag
  %1001001000001111 op: push,

\ ~**********???? ???? ???? ????**************~

: nop, %0000000000000000 opc, ;
' nop, alias noop,

: opcode-pattern-??????????????????? ( opc -- )
  create , does> @
  opc, ;
' opcode-pattern-??????????????????? to op-xt

all-models
	%1001010010001000 op: clc,
	%1001010011011000 op: clh,
	%1001010011111000 op: cli,
	%1001010010101000 op: cln,
	%1001010011001000 op: cls,
	%1001010011101000 op: clt,
	%1001010010111000 op: clv,
	%1001010010011000 op: clz,
	%1001010111001000 op: lpm,
	%1001010100001000 op: ret,
	%1001010100011000 op: reti,
	%1001010000001000 op: sec,
	%1001010001011000 op: seh,
	%1001010001111000 op: sei,
	%1001010000101000 op: sen,
	%1001010001001000 op: ses,
	%1001010001101000 op: set,
	%1001010000111000 op: sev,
	%1001010000011000 op: sez,
	%1001010110001000 op: sleep,
	%1001010110101000 op: wdr,
flag-ijmp to test-flag
  %1001010000001001 op: ijmp,
flag-eijmp to test-flag
  %1001010000011001 op: eijmp,
flag-elpm to test-flag
  %1001010111011000 op: elpm-1,
flag-spm to test-flag
  %1001010111101000 op: spm-1,
  %1001010111101000 op: spm-2-1,
  %1001010111101000 op: spm-2-2,
  %1001010111101000 op: spm-2-3,
  %1001010111111000 op: spm-2-4,
  %1001010111111000 op: spm-2-5,
  %1001010111111000 op: spm-2-6,
flag-icall to test-flag
  %1001010100001001 op: icall,
flag-eicall to test-flag
  %1001010100011001 op: eicall,
flag-break to test-flag
  %1001010110011000 op: break,

\ ##############################################################################
\ ################JUMPS#########################################################
\ ##############################################################################

: brXX ( distance opc -- opc )
  \ ??????KKKKKKK???
  swap 2/ 1 -
  dup -63 63 check-operand abort" Distance to far"
  dup 0 < IF
    negate 128 swap -
  THEN
  3 lshift or ;

: brXX-after ( src distance opc -- )
  brXX
  dup 255 And 2 pick X c! \ src dist opc opc25
  8 rshift swap 1 + X c! ;

: r-jmp-call ( distance opc -- opc )
  swap 2/ 1 -
  dup -2048 2047 check-operand abort" Distance to far"
  dup 0 < IF
    negate 4096 swap -
  THEN
  \ opc distance
  or ;

: r-jmp-call-after ( src distance opc -- )
  r-jmp-call
  dup 255 And 2 pick X c!
  8 rshift swap 1 + X c! ;

: jmp-call ( addr opc -- opc1 opc2 )
  \ ???????KKKKK???KKKKKKKKKKKKKKKKK
  swap 2/ 1 - dup 4194303 0check-operand abort" Distance to far"
  dup [ %11111 16 lshift ] literal and 3 lshift rot or
  over [ 1 16 lshift ] literal and 3 lshift or
  swap [ 1 16 lshift 1 - ] literal and ;

: jmp-call-after ( src distance opc -- )
  jmp-call
  \ addr opc1 opc2
  swap dup 255 And 3 pick X c!
  8 rshift 2 pick X c!
  dup 255 And  2 pick X c!
  8 rshift swap X c! ;

: brb-sc ( distance s opc -- opc )
  \ ??????KKKKKKKSSS
  rot 2 / 1 - dup -64 63 check-operand abort" Distance to far"
  dup 0 < IF
    negate 128 swap -
  THEN
  rot dup 7 0check-operand abort" Wrong operand value" \ opc distance s
  rot or swap 3 lshift or ;

\ ##############################Labels set/call#################################

: $-check ( src distance opc -- )
  dup
  \ FIXME: that looks odd
  CASE
    61440 OF brXX-after ENDOF
    61441 OF brXX-after ENDOF
    61442 OF brXX-after ENDOF
    61443 OF brXX-after ENDOF
    61444 OF brXX-after ENDOF
    61445 OF brXX-after ENDOF
    61446 OF brXX-after ENDOF
    61447 OF brXX-after ENDOF
    62464 OF brXX-after ENDOF
    62465 OF brXX-after ENDOF
    62466 OF brXX-after ENDOF
    62467 OF brXX-after ENDOF
    62468 OF brXX-after ENDOF
    62469 OF brXX-after ENDOF
    62470 OF brXX-after ENDOF
    62471 OF brXX-after ENDOF
    49152 OF r-jmp-call-after ENDOF
    53248 OF r-jmp-call-after ENDOF
    37888 OF jmp-call-after ENDOF
    abort" $-check hat einen kritischen Fehler festgestellt."
  ENDCASE ;

: $: ( index -- )
  branch-dest over cells + @ 0 = IF
    X here branch-dest 2 pick cells + ! \ index
    \ Check if there are allready addresses that targeting that label
    limitperlabel 0 DO \ index
      dup limitperlabel * I + dup branch-src swap cells + @ dup 0 <> IF
        \ if there are addresses that targeting this label \ index index* src
        -rot drop swap \ index src ## index src addr-src src c@
        X here over - over X c@ 2 pick 1 + X c@ 8 lshift +
        \ Prüfung welcher Art es ist und entsprechend verzweigen
        $-check
      ELSE 2drop THEN
    LOOP drop
  ELSE -1 abort" Labels can't be set twice!" THEN ;

: $ ( index -- addr )
  branch-dest over cells + @ 0<> IF \ if we know the address of the label
    branch-dest swap cells + @ \ addr
  ELSE \ if there is no address given now
    limitperlabel 0 DO
      dup limitperlabel * I + branch-src over cells + @ 0 = IF
        X here branch-src 2 pick cells + ! 2drop 0 leave \ 0
      THEN drop
    LOOP THEN ;

\ ~**********???? ??KK KKKK K???**************~

: opcode-pattern-??????KKKKKKK??? ( opc1 opc2 -- )
\ FIXE move create does> that into op:
  create , does> @ ( addr opc )
  over 0<> IF
    \ if the address to jump is known
    swap X here - swap
    brXX
    opc,
  ELSE
    \ if there is no address, so use the label index
    swap 2 + swap
    brXX
    opc,
  THEN ;
' opcode-pattern-??????KKKKKKK??? to op-xt

all-models
	%1111010000000000 op: brcc,
	%1111000000000000 op: brcs,
	%1111000000000001 op: breq,
	%1111010000000100 op: brge,
	%1111010000000101 op: brhc,
	%1111000000000101 op: brhs,
	%1111010000000111 op: brid,
	%1111000000000111 op: brie,
	%1111000000000000 op: brlo,
	%1111000000000100 op: brlt,
	%1111000000000010 op: brmi,
	%1111010000000001 op: brne,
	%1111010000000010 op: brpl,
	%1111010000000000 op: brsh,
	%1111010000000110 op: brtc,
	%1111000000000110 op: brts,
	%1111010000000011 op: brvc,
	%1111000000000011 op: brvs,

\ ~**********???? KKKK KKKK KKKK**************~

: opcode-pattern-????KKKKKKKKKKKK ( opc -- )
  create , does> @ ( adr opc -- )
  over 0<> IF
    \ if the address to jump is known
    swap X here - swap
    r-jmp-call
    opc,
  ELSE
    \ if there is no address, so use the label index
    swap 2 + swap
    r-jmp-call
    opc,
  THEN ;
' opcode-pattern-????KKKKKKKKKKKK to op-xt

all-models
	%1101000000000000 op: rcall,
	%1100000000000000 op: rjmp,

\ ~**********???? ???K KKKK ???K**************~
\ ~**********KKKK KKKK KKKK KKKK**************~

: opcode-pattern-???????KKKKK???KKKKKKKKKKKKKKKKK ( opc1 opc2 -- )
  create , does> @ ( adr opc -- )
  over 0<> IF
    \ if the address to jump is known
    jmp-call
    opc, opc,
  ELSE
    \ if there is no address, so use the label index
    swap 2 + swap
    jmp-call
    swap opc, opc,
  THEN ;
' opcode-pattern-???????KKKKK???KKKKKKKKKKKKKKKKK to op-xt

\ FIXME jump
flag-jmp to test-flag
	%1001010000001100 op: jmp,-XXX-double-check-and-fix-address
flag-call to test-flag
	%1001010000001110 op: call,-XXX-double-check-and-fix-address

\ ~**********???? ??KK KKKK KSSS**************~

: opcode-pattern-??????KKKKKKKSSS ( opc -- )
  create , does> @ ( adr s opc -- )
  2 pick 0<> IF
    \ if the address to jump is known
    rot X here - -rot
    brb-sc
    opc,
  ELSE
    \ if there is no address, so use the label index
    swap 2 + swap
    brb-sc
    opc,
  THEN ;
' opcode-pattern-??????KKKKKKKSSS to op-xt

all-models
	%1111010000000000 op: brbc,
	%1111000000000000 op: brbs,

\ ##############################################################################
\ ################JUMPS        ENDE#############################################
\ ##############################################################################

\ ~**********???? ??DD DDDD DDDD**************~

: opcode-pattern-??????DDDDDDDDDD ( opc --)
  create , does> @ ( d opc -- )
  over check-register
  over 4 lshift or
  swap dup 16 and 5 lshift rot or swap
  15 and or
  opc, ;
' opcode-pattern-??????DDDDDDDDDD to op-xt

all-models
	%0010010000000000 op: clr,
	%0000110000000000 op: lsl,
	%0001110000000000 op: rol,
	%0010000000000000 op: tst,

\ ~**********???? ???? DDDD RRRR**************~

: opcode-pattern-????????DDDDRRRR ( opc -- )
  create , does> @ ( d r opc -- )
  2 pick 0 30 check-register-special
  2 pick 2 mod 0 > abort" Wrong register value"
  over 0 30 check-register-special
  over 2 mod 0 > abort" Wrong register value"
  rot 2 / 4 lshift or swap 2 / or
  opc, ;
' opcode-pattern-????????DDDDRRRR to op-xt

flag-movw to test-flag
  %0000000100000000 op: movw,
flag-muls to test-flag
  %0000001000000000 op: muls,

\ ~**********???? KKKK DDDD KKKK**************~

: operation-????KKKKDDDDKKKK ( d k opc -- opc1 opc2 )
  2 pick 16 31 check-register-special
  over 0 255 check-operand abort" Wrong operand value"
  rot 16 - 4 lshift or \ k opc
  swap dup 240 and 4 lshift swap 15 and or or ;

: opcode-pattern-????KKKKDDDDKKKK ( opc -- )
  create , does> @ ( d k opc -- )
  operation-????KKKKDDDDKKKK
  opc, ;
' opcode-pattern-????KKKKDDDDKKKK to op-xt

all-models
	%0111000000000000 op: andi,
	%0111000000000000 op: cbr,
	%0011000000000000 op: cpi,
	%1110000000000000 op: ldi,
	%0110000000000000 op: ori,
	%0100000000000000 op: sbci,
	%0110000000000000 op: sbr,
	%0101000000000000 op: subi,

\ ~**********???? ???? ?DDD ?RRR**************~

: opcode-pattern-?????????DDD?RRR ( opc1 opc2 -- )
  create , does> @ ( d r opc -- )
  2 pick 16 23 check-register-special
  over 16 23 check-register-special
  swap 16 - or swap 16 - 4 lshift or
  opc, ;
' opcode-pattern-?????????DDD?RRR to op-xt

flag-mulsu to test-flag
	%0000001100000000 op: mulsu,
flag-fmul to test-flag
	%0000001100001000 op: fmul,
flag-fmuls to test-flag
	%0000001110000000 op: fmuls,
flag-fmulsu to test-flag
	%0000001110001000 op: fmulsu,

\ ~**********???? ???? ?SSS ????**************~

: opcode-pattern-?????????SSS???? ( opc1 opc2 -- )
  create , does> @ ( s opc -- )
  over 7 0check-operand abort" Wrong operand value"
  swap 4 lshift or
  opc, ;
' opcode-pattern-?????????SSS???? to op-xt

all-models
	%1001010010001000 op: bclr,
	%1001010000001000 op: bset,

\ ~**********???? ???D DDDD ?BBB**************~

: opcode-pattern-???????DDDDD?BBB ( opc1 opc2 )
  create , does> @ ( d b opc )
  2 pick check-register
  over 7 0check-operand abort" Wrong operand value"
  swap or swap 4 lshift or
  opc, ;
' opcode-pattern-???????DDDDD?BBB to op-xt

all-models
	%1111100000000000 op: bld,
	%1111101000000000 op: bst,
	%1111110000000000 op: sbrc,
	%1111111000000000 op: sbrs,

\ ~**********???? ???? AAAA ABBB**************~

: opcode-pattern-????????AAAAABBB ( opc -- )
  create , does> @ ( A B opc -- )
  over 7 0check-operand abort" Wrong operand value"
  2 pick check-register
  or swap 3 lshift or
  opc, ;
' opcode-pattern-????????AAAAABBB to op-xt

all-models
	%1001100000000000 op: cbi,
	%1001101000000000 op: sbi,
	%1001100100000000 op: sbic,
	%1001101100000000 op: sbis,

\ ~**********???? ?KKK DDDD KKKK**************~

: opcode-pattern-?????KKKDDDDKKKK ( opc -- )
  create , does> @ ( d K opc -- )
  2 pick 16 31 check-register-special
  over 127 0check-operand abort" Wrong operand value"
  rot 16 - 4 lshift or
  over 112 and 4 lshift or
  swap 15 and or
  opc, ;
' opcode-pattern-?????KKKDDDDKKKK to op-xt

flag-lds to test-flag
	%1010000000000000 op: lds-16b,
flag-sts to test-flag
	%1010100000000000 op: sts-16b,

\ ~**********???? ?AAD DDDD AAAA**************~

: opcode-pattern-?????AADDDDDAAAA ( opc -- )
  create , does> @ ( d A opc -- )
  2 pick check-register
  over 63 0check-operand abort" Wrong operand value"
  rot 4 lshift or over
  48 and 5 lshift or
  swap 15 and or
  opc, ;
' opcode-pattern-?????AADDDDDAAAA to op-xt

all-models
	%1011000000000000 op: in,
	%1011100000000000 op: out,

\ ~**********???? ???? KKDD KKKK**************~

: opcode-pattern-????????KKDDKKKK ( opc -- s)
  create , does> @ ( d k opc -- )
  over 63 0check-operand abort" Wrong operand value"
  2 pick 24 - 2 /mod 0 3 check-register-special
  1 = abort" Wrong register value"
  rot 24 - 2/ 4 lshift or \ k opc
  \ K5:K4 go to bits 7:6, above the register bits 5:4
  over 48 and 2 lshift or
  swap 15 and or
  opc, ;
' opcode-pattern-????????KKDDKKKK to op-xt

flag-adiw to test-flag
	%1001011000000000 op: adiw,
flag-sbiw to test-flag
	%1001011100000000 op: sbiw,

\ ~**********??Q? QQ?D DDDD ?QQQ**************~
: opcode-pattern-??Q?QQ?DDDDD?QQQ ( opc1 opc2 -- )
  create , does> @ ( d q opc -- )
  -rot swap rot
  2 pick 63 0check-operand abort" Wrong operand value"
  over check-register
  swap 4 lshift or \ q opc
  over 32 and 8 lshift or
  over 24 and 7 lshift or
  swap 7 and or
  opc, ;
' opcode-pattern-??Q?QQ?DDDDD?QQQ to op-xt

flag-ldd to test-flag
	%1000000000001000 op: lddy,
	%1000000000000000 op: lddz,
flag-std to test-flag
	%1000001000001000 op: stdy,
	%1000001000000000 op: stdz,

\ ~**********???? ???D DDDD ????**************~
\ ~**********KKKK KKKK KKKK KKKK**************~

: opcode-pattern-???????DDDDD????KKKKKKKKKKKKKKKK ( opc1 opc2 -- )
  create , does> @ ( k d opc -- )
  2 pick 65535 0check-operand abort" Wrong operand value"
  over check-register
  swap 4 lshift or opc,
  opc, ;
' opcode-pattern-???????DDDDD????KKKKKKKKKKKKKKKK to op-xt

flag-lds to test-flag
  %1001000000000000 op: lds,
flag-sts to test-flag
  %1001001000000000 op: sts,

\ ~**********???? ???? DDDD ????**************~

: ser, ( d -- )
	%1110111100001111
  over 16 31 check-register-special
  swap 16 - 4 lshift or
  opc, ;

\ ~**********???? ???? KKKK ????**************~

: des, ( k -- )
	%1001010000001011
  over 15 0check-operand abort" Wrong operand value"
  swap 4 lshift or
  opc, ;


\ advanced checks for register commands

: in/lds, ( n n -- )
  dup $60 u< IF
    $20 - dup 0< abort" Expecting data memory address"
    in,
  ELSE
    swap
    lds,
  THEN ;

: out/sts, ( n n -- )
  dup $60 u< IF
    $20 - dup 0< abort" Expecting data memory address"
    out,
  ELSE
    swap
    sts,
  THEN ;

0 [IF]
: sbi/out/sts, ( n n n -- )
  dup $40 < IF
    drop sbi,
  ELSE
    dup 3 pick in/lds,
    dup 2 pick ori,  \ ori @2,exp2(@1)
    2 pick swap out/sts, \ out/sts, @0,@2
    2drop
  THEN ;

: cbi/out/sts,
  dup $40 < IF
    drop cbi,
  ELSE
    dup 2 pick andi, \ andi @2,~(exp2(@1))
    2 pick swap out/sts, \ out/sts, @0,@2
    2drop
  THEN ;
[THEN]

previous definitions

." asm fin"