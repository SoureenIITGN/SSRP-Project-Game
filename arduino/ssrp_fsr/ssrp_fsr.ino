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
 * The firmware sends ADC counts (0-1023). The optional anti-alias filter is
 * disabled by default so the noise can be studied directly.
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
 *   ANTI_ALIAS_ENABLED  1 = enable the cascaded digital low-pass filter. 0 = off.
 *   ANTI_ALIAS_ORDER    Number of cascaded one-pole stages (the filter order). Use 1-4.
 *   ANTI_ALIAS_CUTOFF_HZ -3 dB cutoff in Hz for each one-pole stage. Keep it below SAMPLE_RATE_HZ / 2.
 *
 * The digital filter is useful before a later software downsampling step. It cannot undo aliasing
 * that has already occurred when the Arduino ADC sampled the analog signal; use an analog RC
 * low-pass before the ADC when that protection is required.
 *
 * Timing: a fixed-rate loop based on micros() with unsigned subtraction, so it keeps working
 * across the 70-minute micros() overflow. No delay() is used. Each line is formatted with
 * snprintf into one buffer and sent with a single Serial.write().
 * Budget: 4 channels x 2 reads x ~112 us = ~0.9 ms of ADC time per 5 ms period.
 */

#define SAMPLE_RATE_HZ      200
#define DISCARD_FIRST_READ  1
#define OVERSAMPLE          1
#define ANTI_ALIAS_ENABLED  0
#define ANTI_ALIAS_ORDER    2
#define ANTI_ALIAS_CUTOFF_HZ 20
#define BAUD_RATE           115200

static const uint8_t  CHANNEL_PINS[4] = { A0, A1, A2, A3 };
static const uint32_t PERIOD_US       = 1000000UL / SAMPLE_RATE_HZ;

static uint32_t nextSampleUs = 0;
static uint32_t overrunCount = 0;   // how often the loop fell behind (reported in a comment line)

#if ANTI_ALIAS_ENABLED && (ANTI_ALIAS_ORDER < 1 || ANTI_ALIAS_ORDER > 4)
#error "ANTI_ALIAS_ORDER must be between 1 and 4"
#endif

#if ANTI_ALIAS_ENABLED && (ANTI_ALIAS_CUTOFF_HZ <= 0 || (ANTI_ALIAS_CUTOFF_HZ * 2) >= SAMPLE_RATE_HZ)
#error "ANTI_ALIAS_CUTOFF_HZ must be positive and below SAMPLE_RATE_HZ / 2"
#endif

#if ANTI_ALIAS_ENABLED
static float antiAliasState[4][ANTI_ALIAS_ORDER];
static float antiAliasAlpha;
#endif

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

#if ANTI_ALIAS_ENABLED
static uint16_t applyAntiAlias(uint8_t channel, uint16_t input)
{
  float value = (float)input;

  for (uint8_t stage = 0; stage < ANTI_ALIAS_ORDER; stage++)
  {
    float filtered = antiAliasState[channel][stage];
    filtered += antiAliasAlpha * (value - filtered);
    antiAliasState[channel][stage] = filtered;
    value = filtered;
  }

  if (value <= 0.0f)
    return 0;

  if (value >= 1023.0f)
    return 1023;

  return (uint16_t)(value + 0.5f);
}
#endif

void setup()
{
  Serial.begin(BAUD_RATE);

  // Leonardo-style boards wait for the USB port; harmless on the Uno.
  uint32_t waitStart = millis();
  while (!Serial && (millis() - waitStart) < 2000) { }

#if ANTI_ALIAS_ENABLED
  // Exact discretisation of a one-pole low-pass at the configured sample rate.
  antiAliasAlpha = 1.0f - exp(-2.0f * PI * ANTI_ALIAS_CUTOFF_HZ / SAMPLE_RATE_HZ);

  for (uint8_t channel = 0; channel < 4; channel++)
  {
    for (uint8_t stage = 0; stage < ANTI_ALIAS_ORDER; stage++)
      antiAliasState[channel][stage] = 0.0f;
  }
#endif

  char banner[128];
  int n = snprintf(banner, sizeof(banner),
                   "# SSRP-FSR v1 rate=%dHz discard=%d oversample=%d aa=%d order=%d cutoff=%dHz\n",
                   SAMPLE_RATE_HZ, DISCARD_FIRST_READ, OVERSAMPLE,
                   ANTI_ALIAS_ENABLED, ANTI_ALIAS_ORDER, ANTI_ALIAS_CUTOFF_HZ);
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

#if ANTI_ALIAS_ENABLED
  v0 = applyAntiAlias(0, v0);
  v1 = applyAntiAlias(1, v1);
  v2 = applyAntiAlias(2, v2);
  v3 = applyAntiAlias(3, v3);
#endif

  char line[48];
  int len = snprintf(line, sizeof(line), "%lu,%u,%u,%u,%u\n",(unsigned long)now, v0, v1, v2, v3);

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
