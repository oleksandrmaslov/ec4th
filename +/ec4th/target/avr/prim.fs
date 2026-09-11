\ AVR primitives

decimal



start-macros
\ Jump to the code of the word W points to, l is a free local label number.
\ ROM words (the whole kernel) fall through and save a cycle, words defined
\ at the prompt take the branch
: dispatch, ( l -- )
	ZL WL movw,
	?rom-address-cp,
	dup $ brcc,
	zh-mask-rom-address,
	temp0 lpmZ+, temp1 lpmZ+,
	ZL temp0 movw,
	zh-mask-rom-address,    \ Remove high bit marking the rom address
	ZH lsr, ZL ror, \ TODO make doer word addressed to save shifts?
	ijmp,
	$:
	temp0 ldZ+, temp1 ldZ+,
	ZL temp0 movw,
	zh-mask-rom-address,
	ZH lsr, ZL ror,
	ijmp, ;
end-macros

Label into-forth
	'f pout
	$0d pout
	$0a pout
	zero clr,
	\ sleep mode control register
	zero smcr out/sts,
	\ Initialization: Rom Start
	mempivot-init,

	\ init data stack
	init-dataStack addr>pm
	ZH over highbyte ldi, ZL swap lowbyte ldi,
	YL lpmz+, YH lpmz,
	stack-grows-upwards [IF]
		yl 2 adiw,
	[THEN]
	\ first stack cell content is $baaad / $adba
	\ by holding the tos in registers we loose two bytes in data space
	\ initial tos content, that gets written at the start of the stack
	loadtos
	tosl $ba ldi,
	tosh $ad ldi,
	savetos

	\ tos is 0 => no error
	tosl clr, tosh clr,

\ Error entry, expect the error code in TOS
Label on-error
	sei, \ make sure interrupts are enabled and we receive serial data

	\ init data stack, again for error entry
	init-dataStack addr>pm
	ZH over highbyte ldi, ZL swap lowbyte ldi,
	YL lpmz+, YH lpmz,
	stack-grows-upwards [IF]
		yl 2 adiw,
	[THEN]
	\ init instruction pointer
	init-ip addr>pm 
	ZL over lowbyte ldi, ZH swap highbyte ldi,
	IPL lpmz+, IPH lpmz,

	\ init return stack
	init-ramend addr>pm
	ZL over lowbyte ldi, ZH swap highbyte ldi,
	temp0 lpmz+, temp0 SPL out/sts,
	temp0 lpmz, temp0 SPH out/sts,

	'~ pout
	$0d pout
	$0a pout

Label do_next
	'| pout
	$0d pout
	$0a pout
	?rom-address-xh-cp, 
	0 $ brcs,
	WL ldx+, WH ldx+, \ X is IP, advance and fetch next instruction from RAM
	\ a copy of the dispatch instead of a jump to it saves 2 cycles
	3 dispatch,
0 $:
	zl xl movw,
	IPL 2 adiw,
	zh-mask-rom-address,     \ Remove high bit marking the rom address
	WL lpmZ+, WH lpmZ+,
Label do_next2
	2 dispatch,
end-label+

\ Stack overflow found by the check in :docol. Both stacks share one region:
\ report -3 if the data stack uses more of it than the return stack, else -5.
\ on-error resets both stacks and restarts the interpreter with that code.
Label stack-overflow
	ZL SPL in/lds,
	ZH SPH in/lds,
	ZL YL add,
	ZH YH adc,
	ZL data-stack-init return-stack-init + lowbyte subi,
	ZH data-stack-init return-stack-init + highbyte sbci,
	tosl -5 255 and ldi,
	tosh 255 ldi,
	5 $ brcs,
	tosl -3 255 and ldi,
5 $:
	on-error rjmp,
end-label+

Code: :docol
\ start a colon defined forth word
	': pout
	IPH push, IPL push,
	\ stack check, SP - Y is the free space between the stacks. The margin
	\ covers an interrupt frame (7 bytes) and pushes by primitives until the
	\ next check. subi/sbci because the assembler encodes sbiw K >= 16 wrong
	ZL SPL in/lds,
	ZH SPH in/lds,
	ZL 24 subi,
	ZH 0 sbci,
	ZL YL cp,
	ZH YH cpc,
	stack-overflow brcs,
	IPL WL movw,
	IPL 4 adiw, 
	do_next rjmp,
End-Code+

