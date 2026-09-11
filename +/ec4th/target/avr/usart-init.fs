\ Initialise USART0 to 115k at 16MHz clock for Arduino 10mar26jaw

\ This is straight line code to be included in the device startup sequence

\ https://ww1.microchip.com/downloads/en/DeviceDoc/Atmel-7810-Automotive-Microcontrollers-ATmega328P_Datasheet.pdf#page=149

decimal

Label usart-init
	read-offset clr,
	write-offset clr,
	buffer-status clr,

    \ set Power Reduction Register / not needed all UART is powered by default
    \ temp1 PRR in/lds,
    \ r16 %11110001 ldi, r16 temp1 and, r16 PRR out/sts,

    \ UCSR0A — USART Control and Status Register A
    \ Bit  Name    Function
    \ ------------------------------------------------------------
    \ 7    RXC0    Receive Complete
    \              1 = unread data available in UDR0
    \              cleared when UDR0 is read
    \ 6    TXC0    Transmit Complete
    \              1 = entire frame transmitted (including stop bit)
    \              cleared by writing 1 to TXC0 or by writing UDR0
    \ 5    UDRE0   USART Data Register Empty
    \              1 = UDR0 ready for new transmit byte
    \ 4    FE0     Frame Error
    \              set if stop bit of received frame was incorrect
    \ 3    DOR0    Data OverRun
    \              set if new data arrived before previous byte was read
    \ 2    UPE0    USART Parity Error
    \              set if received parity does not match configuration
    \ 1    U2X0    Double Speed Mode
    \              0 = normal speed (16x oversampling)
    \              1 = double speed (8x oversampling)
    \ 0    MPCM0   Multi-processor Communication Mode
    \              when set, only address frames are accepted
    \ enable double speed mode to increase accuracy for 115k
    r16 %00000010 ldi, r16 UCSR0A out/sts,

    \ UCSR0B — USART Control and Status Register B
    \ Bit  Name    Function
    \ ------------------------------------------------------------
    \ 7    RXCIE0  Receive Complete Interrupt Enable
    \              1 = interrupt when RXC0 is set (byte received)
    \ 6    TXCIE0  Transmit Complete Interrupt Enable
    \              1 = interrupt when TXC0 is set (frame finished)
    \ 5    UDRIE0  USART Data Register Empty Interrupt Enable
    \              1 = interrupt when UDRE0 is set (ready to send)
    \ 4    RXEN0   Receiver Enable
    \              1 = enable UART receiver
    \ 3    TXEN0   Transmitter Enable
    \              1 = enable UART transmitter
    \ 2    UCSZ02  Character Size Bit 2
    \              Used together with UCSZ01 and UCSZ00
    \ 1    RXB80   Receive Data Bit 8
    \              Used only in 9-bit character mode
    \ 0    TXB80   Transmit Data Bit 8
    \              Used only in 9-bit character mode
    \ Enable transmit and receive mode and receive interrupt via Usart 0
    r16 %10011000 ldi, r16 UCSR0B out/sts,

    \ UCSR0C — USART Control and Status Register C
    \ Bit  Name     Function
    \ ------------------------------------------------------------
    \ 7    UMSEL01  USART Mode Select bit 1
    \ 6    UMSEL00  USART Mode Select bit 0
    \               00 = Asynchronous USART
    \               01 = Synchronous USART
    \               10 = Reserved
    \               11 = Master SPI mode
    \ 5    UPM01    Parity Mode bit 1
    \ 4    UPM00    Parity Mode bit 0
    \               00 = Disabled
    \               01 = Reserved
    \               10 = Even parity
    \               11 = Odd parity
    \ 3    USBS0    Stop Bit Select
    \               0 = 1 stop bit
    \               1 = 2 stop bits
    \ 2    UCSZ01   Character Size bit 1
    \ 1    UCSZ00   Character Size bit 0
    \               Together with UCSZ02 (in UCSR0B):
    \               UCSZ02 UCSZ01 UCSZ00
    \               0      0      0  = 5-bit
    \               0      0      1  = 6-bit
    \               0      1      0  = 7-bit
    \               0      1      1  = 8-bit
    \               1      1      1  = 9-bit
    \ 0    UCPOL0   Clock Polarity (synchronous mode only)
    \               0 = data changed on rising, sampled on falling edge
    \               1 = data changed on falling, sampled on rising edge
    \ Set 8N1
    r16 %00000110 ldi, r16 UCSR0C out/sts,
    \ normal mode:       UBRR = (F_CPU / (16 * BAUD)) - 1
    \ double speed mode: UBRR = (F_CPU / ( 8 * BAUD)) - 1
    \ set baud rate to 115k for 16MHz
    16000000 8 115000 * / 1-
    dup highbyte temp0 swap ldi,
    temp0 UBRR0H out/sts,
    lowbyte temp0 swap ldi,
    temp0 UBRR0L out/sts,
    \ make sure interrupts are enabled in general
    sei,
end-label+
