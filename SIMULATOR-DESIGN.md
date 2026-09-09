# How the simulator setup was built

[SIMULATOR.md](SIMULATOR.md) tells you how to *use* the simulator. This one
records how it was arrived at: what was already there, what was missing, and how
each piece was worked out. It is here so the next person to touch
`tools/sim/`, or to point ec4th at a different AVR, does not have to rediscover
the same things.

Every command and output below is real. Reproduce any of them if you doubt a
conclusion.

---

## Step 1. Establish what the stock tool actually does

`run.sh` already called simavr, so the obvious first question is what was
actually wrong with it. Build and run:

```console
$ bash build.sh
$ simavr -m atmega328p -f 16000000 output/ec4th-arduino-nano-regular.hex
ec4th ok..
```

The banner appears, so the firmware boots and transmits. Now try to talk to it:

```console
$ printf '1 2 + . cr\n' | simavr -m atmega328p -f 16000000 output/*.hex
ec4th ok..
```

Nothing. The Forth never answers.

**Conclusion: the stock `simavr` tool copies UART output to the console but
never feeds anything into the UART receiver.** It sets `AVR_UART_FLAG_STDIO`,
which is one-directional by design. It is a smoke test, not a terminal. No
combination of flags fixes this — the input path simply is not wired up.

That single finding determines everything that follows: to get a Forth prompt,
the simulator has to be driven from a program that owns the UART's input IRQ.

## Step 2. Find out whether simavr can be driven programmatically

simavr is a library (`libsimavr`) with a thin CLI on top, so the answer was
likely yes. What mattered was whether the pieces were reachable on this machine.

```console
$ dpkg -L libsimavr2 | grep -E '\.so|include'
/usr/lib/x86_64-linux-gnu/libsimavr.so.2
```

The shared library is installed but there are no headers — those live in
`libsimavr-dev`, which was not installed. Rather than guess at what it contains,
fetch and unpack it without touching the system (`apt-get download` needs no
root):

```console
$ apt-get download libsimavr-dev
$ dpkg-deb -x libsimavr-dev_*.deb ./sdev
$ find sdev -name '*.h' -o -name '*.a'
sdev/usr/include/simavr/sim_avr.h
sdev/usr/include/simavr/avr_uart.h
sdev/usr/include/simavr/parts/uart_pty.h
sdev/usr/lib/x86_64-linux-gnu/libsimavrparts.a
...
```

Two useful findings: the full core API is there, and `parts/uart_pty.h` means
simavr already ships a ready-made pty bridge. Unpacking the package this way is
also what made it possible to develop and test the whole thing before anyone
typed a `sudo` password.

## Step 3. Learn the API surface actually needed

Reading the headers rather than guessing:

| need | API |
| --- | --- |
| create and start a core | `avr_make_mcu_by_name()`, `avr_init()`, `avr_run()` |
| reach the UART | `avr_io_getirq(avr, AVR_IOCTL_UART_GETIRQ('0'), …)` |
| the four UART IRQs | `UART_IRQ_INPUT`, `UART_IRQ_OUTPUT`, `UART_IRQ_OUT_XON`, `UART_IRQ_OUT_XOFF` |
| turn off simavr's own console echo | `AVR_IOCTL_UART_SET_FLAGS('0')`, clearing `AVR_UART_FLAG_STDIO` |
| load firmware | `read_ihex_chunks()`, `elf_read_firmware()`, or `fread` into `avr->flash` |
| debugger | `avr->gdb_port`, `avr->state = cpu_Stopped`, `avr_gdb_init()` |

`avr_uart.h` even documents the intended input loop in a comment: raise
`UART_IRQ_INPUT` per byte, stop while XOFF is asserted, resume on XON. That is
the shape [tools/sim/ec4th-sim.c](tools/sim/ec4th-sim.c) implements.

## Step 4. Read the firmware before writing the terminal code

The runner has to decide about local echo and newline translation, and those are
firmware properties, not preferences. Three answers came out of the sources:

* `+/ec4th/kernel/simple-accept.fs` has `Variable echo` / `echo on`, and
  `accept` does `dup emit` on every character. **ec4th echoes. Local echo must
  be off**, or everything appears twice.