Code ;s
\ end a colon defined forth-word
	'; pout
	IPL pop, IPH pop,
	do_next rjmp,
end-code+

Code: :dovar
	\ save variable to ram
	savetos
	tosl WL movw,
	tosl 4 adiw, 
	do_next rjmp,
End-Code+

Code: :docon
	\ save a constant
	savetos
	ZL WL movw,
	ZL 4 adiw,
	?rom-address-cp,
	0 $ brcc,
	zh-mask-rom-address,
	tosl lpmZ+, tosh lpmZ+,
	do_next rjmp,
	0 $:
	tosl ldZ+, tosh ldZ+,
	do_next rjmp,
End-Code+

Code: :dodoes
  	savetos
	IPH push, IPL push, \ IP sichern
	ZL WL movw, \ im Wort dann
	ZL 2 adiw,
	\ check if its a ram address
	?rom-address-cp,
	0 $ brcc,
	zh-mask-rom-address,
	IPL lpmZ+, IPH lpmZ+,
	tosl ZL movw,
	tosh-pm-forth, \ Z was masked, the body of a ROM child keeps its ROM bit
	do_next rjmp,
	0 $:
	IPL ldZ+, IPH ldZ+,
	tosl ZL movw,
	do_next rjmp,
End-Code+

Code execute
\ executes forth-word thats address is on the tos
	WL tosl movw,
	loadtos
	do_next2 rjmp,
End-Code+

code lit
\ save a literal to tos, first save current tos to data-stack
'l pout
	savetos
	ZL IPL movw,
	IPL 2 adiw,
	?rom-address-cp,
	0 $ brcc,
	zh-mask-rom-address,
	tosl lpmZ+, tosh lpmZ+,
	do_next rjmp,
	0 $:
	tosl ldZ+, tosh ldZ+,
	do_next rjmp,
end-code+

\ ##############################################################################
\ ##############################Branch / Loop###################################
\ ##############################################################################

code branch
label branch
	ZL IPL movw,
	?rom-address-cp,
	0 $ brcc,
	zh-mask-rom-address,
	temp0 lpmz+, temp1 lpmz+,
	IPL temp0 add, IPH temp1 adc,
	do_next rjmp,
	0 $:
	temp0 ldZ+, temp1 ldZ+,
	IPL temp0 add, IPH temp1 adc,
	do_next rjmp,
end-code+

code ?branch
	tosl tosh or,
	loadtos
	branch breq,
label skip_next
	IPL 2 adiw,
	do_next rjmp,
end-code+

code (loop)
	\ TODO: (loop) maybe optimse more?
	zl pop, zh pop, \ index
	temp4 pop, temp5 pop, \ limit
	zl 1 adiw,
	temp5 push, temp4 push,
	zh push, zl push,
	zl temp4 cp,
	zh temp5 cpc,
	branch brcs,
	skip_next rjmp,
end-code+

code unloop
	temp0 pop, temp0 pop,
	temp0 pop, temp0 pop,
	do_next rjmp,
end-code+

code (do)
	temp0 tosl movw, \ index
	loadtos
	tosh push, tosl push, \ limit
	temp1 push, temp0 push, \ index
	loadtos
	do_next rjmp,
end-code+

\ ##############################################################################
\ ##############################Stack manipulation##############################
\ ##############################################################################

Code swap ( n1 n2 -- n2 n1 )
	\ swap the last two stack items
	temp0 tosl movw,
	loadtos
	savetemp
	do_next rjmp,
End-Code+

Code drop ( n -- )
	\ drops the TOS
	loadtos
	do_next rjmp,
End-Code+

Code 2drop ( n1 n2 --  )
	\ drops 2 stack items
stack-grows-upwards [IF]
	YL 2 sbiw,
[ELSE]
  	YL 2 adiw,
[THEN]
  	loadtos
	do_next rjmp,
End-Code+

Code dup ( n -- n n )
	\ duplicates TOS
	savetos
	do_next rjmp,
End-Code+

Code over ( n1 n2 -- n1 n2 n1 )
\ duplicates the item before TOS
stack-grows-upwards [IF]
	\ 2 cycles more than downwards version
	loadtemp
	YL 2 adiw,	
	savetos
	tosl temp0 movw,
[ELSE]
	savetos
	tosl 2 lddY,
	tosh 3 lddY,
[THEN]
	do_next rjmp,
End-Code+

