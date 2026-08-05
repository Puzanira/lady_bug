// ============================================================
//  LadyBugHitTheRoad — combined board: joystick + 2 hand sensors
// ============================================================
// One Arduino carrying BOTH player 2's analog joystick (see
// ArduinoFirmware/Joystick) AND player 1's pair of hand-distance sensors
// (one player's worth — 2 sensors, see ArduinoFirmware/GestureSensors) —
// for a setup with many controllers on one machine's start screen, where
// this single board stands in for what would otherwise be two separate
// ones. Identifies itself as a plain "BOARD,JOYSTICK" (not a new/different
// board type) so it's recognized the same way a dedicated joystick board
// already is; sensor + joystick data go out on one combined line (G first,
// then J — see loop() below).
//
// Wiring:
//   Joystick module:  GND->GND   +5V->5V   VRx->A0   VRy->A1   SW->(unused)
//   2x VL53L0X:        GND->GND  shared    SCL->A5   SDA->A4  (both sensors,
//     same bus — VIN does NOT go to a shared rail, each sensor's VIN goes to
//     its own digital pin instead, see below for why)
//     D2 -> left-hand sensor VIN     D3 -> right-hand sensor VIN
//
// These VL53L0X breakout boards don't expose an XSHUT pin on the header
// (only VIN/GND/SCL/SDA), which is normally how you'd disable a sensor to
// give it a unique I2C address (they all default to the same one, 0x29).
// Powering a sensor off achieves the exact same thing as holding XSHUT low —
// a sensor with no power can't respond on the bus — so this brings each one
// up one at a time by switching its VIN pin instead, no soldering onto the
// board required (~20mA per sensor, well within what an ATmega328P pin can
// source). Once addressed, both VIN pins stay HIGH for the rest of the
// sketch. Library: Pololu VL53L0X (github.com/pololu/vl53l0x-arduino) — same
// as GestureSensors.ino, NOT the heavier Adafruit one.
//
// Output: one line per poll, ~15 Hz (a VL53L0X reading takes real
// measurement time, so this board's own full sweep is slower than the
// joystick-only board's 33 Hz — see MEASUREMENT_BUDGET_US):
//   G,<left_mm>,<right_mm>,J,<up>,<down>,<left>,<right>
// G block matches GestureSensorSerial's 3-field "G,..." shape; J block
// matches ArduinoFirmware/Joystick.
// Identity handshake: sending '?' gets a "BOARD,JOYSTICK" reply.

#include <Wire.h>
#include <VL53L0X.h>

// ---- Joystick ----
const uint8_t VRX_PIN = A0;
const uint8_t VRY_PIN = A1;

const int ANALOG_MAX = 1023;
// Deadzone around center (~511) — anything between these two counts as
// "not pushed" on that axis. Wide on purpose: cheap potentiometers drift
// a fair bit at idle, and a false direction reading is far more annoying
// than a slightly-late real one. If a direction comes out backwards on
// your actual board, swap that axis's LOW_THRESHOLD/HIGH_THRESHOLD
// comparison below rather than re-wiring anything.
const int LOW_THRESHOLD = ANALOG_MAX * 3 / 10;  // ~307
const int HIGH_THRESHOLD = ANALOG_MAX * 7 / 10; // ~716

// ---- Hand sensors ----
const uint8_t SENSOR_COUNT = 2;
const uint8_t VIN_PINS[SENSOR_COUNT] = { 2, 3 }; // each sensor's power switch, not a shared rail
const uint8_t SENSOR_ADDR[SENSOR_COUNT] = { 0x30, 0x31 };

// Same values GestureSensors.ino uses — see the comments there for why:
// longer measurement budget = better range/reliability, shorter = faster
// updates; this board reads 2 sensors back-to-back every loop, so a full
// sweep takes roughly 2x this value in the worst case.
const uint32_t MEASUREMENT_BUDGET_US = 66000;
const uint16_t SENSOR_TIMEOUT_MS = 150;
const int NO_TARGET_MM = 2000; // VL53L0X reports ~8190 with no close target; map to -1

VL53L0X sensors[SENSOR_COUNT];

const unsigned long POLL_INTERVAL_MS = 30; // request rate — actual cadence is however long a sweep takes, see above

unsigned long lastPoll = 0;

void setup()
{
  Serial.begin(115200);
  Serial.println("HELLO!");
  Serial.println("BOARD,JOYSTICK");

  // Bring sensors up one at a time, each getting its own unique address
  // before the next one is powered on — see the class comment above.
  for (uint8_t i = 0; i < SENSOR_COUNT; i++)
  {
    pinMode(VIN_PINS[i], OUTPUT);
    digitalWrite(VIN_PINS[i], LOW);
  }
  delay(50);

  Wire.begin();

  for (uint8_t i = 0; i < SENSOR_COUNT; i++)
  {
    digitalWrite(VIN_PINS[i], HIGH);
    delay(50);

    sensors[i].setTimeout(SENSOR_TIMEOUT_MS);
    if (sensors[i].init())
    {
      sensors[i].setAddress(SENSOR_ADDR[i]);
      sensors[i].setMeasurementTimingBudget(MEASUREMENT_BUDGET_US);
    }
    // If init() fails (sensor missing/miswired), it's left unaddressed —
    // readRangeSingleMillimeters() below will just keep timing out for it,
    // reported the same way an out-of-range reading is (-1), rather than
    // this sketch ever getting stuck waiting on hardware that isn't there.
  }
}

void read_identity_request()
{
  while (Serial.available() > 0)
  {
    if (Serial.read() == '?') Serial.println("BOARD,JOYSTICK");
  }
}

void loop()
{
  read_identity_request();

  if (millis() - lastPoll < POLL_INTERVAL_MS) return;
  lastPoll = millis();

  // --- Joystick ---
  int x = analogRead(VRX_PIN);
  int y = analogRead(VRY_PIN);

  int up = y >= HIGH_THRESHOLD ? 1 : 0;
  int down = y <= LOW_THRESHOLD ? 1 : 0;
  int left = x <= LOW_THRESHOLD ? 1 : 0;
  int right = x >= HIGH_THRESHOLD ? 1 : 0;

  int16_t leftMm = read_sensor_mm(0);
  int16_t rightMm = read_sensor_mm(1);

  Serial.print("G,");
  Serial.print(leftMm);
  Serial.print(",");
  Serial.print(rightMm);
  Serial.print(",J,");
  Serial.print(up);
  Serial.print(",");
  Serial.print(down);
  Serial.print(",");
  Serial.print(left);
  Serial.print(",");
  Serial.println(right);
}

// -1 for "no valid target" (out of range/timeout) — same convention
// GestureSensors.ino/SingleSensorTest.ino use.
int16_t read_sensor_mm(uint8_t index)
{
  uint16_t mm = sensors[index].readRangeSingleMillimeters();
  if (sensors[index].timeoutOccurred() || mm >= NO_TARGET_MM)
    return -1;
  return (int16_t)mm;
}
