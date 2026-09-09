/*
 * ec4th-sim -- interactive simavr front end for ec4th
 *
 * The stock `simavr` command line tool sets AVR_UART_FLAG_STDIO, which copies
 * UART output to the console but never feeds anything back into the UART
 * receiver.  That is enough to watch the boot banner scroll by and nothing
 * more: the Forth interpreter cannot be talked to.
 *
 * This runner wires simulated USART0 to stdin and stdout instead, so the
 * simulator behaves like a board on a serial line.  That gives an interactive
 * REPL in one terminal, and makes scripted runs work too:
 *
 *     echo '1 2 + .' | ec4th-sim output/ec4th-arduino-nano-regular.bin
 *
 * With -p the UART is exposed as a pty instead, for use with tio or screen.
 */

#include <poll.h>
#include <signal.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <termios.h>
#include <unistd.h>

#include <simavr/avr_uart.h>
#include <simavr/parts/uart_pty.h>
#include <simavr/sim_avr.h>
#include <simavr/sim_elf.h>
#include <simavr/sim_gdb.h>
#include <simavr/sim_hex.h>
#include <simavr/sim_io.h>
#include <simavr/sim_irq.h>

static const char usage_text[] =
"usage: ec4th-sim [options] <firmware.bin|.hex|.elf>\n"
"\n"
"  -m <mcu>   MCU type (default atmega328p)\n"
"  -f <hz>    CPU frequency (default 16000000)\n"
"  -d <ms>    extra delay between input characters, in simulated\n"
"             milliseconds, like tio's -o option (default 0; ec4th's own\n"
"             XON/XOFF is honoured, so this is rarely needed)\n"
"  -q <ms>    quiet time after stdin EOF before exiting (default 200)\n"
"  -g[port]   start a gdb server and wait for avr-gdb (default port 1234)\n"
"  -p         expose USART0 as a pty instead of using stdin/stdout\n"
"  -W         send input immediately, without waiting for the boot banner\n"
"  -X         pass XON/XOFF through to stdout instead of acting on them\n"
"  -v         report firmware load details and exit reason on stderr\n";

#define ASCII_XON  0x11
#define ASCII_XOFF 0x13

static avr_t *avr;

/* Flow control.  Two independent mechanisms have to be obeyed:
 * uart_xon comes from simavr's own receive fifo, and firmware_xoff comes from
 * ec4th itself, which sends XOFF once two characters are sitting in its ring
 * buffer and XON again when it has drained them.  Honouring the latter is what
 * makes pasting a whole source file into the simulator lossless, where a real
 * serial link needs tio's -o 1 to slow the sender down. */
static int uart_xon = 1;
static int firmware_xoff;
static int raw_flow_control;	/* -X: show XON/XOFF instead of acting on them */

/* Cycle of the most recent UART output byte, used to detect that the firmware
 * has gone quiet after stdin reached EOF. */
static avr_cycle_count_t last_output_cycle;

static struct termios saved_tio;
static int tio_saved;

static void
restore_tty(void)
{
	if (tio_saved) {
		tcsetattr(STDIN_FILENO, TCSANOW, &saved_tio);
		tio_saved = 0;
	}
}

static void
on_signal(int sig)
{
	restore_tty();
	(void)!write(STDOUT_FILENO, "\n", 1);
	_exit(sig == SIGINT ? 0 : 1);
}

static void
uart_output_hook(struct avr_irq_t *irq, uint32_t value, void *param)
{
	uint8_t byte = value;

	(void)irq;
	(void)param;
	last_output_cycle = avr->cycle;
	if (!raw_flow_control && (byte == ASCII_XON || byte == ASCII_XOFF)) {
		firmware_xoff = (byte == ASCII_XOFF);
		return;
	}
	(void)!write(STDOUT_FILENO, &byte, 1);
}

static void
uart_xon_hook(struct avr_irq_t *irq, uint32_t value, void *param)
{
	(void)irq; (void)value; (void)param;
	uart_xon = 1;
}

static void
uart_xoff_hook(struct avr_irq_t *irq, uint32_t value, void *param)
{
	(void)irq; (void)value; (void)param;
	uart_xon = 0;
}

/* A negative delay would wrap when scaled to unsigned cycle counts. */
static int
max0(int value)
{
	return value < 0 ? 0 : value;
}

static int
has_suffix(const char *s, const char *suffix)
{
	size_t ls = strlen(s), lx = strlen(suffix);

	return ls > lx && !strcasecmp(s + ls - lx, suffix);
}

/*
 * Loads .bin (raw flash image, as produced by build.sh), .hex or .elf.
 * Returns the number of bytes placed in flash, or -1.
 */
