/*
 * SSRP-FSR v1  -  raw force-sensor streamer for the SSRP Pong game
 * ------------------------------------------------------------------
 * Board   : Arduino Uno (ATmega328P, 10-bit ADC, 5 V)
 * Protocol: docs/serial-protocol.md  (115200 baud, 8N1, one ASCII line per sample)
 *
 *   t_us,p1_up,p1_down,p2_up,p2_down\n      e.g.  "1234567,512,3,0,1020\n"
 *   Lines starting with '#' are comments. Boot banner: "# SSRP-FSR v1 rate=200Hz ..."
 *
 * Wiring (one voltage divider per sensor, see docs/hardware/wiring.md):
 *
 *        5V ---[ FSR ]---+---[ 10 kOhm ]--- GND
 *                        |
 *                        +---> analog pin
 *
 *   Vout = Vcc * R_fixed / (R_fsr + R_fixed): pressing harder lowers R_fsr, so the reading rises.
 *
 *   A0 = Player 1 UP     A1 = Player 1 DOWN
 *   A2 = Player 2 UP     A3 = Player 2 DOWN
 *
 * The firmware sends RAW ADC counts (0-1023) and does NO filtering on purpose:
 * the noise (mains pickup, quantisation, drift, crosstalk) is what the project studies.
 *
 * Compile-time switches
 * ---------------------
 *   SAMPLE_RATE_HZ      Lines per second. 200 fits 115200 baud (~31 bytes/line = ~2.7 ms < 5 ms).
 *                       Drop to 100 if the PC reports gaps or truncated lines.
 *   DISCARD_FIRST_READ  1 = read each pin twice and keep the second value. The ADC's sample-and-hold
 *                       capacitor still holds charge from the previously selected channel, so the
 *                       first conversion after switching the multiplexer is pulled towards it
 *                       (channel crosstalk). Costs one extra conversion (~112 us) per channel.
 *   OVERSAMPLE          Number of conversions averaged per channel (1 = off). Averaging N reads
 *                       reduces white noise by ~sqrt(N) but is a (very mild) filter - keep 1 for
 *                       noise-analysis recordings.
 *
 * Timing: a fixed-rate loop based on micros() with unsigned subtraction, so it keeps working
 * across the 70-minute micros() overflow. No delay() is used. Each line is formatted with
 * snprintf into one buffer and sent with a single Serial.write().
 * Budget: 4 channels x 2 reads x ~112 us = ~0.9 ms of ADC time per 5 ms period.
 */

#define SAMPLE_RATE_HZ      200
#define DISCARD_FIRST_READ  1
#define OVERSAMPLE          1
#define BAUD_RATE           115200

static const uint8_t  CHANNEL_PINS[4] = { A0, A1, A2, A3 };
static const uint32_t PERIOD_US       = 1000000UL / SAMPLE_RATE_HZ;

static uint32_t nextSampleUs = 0;
static uint32_t overrunCount = 0;   // how often the loop fell behind (reported in a comment line)

static uint16_t readChannel(uint8_t pin)
{
#if DISCARD_FIRST_READ
  (void)analogRead(pin);           // settle the sample-and-hold on the new channel
#endif

#if OVERSAMPLE > 1
  uint32_t sum = 0;
  for (uint8_t i = 0; i < OVERSAMPLE; i++)
  {
    sum += analogRead(pin);
  }
  return (uint16_t)(sum / OVERSAMPLE);
#else
  return (uint16_t)analogRead(pin);
#endif
}

void setup()
{
  Serial.begin(BAUD_RATE);

  // Leonardo-style boards wait for the USB port; harmless on the Uno.
  uint32_t waitStart = millis();
  while (!Serial && (millis() - waitStart) < 2000) { }

  char banner[96];
  int n = snprintf(banner, sizeof(banner),
                   "# SSRP-FSR v1 rate=%dHz discard=%d oversample=%d\n",
                   SAMPLE_RATE_HZ, DISCARD_FIRST_READ, OVERSAMPLE);
  Serial.write((const uint8_t *)banner, n);
  Serial.write("# columns: t_us,p1_up,p1_down,p2_up,p2_down\n");

  nextSampleUs = micros();
}

void loop()
{
  uint32_t now = micros();

  // Unsigned subtraction is overflow-safe: true once 'now' has reached nextSampleUs.
  if ((int32_t)(now - nextSampleUs) < 0)
  {
    return;
  }

  nextSampleUs += PERIOD_US;

  // If we are more than one full period late (e.g. the serial buffer was full), resynchronise
  // instead of sending a burst of catch-up samples, and count it.
  if ((int32_t)(now - nextSampleUs) > (int32_t)PERIOD_US)
  {
    nextSampleUs = now + PERIOD_US;
    overrunCount++;
  }

  uint16_t v0 = readChannel(CHANNEL_PINS[0]);
  uint16_t v1 = readChannel(CHANNEL_PINS[1]);
  uint16_t v2 = readChannel(CHANNEL_PINS[2]);
  uint16_t v3 = readChannel(CHANNEL_PINS[3]);

  char line[48];
  int len = snprintf(line, sizeof(line), "%lu,%u,%u,%u,%u\n",
                     (unsigned long)now, v0, v1, v2, v3);

  if (len > 0 && len < (int)sizeof(line))
  {
    Serial.write((const uint8_t *)line, len);
  }

  // Report overruns at most once per second as a comment line (ignored by the parser).
  static uint32_t lastReportMs = 0;
  static uint32_t lastReportedOverruns = 0;
  if (overrunCount != lastReportedOverruns && (millis() - lastReportMs) > 1000)
  {
    char msg[48];
    int m = snprintf(msg, sizeof(msg), "# overruns=%lu\n", (unsigned long)overrunCount);
    Serial.write((const uint8_t *)msg, m);
    lastReportedOverruns = overrunCount;
    lastReportMs = millis();
  }
}