Code 2dup ( d -- d d  )
\ duplicates a double
stack-grows-upwards [IF]
	temp0 tosl movw,
	loadtos
	YL 2 adiw,	
	savetemp
	savetos
	tosl temp0 movw,
[ELSE]
	savetos
	tosl 2 lddY, tosh 3 lddY,
	savetos
	tosl 2 lddY, tosh 3 lddY,
[THEN]
	do_next rjmp,
End-Code+

Code nip ( n1 n2 -- n2 )
stack-grows-upwards [IF]
	YL 2 sbiw,
[ELSE]
	YL 2 adiw,
[THEN]
	do_next rjmp,
End-Code+

Code tuck ( n1 n2 -- n2 n1 n2 )
\ around two cycles more than 
stack-grows-upwards [IF]
	loadtemp
	savetos
	savetemp
[ELSE]
	\ FIMXE that looks strange
	temp0 0 lddY, temp1 1 lddY,
	tosl 0 stdy, tosh 1 stdy,
	temp1 st-Y, temp0 st-Y,
[THEN]
	do_next rjmp,
End-Code+

Code todigit ( u -- c )
\G Character of the digit u: 0..9 give '0'..'9', 10..35 give 'A'..'Z'.
\G Same as : todigit 9 over < 7 and + [char] 0 + ; (signed compare)
	tosl 10 cpi,
	tosh zero cpc,
	0 $ brlt,
	tosl 7 adiw,
0 $:
	tosl 48 adiw,
	do_next rjmp,
End-Code+

Code rot ( n1 n2 n3 -- n2 n3 n1 )
\ needs two scratch registers, e.g. temp0 temp1
stack-grows-upwards [IF]
	temp2 tosl movw,
	loadtemp
	loadtos
	savetemp
	savetemp2
[ELSE]
	temp0 2 lddY,
	temp1 3 lddY,
	r0 0 lddY,   r0 2 stdY,
	r0 1 lddY,   r0 3 stdY,
	tosl 0 stdY,
	tosh 1 stdY,
	tosl temp0 mov,
	tosh temp1 mov,
[THEN]
	do_next rjmp,
End-Code+

Code -rot ( n1 n2 n3 -- n3 n1 n2 )
stack-grows-upwards [IF]
	temp2 tosl movw,
	loadtos
	loadtemp
	savetemp2
	savetemp
[ELSE]
	temp0 0 lddY,
	temp1 1 lddY,
	r0 2 lddY,   r0 0 stdY,
	r0 3 lddY,   r0 1 stdY,
	tosl 2 stdY,
	tosh 3 stdY,
	tosl temp0 mov,
	tosh temp1 mov,
[THEN]
	do_next rjmp,
End-Code+

Code ?dup ( n -- 0 | n n )
	temp0 tosl mov,
	temp0 tosh or,
	0 $ breq,
	savetos
	0 $:
	do_next rjmp,
End-Code+

\ ##############################################################################
\ ################################# Arithmetic #################################
\ ##############################################################################

Code - ( n1 n2 -- n3 )
	\ substract n2 from n1
	loadtemp
	temp0 tosl sub, temp1 tosh sbc,
	tosl temp0 movw,
	do_next rjmp,
End-Code+

Code + ( n1 n2 -- n3 )
	\ adds n2 to n1
	temp0 tosl movw,
	loadtos
	tosl temp0 add, tosh temp1 adc,
	do_next rjmp,
End-Code+

Code 2+ ( n1 -- n2 )
	\ adds the size of one cell to n1
	\ cell+ will be aliased to 2+
	tosl 2 adiw,
	do_next rjmp,
End-Code+

Code 1+ ( n1 -- n2 )
	\ add one to tos, also used for char+
	tosl 1 adiw,
	do_next rjmp,
End-Code+

Code 1- ( n1 -- n2 )
	\ subctract 1
Label rp@adjust
	tosl 1 subi,
	tosh zero sbc,
	do_next rjmp,
End-Code+

Code 2/ ( n1 -- n2 )
	\ devides n1 by 2
	tosh asr, tosl ror,
	do_next rjmp,
End-Code+

Code 2* ( n1 -- n2  )
	\ multiplies n1 by 2
	tosl lsl, tosh rol,
	do_next rjmp,
End-Code+

\ ##############################################################################
\ ############################# Comparisons ####################################
\ ##############################################################################