static long
load_firmware(const char *path, int verbose)
{
	uint32_t flash_size = avr->flashend + 1;
	long loaded = -1;

	if (has_suffix(path, ".elf")) {
		elf_firmware_t fw;

		memset(&fw, 0, sizeof(fw));

		if (elf_read_firmware(path, &fw) != 0) {
			fprintf(stderr, "ec4th-sim: %s: cannot read ELF\n", path);
			return -1;
		}
		avr_load_firmware(avr, &fw);
		loaded = avr->codeend;
	} else if (has_suffix(path, ".hex")) {
		ihex_chunk_p chunks = NULL;
		int count = read_ihex_chunks(path, &chunks);
		uint32_t top = 0;

		if (count <= 0) {
			fprintf(stderr, "ec4th-sim: %s: no usable ihex records\n", path);
			return -1;
		}
		for (int i = 0; i < count; i++) {
			uint32_t base = chunks[i].baseaddr, size = chunks[i].size;

			if (base + size > flash_size) {
				fprintf(stderr, "ec4th-sim: %s: skipping chunk at "
				    "0x%08x (%u bytes), outside flash\n", path, base, size);
				continue;
			}
			memcpy(avr->flash + base, chunks[i].data, size);
			if (base + size > top)
				top = base + size;
		}
		free_ihex_chunks(chunks);
		avr->codeend = top;
		loaded = top;
	} else {
		FILE *fp = fopen(path, "rb");
		size_t got;

		if (!fp) {
			perror(path);
			return -1;
		}
		got = fread(avr->flash, 1, flash_size, fp);
		if (ferror(fp)) {
			perror(path);
			fclose(fp);
			return -1;
		}
		fclose(fp);
		avr->codeend = got;
		loaded = got;
	}

	if (loaded <= 0) {
		fprintf(stderr, "ec4th-sim: %s: nothing loaded into flash\n", path);
		return -1;
	}
	if (verbose)
		fprintf(stderr, "ec4th-sim: loaded %ld bytes of flash from %s\n",
		    loaded, path);
	return loaded;
}

/* Non-blocking readability test, so that a read() of 0 really means EOF. */
static int
stdin_ready(void)
{
	struct pollfd pfd = { .fd = STDIN_FILENO, .events = POLLIN };

	return poll(&pfd, 1, 0) > 0 && (pfd.revents & (POLLIN | POLLHUP));
}

