"""gdb helpers for debugging ec4th under simavr.

    avr-gdb output/ec4th-arduino-nano-regular.elf -x tools/sim/ec4th-gdb.py

Forth word names go through gdb's C expression parser badly.  Some need quoting
(`break *(':docol')`), and short ones such as `0=` are rejected outright as
invalid character constants.  These commands look names up in the cross
compiler's own .sym file instead, so every word works regardless of spelling:

    fbreak dup      break on a word's machine code
    faddr 0=        show a word's addresses
    fsym $pc        name the word an address falls in
    fsymfile <path> use a different .sym file

They also apply the two offsets that make hand-written breakpoints miss: the
$8000 ROM base, and the four bytes of code field and filler in front of a
dictionary entry's body.  See SIMULATOR.md.
"""

import glob
import os

import gdb

# Assembler labels rather than dictionary entries: real code at the address
# given, with no code field in front of them.
LABEL_PREFIX = ":"
BODY_OFFSET = 4


class SymbolTable:
    def __init__(self):
        self.path = None
        self.words = {}
        self.sorted = []

    def load(self, path=None):
        if path is None:
            candidates = sorted(glob.glob("output/*.sym"))
            if not candidates:
                raise gdb.GdbError(
                    "no output/*.sym found; run make, or use fsymfile <path>")
            path = candidates[0]
        if not os.path.exists(path):
            raise gdb.GdbError(f"{path}: no such file")

        words = {}
        with open(path, encoding="utf-8", errors="replace") as fp:
            for line in fp:
                addr, sep, name = line.rstrip("\n").partition(":")
                if not sep or not name:
                    continue
                try:
                    words[name] = int(addr, 16)
                except ValueError:
                    continue
        if not words:
            raise gdb.GdbError(f"{path}: no symbols")

        # Flash starts at zero, the ROM dictionary at $8000. Same rule as
        # tools/sim/mkelf.py.
        base = 0x8000 if min(words.values()) >= 0x8000 else 0
        self.path = path
        self.words = {name: addr - base for name, addr in words.items()}
        self.sorted = sorted((a, n) for n, a in self.words.items())
        return path

    def ensure(self):
        if not self.words:
            self.load()

    def lookup(self, name):
        self.ensure()
        if name not in self.words:
            raise gdb.GdbError(f"no Forth word named {name!r} in {self.path}")
        return self.words[name]

    def body(self, name):
        """Address of the code a word actually executes."""
        addr = self.lookup(name)
        return addr if name.startswith(LABEL_PREFIX) else addr + BODY_OFFSET

    def nearest(self, addr):
        self.ensure()
        best = None
        for start, name in self.sorted:
            if start > addr:
                break
            best = (start, name)
        return best


TABLE = SymbolTable()


class FSymFile(gdb.Command):
    """fsymfile <path> -- read Forth word addresses from this .sym file."""

    def __init__(self):
        super().__init__("fsymfile", gdb.COMMAND_FILES)

    def invoke(self, arg, from_tty):
        path = TABLE.load(arg.strip() or None)
        print(f"{len(TABLE.words)} words from {path}")


class FAddr(gdb.Command):
    """faddr <word> -- show the addresses of a Forth word."""

    def __init__(self):
        super().__init__("faddr", gdb.COMMAND_DATA)

    def invoke(self, arg, from_tty):
        name = arg.strip()
        if not name:
            raise gdb.GdbError("usage: faddr <word>")
        addr = TABLE.lookup(name)
        if name.startswith(LABEL_PREFIX):
            print(f"{name}: 0x{addr:x} (assembler label, code starts here)")
        else:
            print(f"{name}: code field 0x{addr:x}, body 0x{addr + BODY_OFFSET:x}")


class FBreak(gdb.Command):
    """fbreak <word> -- break on a Forth word's machine code.

    Only primitives are machine code. A colon definition's body is a list of
    addresses that the inner interpreter reads, so the PC never reaches it;
    break on ':docol' to catch colon words being entered instead.
    """

    def __init__(self):
        super().__init__("fbreak", gdb.COMMAND_BREAKPOINTS)

    def invoke(self, arg, from_tty):
        name = arg.strip()
        if not name:
            raise gdb.GdbError("usage: fbreak <word>")
        addr = TABLE.body(name)
        gdb.Breakpoint(f"*0x{addr:x}")
        print(f"breakpoint on {name} at 0x{addr:x}")


class FSym(gdb.Command):
    """fsym [expr] -- name the Forth word an address falls in (default $pc)."""

    def __init__(self):
        super().__init__("fsym", gdb.COMMAND_DATA)

    def invoke(self, arg, from_tty):
        expr = arg.strip() or "$pc"
        addr = int(gdb.parse_and_eval(expr)) & 0xFFFFFFFF
        hit = TABLE.nearest(addr)
        if hit is None:
            print(f"0x{addr:x}: below the first word")
            return
        start, name = hit
        offset = addr - start
        print(f"0x{addr:x}: {name}" + (f" + {offset}" if offset else ""))


FSymFile()
FAddr()
FBreak()
FSym()
