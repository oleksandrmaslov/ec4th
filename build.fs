\ builds the image for the Arduino Nano, will be refactored so its
\ easier to build with different features and for other targets


Create mach-file ," +/ec4th/target/avr/mach-common.fs"

s" output/ec4th-arduino-nano-regular.sym" r/w create-file throw value fd-symbol-table

include +/ec4th/cross/cross.fs

unlock

0 Constant rom-at-0000

rom-at-0000 [IF]
\ FIXME: why 7ff?
$c100 $07ff region ram-dictionary
$0000 $ffff region rom-dictionary
[ELSE]
$0100 $0800 region ram-dictionary
$8000 $8000 region rom-dictionary
[THEN]

lock e? stack-grows-upwards unlock
[IF]

\ both stacks share this region. The stack checks in ?stack and :docol keep
\ 62 and 24 bytes free, $A0 leaves room for about 40 data stack items
ram-dictionary $A0 steal-from-end region return-stack

[ELSE]

\ return stack needs to be quite deep depending on the prmitives
ram-dictionary $40 steal-from-end region return-stack
ram-dictionary $40 steal-from-end region data-stack

[THEN]

\ FIXME: optimise tib input ring buffer
\ on Arduino this region is used for the usart input ring buffer 
\ make it longer than an input line, so one line is buffered while compiling
ram-dictionary 90 steal-from-end region tib-region

setup-target

lock

include +/ec4th/target/atmega328.fs

include +/ec4th/target/avr/io/uart.fs
include +/ec4th/target/avr/usart-ringbuffer.fs
\ include +/ec4th/target/avr/io/dot_s.fs
include +/ec4th/target/avr/io/emit_key.fs


include +/ec4th/kernel/io.fs

include +/ec4th/primitives/minimal.fs

include +/ec4th/nio/dothex.fs
include +/ec4th/debug/dump.fs

include +/ec4th/kernel/hashed-search.fs
include +/ec4th/kernel/dictionary.fs
include +/ec4th/kernel/compiler.fs
include +/ec4th/kernel/interpreter.fs
include +/ec4th/kernel/flow-control.fs
include +/ec4th/kernel/numeric-output.fs
include +/ec4th/kernel/picture-numeric-output.fs
include +/ec4th/primitives/doers.fs

\ include +/ec4th/testing/tester.fs
\ include +/ec4th/testing/test-constants.fs

\ Codes for testing the Forth-System
\ include +/gforth/arch/misc/tt.fs
\	include nqueens.fs


\ ##############################################################################
\ #############################Code#############################################
\ ##############################################################################

Decimal

>rom

[IFDEF] current 
  forth-wordlist current !
  >ram unlock tdp @ lock dup . dp !
[THEN]

has? hash-bits [IF]

forth-wordlist unlock 
copy-hash-links 
lock

[ELSE]

\ Set up last and forth-wordlist with the address of the last word's
UNLOCK tlast @ LOCK 
1 cells -
forth-wordlist !

[THEN]

include +/ec4th/boot/mirror.fs

 : boot ( 0|error -- )
  ?dup IF 
    dup -99 = IF 
      cr ." ===> Input overrun, press Ctrl-C <===" cr 
      \ this loop is expected to read faster then we receive with 115k, so
      \ we don't see repeated overrun lines
      BEGIN key drop AGAIN
    THEN
    quit-error
  ELSE 
    mirrorram
    cr ." ec4th" quit
  THEN
  bye ;

\ set start ip
 ' boot >body init-ip !

 unlock
.unresolved
.regions
fd-symbol-table close-file throw

\ dictionary dump-region

dictionary extent save-region output/ec4th-arduino-nano-regular.bin
