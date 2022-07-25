#define FASTLED_ALLOW_INTERRUPTS 0

#include <FastLED.h>
#include <RingBuf.h>
#include <CRC.h>

FASTLED_USING_NAMESPACE

#define BAUD_RATE   921600
#define DATA_PIN    3
#define LED_PIN     13
#define LED_TYPE    WS2811
#define COLOR_ORDER GRB

#define FRAMES_PER_SECOND   120
#define MAX_PAYLOAD_SIZE    255
#define MAX_PACKET_SIZE     MAX_PAYLOAD_SIZE + 5

#define START_MARKER 0xFC

#define COMMAND_SET_LED             0x01
#define COMMAND_SET_LED_CHUNK       0x02
#define COMMAND_SET_BRIGHTNESS      0x03
#define BUFFER_SIZE                 255
#define NUM_LEDS                    144

CRGB leds[NUM_LEDS];
RingBuf<byte, BUFFER_SIZE> in_buffer;

void setup() {
	Serial.begin(BAUD_RATE);

	pinMode(LED_PIN, OUTPUT);

	FastLED.addLeds<LED_TYPE, DATA_PIN, COLOR_ORDER>(leds, NUM_LEDS).setCorrection(TypicalLEDStrip);
	FastLED.setBrightness(255);

	while (!Serial) {}
}

bool processCommand(const byte command, const byte* payload, byte payload_size)
{
	switch (command)
	{
	case COMMAND_SET_LED:
		if (payload_size != 4)
		{
			break;
		}

		byte led = payload[0];
		byte r = payload[1];
		byte g = payload[2];
		byte b = payload[3];

		if (led < 0 || led >= NUM_LEDS) {
			break;
		}

		leds[led] = CRGB(r, g, b);
		return true;

	case COMMAND_SET_LED_CHUNK:
		constexpr int payload_offset = 2;
		constexpr int chunk_size = 19;

		byte start = payload[0];
		byte end = payload[1];

		if (end >= NUM_LEDS
			|| start <= 0
			|| start > end
			|| end - start > chunk_size) {
			break;
		}

		if (payload_size != (end - start) * sizeof(CRGB) + payload_offset) {
			break;
		}

		for (int i = start; i < end; i++) {
			leds[i] = CRGB(
				payload[i * sizeof(CRGB) + payload_offset + 0],
				payload[i * sizeof(CRGB) + payload_offset + 1],
				payload[i * sizeof(CRGB) + payload_offset + 2]
			);
		}
		return true;

	case COMMAND_SET_BRIGHTNESS:
		if (payload_size != 1)
		{
			break;
		}

		byte brightness = payload[0];
		FastLED.setBrightness(brightness);
		return true;
	}

	return false;
}

void loop() {
	while (Serial.available()) {
		byte serial_buffer[SERIAL_RX_BUFFER_SIZE];
		const int read = Serial.readBytes(serial_buffer, Serial.available());

		bool cancel = false;
		for (int i = 0; i < read; i++) {
			if (in_buffer.isFull()) {
				Serial.println("BUFFER OVERFLOW");
				// Buffer overflow, discard incoming data
				cancel = true;
				break;
			}

			in_buffer.push(serial_buffer[i]);
		}

		if (cancel) {
			break;
		}
	}

	byte in;

	static byte payload_size = 0;
	static byte packet_idx = 0;
	static byte packet[MAX_PACKET_SIZE];

	static bool update_frame = false;

	while (!in_buffer.isEmpty()) {
		in_buffer.pop(in);

		if (packet_idx == 0) {
			if (in != START_MARKER) {

				// Discard
				packet_idx = 0;
				continue;
			}
		}

		if (packet_idx == 1) {
			const byte command = in;

			if (command < COMMAND_SET_LED || command > COMMAND_SET_BRIGHTNESS) {
				packet_idx = 0;
				in_buffer.push(in);

				continue;
			}
		}

		if (packet_idx == 2) {
			payload_size = in;

			if (payload_size < 0 || payload_size > MAX_PAYLOAD_SIZE) {
				packet_idx = 0;
				payload_size = 0;

				in_buffer.push(in);

				continue;
			}
		}

		if (packet_idx == 3) {
			const byte header_crc = in;
			const byte expected_header_crc = crc8(packet, 3);

			if (header_crc != expected_header_crc) {

				packet_idx = 0;
				in_buffer.push(in);

				continue;
			}
		}

		packet[packet_idx++] = in;

		const byte header_size = MAX_PACKET_SIZE - MAX_PAYLOAD_SIZE - 1;
		if (packet_idx == payload_size + header_size + 1) {
			byte payload[MAX_PAYLOAD_SIZE];
			memcpy(payload, packet + header_size, payload_size);

			const byte payload_crc = in;
			const byte expected_payload_crc = crc8(payload, payload_size);

			if (payload_crc == expected_payload_crc) {
				if (processCommand(packet[1], payload, payload_size)) {
					update_frame = true;
				}
			}

			packet_idx = 0;
			payload_size = -1;
		}
	}

	EVERY_N_MILLISECONDS(1000 / FRAMES_PER_SECOND)
	{
		if (update_frame) {
			FastLED.show();
			while (Serial.available()) { Serial.read(); }
			update_frame = false;
		}
	}
}