Code 0= ( n1 -- n2 )
	tosl tosh or,
	0 $ breq, 
Label press-false	
	tosl clr, tosh clr,
	do_next rjmp,
Label press-true
	0 $:
	tosl $ff ldi, tosh $ff ldi,
	do_next rjmp,
End-Code+

Code 0<> ( n1 -- n2 )
	tosl tosh or,
	press-true brne, 
	press-false rjmp,
End-Code+

Code 0< ( n1 -- n2 )
	tosh $80 andi,
	press-true brne, 
	press-false rjmp,
End-Code+

Code u< ( u1 u2 -- f )
	temp2 clr,
	loadtemp
	temp0 tosl cp,
	temp1 tosh cpc,
	\ fill tos from carry, should be 1 cycle faster then jump
	temp2 zero sbc,
	tosl temp2 mov,
	tosh temp2 mov,
	do_next rjmp,
End-Code+

\ ##############################################################################
\ ################################## Shifts ####################################
\ ##############################################################################

Code rshift ( u1 n -- u2 )
\G Logical shift right by n bits. Gives 0 for every n >= 16, as the colon
\G loop did. n >= 8 moves a whole byte first, then at most 7 single shifts
	temp0 tosl movw,
	loadtos
	temp1 tst,
	press-false brne,	\ n >= 256
	temp0 8 cpi,
	1 $ brlo,
	tosl tosh mov,
	tosh clr,
	temp0 8 subi,
	temp0 8 cpi,
	press-false brsh,	\ n >= 16
1 $:
	temp0 tst,
	3 $ breq,
2 $:
	tosh lsr, tosl ror,
	temp0 dec,
	2 $ brne,
3 $:
	do_next rjmp,
End-Code+

Code lshift ( u1 n -- u2 )
\G Shift left by n bits. Gives 0 for every n >= 16, as the colon loop did
	temp0 tosl movw,
	loadtos
	temp1 tst,
	4 $ brne,	\ n >= 256
	temp0 8 cpi,
	1 $ brlo,
	tosh tosl mov,
	tosl clr,
	temp0 8 subi,
	temp0 8 cpi,
	4 $ brsh,	\ n >= 16
1 $:
	temp0 tst,
	3 $ breq,
2 $:
	tosl lsl, tosh rol,
	temp0 dec,
	2 $ brne,
3 $:
	do_next rjmp,
4 $:
	press-false rjmp,
End-Code+

\ ##############################################################################
\ ############################# Return Stack ###################################
\ ##############################################################################

Code r> ( S: -- n ; R: n -- )
	\ moves one item from return stack to TOS
	savetos
	tosl pop, tosh pop,
	do_next rjmp,
End-Code+

Code >r ( S: n -- ; R: -- n )
	\ moves TOS into Return-Stack
	tosh push, tosl push,
	loadtos
	do_next rjmp,
End-Code+

Code r@ ( S: -- n ; R: n -- n )
	\ duplicates one item from return stack to TOS
	\ Same as I for DO ... LOOP
	savetos
	zl SPL in/lds,
	zh SPH in/lds,
	tosl 1 lddz, tosh 2 lddz,
\ alternative, just pop and push, above should be faster
\	tosl pop, tosh pop,
\	tosh push, tosl push,
	do_next rjmp,
End-Code+

Code rp@ ( S: -- addr ; R: -- )
	savetos
	tosl SPL in/lds,
	tosh SPH in/lds,
	\ align so : r@ rp@ cell+ @ ;
	\ the actual pointer is always off by 1, since we write bytes not cells
	rp@adjust rjmp,
End-Code+

Code stack-space ( -- n )
\G free stack space
	savetos
	tosl SPL in/lds,
	tosh SPH in/lds,
	tosl YL sub,
	tosh YH sbc,
	do_next rjmp,
End-Code+

\ ##############################################################################
\ ##########################Store & Fetch System################################
\ ##############################################################################

Code @ ( addr -- n )
	\ read 1 cell from RAM (or IO/CPU register)
	ZL tosl movw,
	\ check if it is not a RAM addr
	?rom-address-cp,
  \ Without local labels: here 8 + brcs,
  0 $ brcs,
	tosl ldZ+, tosh ldZ+,
	do_next rjmp,
	0 $:
	zh-mask-rom-address,
	tosl lpmZ+, tosh lpmZ+,
	do_next rjmp,
End-Code+