* `+/ec4th/kernel/io.fs` defines `cr` as `#cr emit #lf emit`, i.e. CR **and** LF.
  **No `ONLCR` translation is needed** — full raw mode is correct.
* the same `accept` exits on `#cr` **or** `#lf`. **Unix LF-only files can be
  piped in directly**, no conversion needed.

So the terminal setup is `cfmakeraw()`, with `ISIG` put back so Ctrl-C still
quits. Guessing here would have produced doubled characters or stair-stepped
output and looked like a simulator bug.

## Step 5. First working runner, and the three bugs it exposed

The first version wired stdin/stdout to the UART IRQs. Its output:

```console
$ printf '1 2 + . cr\r' | ec4th-sim output/*.bin | hexdump -C
00000000  65 63 34 74 68 20 6f 6b  0d 0a 11 31 11 20 11 32  |ec4th ok...1. .2|
00000010  11 20 11 2b 11 20 11 2e  11 20 11 63 11 72 11 20  |. .+. ... .c.r. |
00000020  33 20 0d 0a 7e 34 6d 73  20 20 6f 6b 0d 0a        |3 ..~4ms  ok..|
```

It answered `3`, so the concept worked. Three defects were visible:

**Output appeared twice.** The green copy was going to stderr. The `megax8`
cores enable `AVR_UART_FLAG_STDIO` themselves, so simavr was printing every byte
in addition to our handler. Fixed by clearing the flag through
`AVR_IOCTL_UART_SET_FLAGS`.

**The leading `1` was lost.** Input was being pushed at the receiver from cycle
zero, while the firmware was still in its reset code and had not set `RXEN`.
Fixed by holding input back until the board has transmitted its first byte —
self-tuning, and no magic delay constant. `-W` disables it.

**A `0x11` byte before every echoed character.** This one was not a bug at all,
which brings us to the next step.

## Step 6. The `0x11` bytes: ec4th has its own flow control

`0x11` is ASCII XON. Grepping the firmware for it:

```console
$ grep -n 'XON\|XOFF' +/ec4th/target/avr/usart-ringbuffer.fs
18:\ send XOFF already when 2 chars are in the buffer.
69:    \ This sends XOFF after threshold is reached
81:    temp0 throttle-threshold cpi, \ send XOFF once if at threshold
83:    buffer-status buffer-status and, \ skip if XOFF was send before
88:    temp1 $13 ldi, \ send XOFF and set flag
121:    \ send XON, if transmit register is not empty then wait
122:    \ to make sure the to XON is sent
```

The receive ISR implements **software flow control**: XOFF (`$13`) once two
characters are sitting in its ring buffer, XON (`$11`) when it has drained them.
Those bytes were the firmware correctly telling the sender to wait.

The README tells you to run `tio -b 115200 -o 1` on real hardware because ec4th
drops characters when a file arrives faster than it compiles. The simulator can
do better than a blunt delay: watch for `$13`/`$11` in the output stream, hold
the sender while XOFF stands, and swallow the two bytes so they never reach the
console. That was implemented, and an eight-line test file pasted perfectly at
full speed.

**That test file was too small.** A 26-line one overruns anyway:

```console
$ ec4th-sim -d 0 output/*.bin < buzzer-hw.fs
: d2-low %-14b1440b ~
===> Input overrun, press Ctrl-C <===
```

The ISR says why, in a comment right above the throttle code:

```forth
\ If the transmit register is busy, we don't send it and
\ will send it when we receive the next char. This way,
\ if there is concurrent output, we don't block in the ISR
```

XOFF only goes out when the transmitter happens to be idle — and while the board
is echoing input and printing `%-6b1388b ~3ms compiled` after every line, it
rarely is. Honouring XON/XOFF genuinely helps, but it cannot be relied on as the
only mechanism.

So redirected input is paced at 1 ms per character by default, the same trick as
`tio -o 1`, while terminal input is left alone. The same file then compiles with
no flags at all. `-d 0` restores full speed, and `-X` shows the flow control
bytes instead of acting on them.

The lesson is about the test, not the code: the first conclusion was not wrong
in its mechanism, only over-generalised from a sample too small to falsify it.

## Step 7. Giving gdb something to work with

The README's debugging recipe referenced `output/avr.elf`, a file the build has
never produced — `build.sh` emits only `.bin` and `.hex`, and its ELF conversion
lines are commented out. So gdb had no symbols at all.

