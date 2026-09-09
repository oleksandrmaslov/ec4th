#!/usr/bin/env python3
"""Build an ELF image with debug symbols from a raw flash image + symbol table.

build.sh produces only .bin and .hex, which are enough to run but leave avr-gdb
disassembling anonymous addresses.  This turns the flash image into an ELF and
attaches every Forth word from the .sym file that build.fs writes, so that gdb
and objdump can name them.

The cross compiler places the ROM dictionary at $8000 in the Forth address space
(see the rom-dictionary region in build.fs) while flash itself starts at zero,
so symbol addresses are shifted down by that base.  The base is detected from
the symbol table unless one is given explicitly.

usage: mkelf.py <image.bin> <image.sym> <out.elf> [rom-base]
"""

import os
import shutil
import subprocess
import sys
import tempfile

OBJCOPY = os.environ.get("OBJCOPY", "avr-objcopy")


def read_symbols(path):
    """Parse the 'AAAAAAAA:name' lines written by the cross compiler."""
    symbols = []
    with open(path, encoding="utf-8", errors="replace") as fp:
        for line in fp:
            line = line.rstrip("\n")
            addr, sep, name = line.partition(":")
            if not sep or not name:
                continue
            try:
                symbols.append((int(addr, 16), name))
            except ValueError:
                continue
    return symbols


def main(argv):
    if not 4 <= len(argv) <= 5:
        sys.exit(__doc__.strip().splitlines()[-1])
    bin_path, sym_path, elf_path = argv[1:4]

    symbols = read_symbols(sym_path)
    if not symbols:
        sys.exit(f"mkelf.py: no symbols found in {sym_path}")

    if len(argv) == 5:
        rom_base = int(argv[4], 0)
    else:
        rom_base = 0x8000 if min(a for a, _ in symbols) >= 0x8000 else 0

    flash_size = os.path.getsize(bin_path)

    add_args, renames = [], []
    skipped = 0
    for index, (addr, name) in enumerate(symbols):
        offset = addr - rom_base
        if not 0 <= offset < flash_size:
            skipped += 1
            continue
        # '=' is --add-symbol's own separator and cannot be escaped, so those
        # names go in under a placeholder and are renamed afterwards.
        # --redefine-syms splits on whitespace, which no Forth word contains.
        if "=" in name:
            placeholder = f"ec4th_sym_{index}"
            add_args.append(f"--add-symbol={placeholder}=.text:0x{offset:x}")
            renames.append(f"{placeholder} {name}")
        else:
            add_args.append(f"--add-symbol={name}=.text:0x{offset:x}")

    with tempfile.TemporaryDirectory() as tmp:
        base_elf = os.path.join(tmp, "base.elf")
        sym_elf = os.path.join(tmp, "sym.elf")

        # -I binary yields a single .data section; rename it to a loadable,
        # executable .text at address 0 so gdb disassembles it as code.
        # -w -N drops the _binary_<path>_start/_end/_size symbols objcopy
        # invents for the input file; they sit at address 0 and would otherwise
        # shadow every unnamed address in gdb's output.
        run([OBJCOPY, "-I", "binary", "-O", "elf32-avr", "-B", "avr",
             "--rename-section",
             ".data=.text,contents,alloc,load,readonly,code",
             "-w", "-N", "_binary_*",
             bin_path, base_elf])
        run([OBJCOPY, *add_args, base_elf, sym_elf])

        if renames:
            rename_file = os.path.join(tmp, "renames")
            with open(rename_file, "w", encoding="utf-8") as fp:
                fp.write("\n".join(renames) + "\n")
            run([OBJCOPY, f"--redefine-syms={rename_file}", sym_elf, elf_path])
        else:
            shutil.copyfile(sym_elf, elf_path)

    note = f", {skipped} outside flash" if skipped else ""
    print(f"mkelf.py: {elf_path} "
          f"({len(add_args)} symbols, ROM base 0x{rom_base:x}{note})")


def run(cmd):
    try:
        subprocess.run(cmd, check=True)
    except FileNotFoundError:
        sys.exit(f"mkelf.py: {cmd[0]} not found, install binutils-avr")
    except subprocess.CalledProcessError as exc:
        sys.exit(f"mkelf.py: {cmd[0]} failed with status {exc.returncode}")


if __name__ == "__main__":
    main(sys.argv)