Code c@ ( addr -- c )
	\ fetch a byte from RAM (or IO/CPU register)
	ZL tosl movw,
	tosh clr,
	\ check if it is not a RAM addr
	?rom-address-cp,
	0 $ brcs,
	tosl ldZ+,
	do_next rjmp,
	0 $:
	zh-mask-rom-address,
	tosl lpmZ+,
	do_next rjmp,
End-Code+

Code ! ( n addr -- )
	\ writes 16 bit to RAM memory (or IO/CPU regis.)
	ZL tosl movw,
	loadtos
	\ 1 tosh stdZ, 0 tosl stdZ,
	tosl stZ+, tosh stZ+,
	loadtos
	do_next rjmp,
End-Code+

Code c! ( c addr -- )
	\ store a byte to RAM address
	ZL tosl movw,
	loadtos
	tosl stZ,
	loadtos
	do_next rjmp,
End-Code+

Code sp@ ( -- addr  )
	'% pout
	\ returns stack pointer position
stack-grows-upwards [IF]
	temp0 YL movw,
	savetos
	tosl temp0 movw,
[ELSE]
	savetos
	tosl YL movw,
[THEN]
	do_next rjmp,
End-Code+

e? stack-grows-upwards [IF]
Code sp! ( addr -- )
\G Set the data stack pointer, sp@ returns addr afterwards. A primitive,
\G because the colon version in catch-throw.fs refills a deep underflow
\G with zeros, which wipes the RAM dictionary below the stacks
	YL tosl movw,
	tosl 0 lddY,
	tosh 1 lddY,
	do_next rjmp,
End-Code+
[THEN]

\ ##############################################################################
\ ############################ Bit twiddling ###################################
\ ##############################################################################

Code and ( n1 n2 -- n3 )
\G bitwise logical and
	loadtemp
	tosl temp0 and, tosh temp1 and,
	do_next rjmp,
End-Code+

Code or ( n1 n2 -- n3 )
\G bitwise logical or
	loadtemp
	tosl temp0 or, tosh temp1 or,
	do_next rjmp,
End-Code+

Code xor ( n1 n2 -- n3 )
	\ exclusive or
	\ temp0 tosl movw,
	\ loadtos
	loadtemp
	tosl temp0 eor, tosh temp1 eor,
	do_next rjmp,
End-Code+

Code invert ( n1 -- n2)
	\ invert / one complement
	tosl com, tosh com,
	do_next rjmp,
End-Code+

Code negate ( n1 -- n2)
\ twos complement
	\ seems not to work with neg:
	\ tosl neg, 
	\ tosh zero sbc,
	\ tosh neg,
	temp0 clr,
	temp1 clr,
	temp0 tosl sub,
	temp1 tosh sbc,
	tosl temp0 movw,
	do_next rjmp,
End-Code+

Code dmicros ( -- ud )
	savetos
	cli,
	temp1 ticks2 mov,
	temp0 ticks1 mov,
	tosh ticks0 mov,
	tosl TCNT0 in/lds,
	sei,
	\ *4 since we get 4 microseonds ticks
	tosl lsl, tosh rol, temp0 rol, temp1 rol,
	tosl lsl, tosh rol, temp0 rol, temp1 rol,
	savetos
	tosl temp0 movw,
	do_next rjmp,
End-Code+

Code millis ( -- u )
	savetos
	cli,
	tosl millis0 mov,
	tosh millis1 mov,
	sei,
	do_next rjmp,
End-Code+

Code dmillis ( -- ud )
	savetos
	cli,
	tosl millis0 mov,
	tosh millis1 mov,
	savetos
	tosl millis2 mov,
	tosh millis3 mov,
	sei,
	do_next rjmp,
End-Code+

\ put the CPU to sleep forever, this will exit the simavr simulator 20260307;jw
Code bye ( -- ) 
	cli,
	sleep,
End-Code+

\ restart Forth as after power up: stacks, the RAM dictionary image (HERE,
\ the word list, BASE, STATE ...) and the banner. The USART, timer0 and
\ input already waiting in the ring buffer are kept.
Code cold ( -- )
	cli,
	into-forth rjmp,
End-Code+


\ include io/dot_s.fs
\ include io/emit_key.fs

\ sp0 is expected to the a variable and usually defined as: User sp0
\ FIXME should we keep that as "variable", check other forth systems
here start-macros data-stack-init end-macros , Constant sp0

HEX