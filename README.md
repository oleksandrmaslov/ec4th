# ec4th

`ec4th` is a small Forth system that is cross-compiled with `gforth` and currently targets the ATmega328/Arduino Nano class of boards. This repository contains the cross compiler, the target kernel and primitives, board-specific code, and the documentation sources for the generated word reference.

## Quickstart

To run ec4th on an Arduino Nano without building from source, install:

- `avrdude`
- a serial terminal such as `tio`

On Ubuntu, install them with:

```bash
sudo apt-get install avrdude tio
```

Connect the board, identify the serial device, and make sure your user can access it. On Linux that is often `/dev/ttyUSB0` and usually requires membership in the `dialout` group.

Download a released HEX image from the GitHub releases page:

<https://github.com/cruftex/ec4th/releases>

The regular Arduino Nano build is published as:

- `ec4th-arduino-nano-regular.hex`

Flash the release image:

```bash
avrdude -p atmega328p -c arduino -P /dev/ttyUSB0 -b 115200 -D \
  -U flash:w:ec4th-arduino-nano-regular.hex:i
```

Open a terminal session:

```bash
tio -b 115200 -o 1 /dev/ttyUSB0
```

The `-o 1` option adds a small transmit delay. ec4th can otherwise overrun on interactive input at full serial speed.

## Building From Source

To build the firmware image you need:

- `gforth`
- `avr-objcopy` from AVR binutils

On Ubuntu/Debian this is typically:

```bash
sudo apt-get install gforth binutils-avr
```

Build the default image with:

```bash
make
```

That produces:

- `output/ec4th-arduino-nano-regular.bin`
- `output/ec4th-arduino-nano-regular.hex`
- `output/ec4th-arduino-nano-regular.sym`
- `output/ec4th-arduino-nano-regular.tags`

## Simulator and debugging

The image runs on your machine under `simavr`, with a working Forth prompt on
the simulated serial port:

```bash
sudo apt-get install simavr libsimavr-dev gdb-avr
make sim
```

`make debug` starts the same simulation halted at reset for `avr-gdb`.

See [SIMULATOR.md](SIMULATOR.md) for the full walkthrough, including how to pipe
a source file in and where breakpoints have to go in a threaded-code system.

## Repository layout

- `build.fs` builds the default Arduino Nano image.
- `build.sh` runs the cross build and converts the generated binary into Intel HEX.
- `run.sh` builds and starts the image in `simavr`.
- `tools/sim/` holds the simavr front end, the ELF generator and the gdb helpers.
- `+/ec4th/` contains the ec4th sources.
- `doc/` contains documentation sources and the word metadata used to generate the reference.
- `output/` is generated during builds and documentation generation.

## Documentation

The generated word reference is built from:

- YAML files in `doc/word/`
- tag files in `output/ec4th-*.tags`
- the custom Sphinx Forth domain in `doc/forth_domain.py`

Build the documentation with:

```bash
make doc
```

See `DOCUMENTATION.md` for the documentation conventions and generator details.
