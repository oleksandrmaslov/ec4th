
Start-macros
\ make constants available for assembler
include avr/constants.fs
End-macros

include avr/macros.fs
include avr/vectors328.fs

decimal
Label init-ip    	0 ,
Label error-ip		0 ,
Label init-ramend return-stack-init addr>data ,
Label init-dataStack data-stack-init addr>data ,

Label init-system
    here jmp-reset jmp!
end-label+

include avr/usart-init.fs
include avr/timer0-ticks.fs
include avr/prim.fs
include avr/muldiv.fs
include avr/arduino-library.fs
include avr/f83search.fs
include avr/parse-word.fs
include avr/string-prims.fs