int
main(int argc, char *argv[])
{
	const char *mcu = "atmega328p";
	const char *firmware = NULL;
	uint32_t frequency = 16000000;
	int char_delay_ms = 0;
	int quiet_ms = 200;
	int gdb_port = 0;
	int use_pty = 0;
	int ignore_boot_banner = 0;
	int verbose = 0;
	int opt;

	while ((opt = getopt(argc, argv, "m:f:d:q:g::pWXvh")) != -1) {
		switch (opt) {
		case 'm': mcu = optarg; break;
		case 'f': frequency = strtoul(optarg, NULL, 0); break;
		case 'd': char_delay_ms = max0(atoi(optarg)); break;
		case 'q': quiet_ms = max0(atoi(optarg)); break;
		case 'g': gdb_port = optarg ? atoi(optarg) : 1234; break;
		case 'p': use_pty = 1; break;
		case 'W': ignore_boot_banner = 1; break;
		case 'X': raw_flow_control = 1; break;
		case 'v': verbose = 1; break;
		default:
			fputs(usage_text, opt == 'h' ? stdout : stderr);
			return opt == 'h' ? 0 : 2;
		}
	}
	if (optind >= argc) {
		fputs(usage_text, stderr);
		return 2;
	}
	firmware = argv[optind];

	/* UART output goes out through write(2) while simavr's own parts report
	 * things such as the pty name through printf; unbuffered stdout keeps the
	 * two in order and makes messages survive an exit from a signal handler. */
	setvbuf(stdout, NULL, _IONBF, 0);

	avr = avr_make_mcu_by_name(mcu);
	if (!avr) {
		fprintf(stderr, "ec4th-sim: unknown MCU '%s'\n", mcu);
		return 1;
	}
	avr_init(avr);
	if (load_firmware(firmware, verbose) < 0)
		return 1;
	/* After loading: an ELF without an .mmcu section would otherwise leave
	 * the frequency at zero, which breaks all UART baud rate maths. */
	avr->frequency = frequency;

	if (gdb_port) {
		avr->gdb_port = gdb_port;
		avr->state = cpu_Stopped;
		avr_gdb_init(avr);
		fprintf(stderr, "ec4th-sim: waiting for avr-gdb on port %d\n", gdb_port);
	}

	if (use_pty) {
		static uart_pty_t pty;

		uart_pty_init(avr, &pty);
		uart_pty_connect(&pty, '0');
	} else {
		avr_irq_t *out = avr_io_getirq(avr, AVR_IOCTL_UART_GETIRQ('0'),
		    UART_IRQ_OUTPUT);
		avr_irq_t *xon = avr_io_getirq(avr, AVR_IOCTL_UART_GETIRQ('0'),
		    UART_IRQ_OUT_XON);
		avr_irq_t *xoff = avr_io_getirq(avr, AVR_IOCTL_UART_GETIRQ('0'),
		    UART_IRQ_OUT_XOFF);

		if (!out || !xon || !xoff) {
			fprintf(stderr, "ec4th-sim: %s has no USART0\n", mcu);
			return 1;
		}
		avr_irq_register_notify(out, uart_output_hook, NULL);
		avr_irq_register_notify(xon, uart_xon_hook, NULL);
		avr_irq_register_notify(xoff, uart_xoff_hook, NULL);

		/* The megax8 cores enable AVR_UART_FLAG_STDIO, which makes simavr
		 * echo every transmitted byte to stderr as well.  We render the
		 * output ourselves, so turn that copy off. */
		uint32_t flags = 0;
		avr_ioctl(avr, AVR_IOCTL_UART_GET_FLAGS('0'), &flags);
		flags &= ~AVR_UART_FLAG_STDIO;
		avr_ioctl(avr, AVR_IOCTL_UART_SET_FLAGS('0'), &flags);
	}

	avr_irq_t *uart_in = avr_io_getirq(avr, AVR_IOCTL_UART_GETIRQ('0'),
	    UART_IRQ_INPUT);

	if (!use_pty) {
		if (isatty(STDIN_FILENO)) {
			struct termios raw;

			tcgetattr(STDIN_FILENO, &saved_tio);
			tio_saved = 1;
			atexit(restore_tty);
			raw = saved_tio;
			/* Full raw mode: ec4th echoes characters itself and emits
			 * CR+LF for `cr`, so any local echo or newline translation
			 * would double up. ISIG stays on so ^C still quits. */
			cfmakeraw(&raw);
			raw.c_lflag |= ISIG;
			raw.c_cc[VMIN] = 0;
			raw.c_cc[VTIME] = 0;
			tcsetattr(STDIN_FILENO, TCSANOW, &raw);
		}
	}
	signal(SIGINT, on_signal);
	signal(SIGTERM, on_signal);

	const avr_cycle_count_t poll_interval = 4096;
	const avr_cycle_count_t char_delay =
	    (avr_cycle_count_t)char_delay_ms * (frequency / 1000);
	const avr_cycle_count_t quiet_cycles =
	    (avr_cycle_count_t)quiet_ms * (frequency / 1000);

	avr_cycle_count_t next_poll = 0;
	avr_cycle_count_t next_input_cycle = 0;
	int pending = -1;		/* byte read from stdin, not yet delivered */
	int stdin_eof = 0;
	int state = cpu_Running;

	while (state != cpu_Done && state != cpu_Crashed) {
		state = avr_run(avr);

		if (use_pty || avr->cycle < next_poll)
			continue;
		next_poll = avr->cycle + poll_interval;

		if (pending < 0 && !stdin_eof && stdin_ready()) {
			uint8_t byte;
			ssize_t n = read(STDIN_FILENO, &byte, 1);

			if (n == 1)
				pending = byte;
			else if (n == 0)
				stdin_eof = 1;
		}
		/* Hold input back until the board has said something.  Feeding
		 * the receiver while the firmware is still in its reset code and
		 * has not set RXEN yet simply loses the leading characters. */
		if (pending >= 0 && last_output_cycle == 0 && !ignore_boot_banner)
			continue;
		if (pending >= 0 && uart_xon && !firmware_xoff &&
		    avr->cycle >= next_input_cycle) {
			avr_raise_irq(uart_in, pending);
			pending = -1;
			next_input_cycle = avr->cycle + char_delay;
		}
		/* A piped script has run out and the board has stopped talking.
		 * Under gdb the session outlives its input, so never quit there. */
		if (!gdb_port && stdin_eof && pending < 0 &&
		    avr->cycle - last_output_cycle > quiet_cycles)
			break;
	}

	restore_tty();
	if (verbose)
		fprintf(stderr, "\nec4th-sim: stopped in state %d after %"
		    PRI_avr_cycle_count " cycles\n", state, avr->cycle);
	avr_terminate(avr);
	return state == cpu_Crashed ? 1 : 0;
}