The flash image plus the cross compiler's `.sym` file contain everything needed:

```console
$ head -3 output/ec4th-arduino-nano-regular.sym
00008160::docol
00008170:;S
0000817A::dovar
```

Three obstacles turned up while converting that into an ELF.

**The `$8000` ROM base.** Symbols start at `$8160` but flash starts at zero.
`build.fs` explains why:

```forth
$8000 $8000 region rom-dictionary
```

The ROM dictionary lives at `$8000` in the Forth address space. Checked against
the image rather than trusted: `boot` is listed at `$B160`, and `$B160 - $8000 =
0x3160`, which is 96 bytes from the end of a 12736-byte image — plausible for
the last word defined. The string `boot` appears immediately before it. Base
confirmed; [tools/sim/mkelf.py](tools/sim/mkelf.py) detects it and applies it.

**`objcopy --add-symbol` cannot express 10 of the 407 names.** Its syntax is
`--add-symbol <name>=<section>:<value>` and it splits on the *first* `=`, so
`0=`, `<=`, `u>=`, `d0=` and friends are unparseable. Rather than drop them,
they go in under a placeholder and are renamed afterwards with
`--redefine-syms`, whose file format is whitespace-separated — and no Forth word
contains a space. All 407 make it in:

```console
$ avr-nm output/*.elf | grep -E ' (0=|u<=|d0=)$'
0000041e T 0=
00001034 T u<=
00001834 T d0=
```

**`objcopy` invents its own symbols.** `-I binary` adds
`_binary_<path>_start/_end/_size` at address 0, which then shadow every unnamed
address in gdb's output. `-w -N '_binary_*'` removes them.

(A first attempt wrote the address arithmetic in awk. Ubuntu's default `mawk`
has no `strtonum`, so hex parsing failed. The repo already ships Python tooling
in `tools/`, so it moved there.)

## Step 8. Why the first breakpoints never fired

With symbols in place, the obvious test:

```console
(gdb) break *dup
Breakpoint 1 at 0x2e2
(gdb) continue
```

`dup` ran many times. The breakpoint never fired. But breaking on `:docol`
worked immediately, so the mechanism was fine — the address was wrong.

Looking at what is actually at `dup`:

```console
$ xxd -s 0x2e2 -l 8 output/ec4th-arduino-nano-regular.bin
000002e2: e682 0000 8993 9993                      ........
```

`e6 82` little-endian is `$82E6`, which is `0x2e6` in flash — **four bytes
further on**. The symbol points at the word's *code field*, a two-byte pointer
that the inner interpreter reads; the machine code starts after it and two
filler bytes. `89 93 99 93` at `0x2e6` is `st Y+, r24 / st Y+, r25`, which is
exactly what `dup` should be.

```console
(gdb) break *(dup+4)
Breakpoint 1, 0x000002e6 in dup ()
(gdb) x/3i $pc
=> 0x2e6 <dup+4>:	st	Y+, r24
   0x2e8 <dup+6>:	st	Y+, r25
   0x2ea <dup+8>:	rjmp	.-452
```

The same layout explains why colon definitions cannot be breakpointed at all.
`mirrorram` at `$B14A` → `0x314a`:

```console
$ xxd -s 0x3138 -l 32 output/ec4th-arduino-nano-regular.bin
00003138: 0000 0000 0000 e4af 096d 6972 726f 7272  .........mirrorr
00003148: 616d 6081 0000 88b0 74b0 62b0 4c94 7081  am`.....t.b.L.p.
```

Reading it out: link field `e4 af`, counted name `09 "mirrorram"`, then the code
field at `0x314a` holding `$8160` — which is `:docol`. The body at `0x314e` is
`88 b0 74 b0 62 b0`, i.e. the addresses `$B088`, `$B074`, `$B062`. Those are
`ram-origin`, `ram-start` and `ram-len` in the symbol table. It is a list of
addresses, not instructions. **The PC never goes there**, so no address
breakpoint on a colon definition can ever hit; break on `:docol` instead.

The `:`-prefixed symbols (`:docol`, `:dovar`, `:docon`, `:dodoes`) are assembler
labels rather than dictionary entries, so they are real code at the address
given, with no `+4`.

## Step 9. gdb cannot spell some Forth words

`+4` is easy to type but does not always parse:

```console
(gdb) break *:docol
A syntax error in expression, near `:docol'.
(gdb) break *':docol'          # quoting fixes it
Breakpoint 1 at 0x160

(gdb) break *('0='+4)
Invalid character constant.
```

