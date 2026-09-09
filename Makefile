.PHONY: all clean doc forth2012-report sim debug elf

IMAGE ?= output/ec4th-arduino-nano-regular
MCU ?= atmega328p
F_CPU ?= 16000000

SYM ?= $(IMAGE).sym
OUT ?= doc/forth2012-core-wordset-coverage.md

# Host-side simavr front end, needs libsimavr-dev.
#
# simavr.pc declares "Requires.private: libelf" but libsimavr-dev does not
# depend on libelf-dev, so pkg-config commonly fails on an otherwise complete
# installation. Fall back to the standard include path when it does, rather
# than compiling with no -I at all. The parts headers include each other
# unqualified, which is why simavr's own include directory has to be on the
# path and not just /usr/include.
SIM_SRC := tools/sim/ec4th-sim.c
SIM_BIN := output/ec4th-sim
SIM_CFLAGS ?= $(shell pkg-config --cflags simavr 2>/dev/null)
ifeq ($(strip $(SIM_CFLAGS)),)
SIM_CFLAGS := -I/usr/include/simavr
endif
SIM_LIBS ?= -lsimavr -lsimavrparts -lpthread -lutil

all:
	bash build.sh

forth2012-report:
	python3 tools/forth2012_wordset_report.py "$(SYM)" "$(OUT)"

doc:
	PYTHONPYCACHEPREFIX=output/pycache python3 doc/build_word_docs.py
	PYTHONPYCACHEPREFIX=output/pycache sphinx-build -d output/doc-doctrees -b html -c doc output/doc output/doc-html

$(SIM_BIN): $(SIM_SRC)
	@mkdir -p output
	@$(CC) -O2 -Wall $(SIM_CFLAGS) -o $@ $< $(SIM_LIBS) || { \
	  echo; \
	  echo "Could not build $@."; \
	  echo "It needs the simavr headers and the static parts library:"; \
	  echo "    sudo apt-get install libsimavr-dev"; \
	  exit 1; \
	}

# ELF with the Forth words as symbols, for avr-gdb. build.sh only emits the
# raw flash image and the cross compiler's own symbol table.
elf: $(IMAGE).elf
$(IMAGE).elf: $(IMAGE).bin $(IMAGE).sym
	python3 tools/sim/mkelf.py $(IMAGE).bin $(IMAGE).sym $@

# Interactive Forth prompt on the simulated USART0. Quit with Ctrl-C.
sim: all $(SIM_BIN)
	$(SIM_BIN) -m $(MCU) -f $(F_CPU) $(IMAGE).bin

# Same, but stopped at reset waiting for avr-gdb on port 1234.
debug: all $(SIM_BIN)
	$(MAKE) $(IMAGE).elf
	@echo "connect from another terminal with:"
	@echo "    avr-gdb $(IMAGE).elf -x tools/sim/ec4th-gdb.py -ex 'target remote :1234'"
	$(SIM_BIN) -m $(MCU) -f $(F_CPU) -g $(IMAGE).elf

clean:
	rm -rf output
