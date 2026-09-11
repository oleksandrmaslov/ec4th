\ USART ring buffer implementation for Arduino / AVR        13mar26jaw

\ When the Forth system is interpreting or compiling, subsequent 
\ input will get lost in case there is only one character buffer.
\ This receives serial input in an interrupt service routine (ISR) and collects
\ it in a ring buffer. The ISR interprets a Ctrl-C and also an detects an
\ overflow. In this case it restarts the Forth interpreter with an error code.

decimal

start-macros
also cross 
tib-region area
dup 0= error" Needs tib-region"
dup 255 u> error" tib region should be 255 maximum"
Constant buffer-size \ maximum 255
Constant buffer-start
\ send XOFF already when 2 chars are in the buffer.
2 Constant throttle-threshold
previous 
end-macros

Label uart-rx-isr
    temp0 push,
    temp0 sreg in/lds,
    temp0 push,
    temp1 push,
    zl push,
    zh push,

    temp0 udr0 in/lds,
    temp0 3 cpi, \ Ctrl-C?
    4 $ brne,
    tosl -28 255 and ldi,
    tosh 255 ldi,
    on-error rjmp, \ initialises stacks again

    4 $:
    \ store into buffer
    ZL buffer-start lowbyte ldi,
    ZH buffer-start highbyte ldi,
    zl write-offset add,
    zh zero adc,
    temp0 stz, 

    \ increment and wrap around
    write-offset inc,
    write-offset buffer-size cpi, 
    0 $ brne,
    write-offset clr,
    0 $:

    \ if after incrementing pointers are equal, we have an overflow
    \ unfortunately, this simple approach wastes one byte of buffer space
    write-offset read-offset cp, 
    3 $ brne,
    tosl -99 255 and ldi,
    tosh 255 ldi,
    on-error rjmp, \ initialises stacks again
    3 $:

    \ debug write ofset
    \ temp0 write-offset mov,
    \ temp0 char 0 ldi,
    \ temp0 write-offset add,
    \ temp0 UDR0 out/sts,

    \ throttle
    \ This sends XOFF after threshold is reached
    \ If the transmit register is busy, we don't send it and
    \ will send it when we receive the next char. This way, 
    \ if there is concurrent output, we don't block in the ISR
    clc, \ calculate buffer size
    temp0 write-offset mov,
    temp0 read-offset sbc,
    1 $ brcc,
    clc,
    temp0 0 buffer-size - $ff and subi,
    1 $:

    temp0 throttle-threshold cpi, \ send XOFF once the threshold is reached
    2 $ brlo,
    buffer-status buffer-status and, \ skip if XOFF was send before
    2 $ brne,
    temp1 UCSR0A in/lds, \ skip if output register is busy
    temp1 5 sbrs,
    2 $ rjmp,
    temp1 $13 ldi, \ send XOFF and set flag
    temp1 UDR0 out/sts,
    buffer-status dec,
    2 $:

    zh pop,
    zl pop,
    temp1 pop,
    temp0 pop,
    temp0 sreg out/sts, \ reversed!
    temp0 pop,
    reti,
    uart-rx-isr jmp-usart_rxc jmp!
End-label+

label receive-char
    read-offset write-offset cp, \ I think we don't need cli for comparison
    receive-char breq,
    cli,
    ZL buffer-start lowbyte ldi,
    ZH buffer-start highbyte ldi,

    zl read-offset add,
    zh zero adc,
    temp1 ldz,

    read-offset inc, 
    read-offset buffer-size cpi, \ wrap around if needed
    1 $ brne,
    read-offset clr,
    1 $:

    sei,
    \ send XON, if transmit register is not empty then wait
    \ to make sure the to XON is sent
    read-offset write-offset cp,
    2 $ brne,
    buffer-status buffer-status and, \ XON only after an XOFF
    2 $ breq,
    temp1 push,
    temp1 $11 ldi,
    transmit rcall,
    temp1 pop,
    buffer-status clr,
    2 $:

    ret,
End-label+