Short quoted names collide with C character constants, and there is no spelling
of `0=` that gdb's expression parser will accept in an arithmetic expression.
Since that is a parser limitation rather than a symbol problem, the fix is to
bypass the parser: [tools/sim/ec4th-gdb.py](tools/sim/ec4th-gdb.py) reads the
`.sym` file directly and applies both offsets itself.

```console
(gdb) fbreak 0=
breakpoint on 0= at 0x422
(gdb) continue
Breakpoint 1, 0x00000422 in 0= ()
(gdb) fsym
0x422: 0= + 4
```

`fsym` also answers "which word am I in" for an arbitrary address, which is the
question you actually have after a crash.

## Step 10. Packaging, and the trap in it

Two more defects surfaced while turning this into `make` targets.

**The simulator quit while gdb was attached.** Piped input ends at EOF, and the
runner exits once the board goes quiet — correct for scripted runs, wrong under
a debugger, where the session outlives its input. The EOF rule is now suppressed
whenever `-g` is in use.

**pty mode printed nothing.** `uart_pty` reports the pty name with `printf`,
which is block-buffered into a pipe, and the signal handler leaves via `_exit()`
without flushing. Since UART bytes are written with `write(2)` and the library
uses stdio, the two were also liable to interleave wrongly. `setvbuf(stdout,
NULL, _IONBF, 0)` fixes both.

**And the one that would have bitten you first:** `pkg-config --cflags simavr`
returns *nothing* on a correctly installed system.

```console
$ pkg-config --cflags simavr
Package libelf was not found in the pkg-config search path.
Perhaps you should add the directory containing `libelf.pc'
to the PKG_CONFIG_PATH environment variable
Package 'libelf', required by 'simavr', not found
$ echo $?
1
```

`simavr.pc` declares `Requires.private: libelf`, but:

```console
$ dpkg-deb -f libsimavr-dev_*.deb Depends
Depends: libsimavr2 (= 1.6...), libsimavrparts1 (= 1.6...)
```

`libsimavr-dev` does **not** depend on `libelf-dev`, so `libelf.pc` is usually
absent and the query fails. A Makefile that trusted it would compile with no
`-I` at all and blame the user for a missing package. The Makefile therefore
falls back to `-I/usr/include/simavr` when pkg-config yields an empty result.

That dependency line also settled how to link: `libsimavr-dev` pulls in
`libsimavrparts1`, so `-lsimavrparts` resolves at both link and run time and
there is no need to reference a static archive by path.

**Validating it without root.** The default build path could not be tested
against a real installation, so one was simulated: unpack `libsimavr-dev` and
`libsimavrparts1`, repair the dangling `libsimavr.so` symlink, and stand in for
the default search paths with `CPATH` and `LIBRARY_PATH`. Building with no
overrides at all and pkg-config deliberately broken then exercises exactly the
path a real user hits.

---

## If you retarget this

The order that worked, generalised:

1. **Prove the gap before building anything.** One `printf | simavr` established
   that the input path did not exist and that a custom runner was unavoidable.
2. **Get the headers without committing to an install.** `apt-get download` plus
   `dpkg-deb -x` lets you read the real API and build against it as a normal
   user.
3. **Read the firmware for anything the host side must agree with** — echo,
   line endings, flow control. These are facts to look up, not defaults to pick.
4. **Hexdump the image to confirm every address assumption.** The `$8000` base
   and the `+4` code field were both settled by looking at bytes, and both would
   have been wrong if guessed.
5. **Distrust packaging metadata.** pkg-config was broken in a way that produced
   a plausible-looking wrong answer rather than an error.

For a different AVR, `-m` and `-f` are the only runner changes. For a different
memory layout, `mkelf.py` and `ec4th-gdb.py` take the ROM base as an argument
and otherwise detect it; the `+4` body offset is the constant `BODY_OFFSET` in
the gdb helper and would need changing if the dictionary header changed.
