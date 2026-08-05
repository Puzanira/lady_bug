// ============================================================
//  LadyBugHitTheRoad — gesture sensors (player 1 hands)
// ============================================================
// 2x VL53L0X (one over each hand).
//
// Wiring, distance sensors: GND, SCL and SDA are shared by both
// (SCL->A5, SDA->A4). VIN is NOT shared and does NOT go to a power rail —
// each sensor's VIN goes to its own digital pin instead:
//   D2 -> left hand VIN    D3 -> right hand VIN
// These breakout boards don't expose an XSHUT pin on the header (only
// VIN/GND/SCL/SDA), which is normally how you'd disable a sensor to give it
// a unique I2C address (they all default to the same one, 0x29). Powering
// a sensor off achieves the exact same thing as holding XSHUT low — a
// sensor with no power can't respond on the bus — so this code brings each
// one up one at a time by switching its VIN pin instead, no soldering onto
// the board required. (~20mA per sensor, well within what an ATmega328P
// pin can source.) Once addressed, every sensor's VIN pin stays HIGH for
// the rest of the sketch — cutting power again would lose the assigned
// address, same as re-toggling XSHUT would.
// Library: Pololu VL53L0X (github.com/pololu/vl53l0x-arduino) — NOT the
// Adafruit one used by ArduinoFiles4WorkShop/ArduinoSource/Sensors_1: with
// several sensors alive at once the Adafruit driver's per-object footprint
// blows past the ATmega328P's 2KB RAM ("data section exceeds available
// space in board"); Pololu's is a much lighter driver and was written with
// exactly this multi-sensor pattern in mind.
//
// Output: one CSV line per full sweep, ~33 Hz:
//   G,<left_mm>,<right_mm>
// -1 for a distance channel means "no valid target" (out of range/timeout),
// same convention ArduinoFiles4WorkShop/ArduinoSource/Sensors_1 uses.
// Identity handshake matches the workshop sketches too: sending '?' gets a
// "BOARD,GESTURE_SENSORS" reply, used by
// UnityProject/Assets/Scripts/GestureSensorSerial.cs to find this board
// among other serial devices.

#include <Wire.h>
#include <VL53L0X.h>

const uint8_t SENSOR_COUNT = 2;
const uint8_t VIN_PINS[SENSOR_COUNT] = { 2, 3 }; // each sensor's power switch, not a shared rail
const uint8_t SENSOR_ADDR[SENSOR_COUNT] = { 0x30, 0x31 };
const char *SENSOR_NAMES[SENSOR_COUNT] = { "LEFT", "RIGHT" };

const unsigned long POLL_INTERVAL_MS = 30; // ~33 Hz, best case (see budget below)

// How long each sensor spends actively measuring, every single reading —
// longer = better range/reliability, shorter = faster updates. This board
// reads 2 sensors back-to-back every loop, so a full sweep takes roughly
// 2x this value in the worst case (both sensors, every reading) — with
// 66ms that's ~130ms (~7.7 sweeps/sec), which still leaves a handful of
// samples inside the 1.2s flap-detection window (GestureInput.FlapTracker).
// Default (unset) is ~33ms, which was enough range for close objects but
// too short for farther ones. If range still isn't enough, raise this —
// but every sensor's read (even ones with no target) always takes this
// long, so a higher number means slower updates for everyone, not just the
// far one.
const uint32_t MEASUREMENT_BUDGET_US = 66000;

// Safety-net ceiling for a single I2C read, in case of an actual
// communication problem — NOT the normal "no target" path anymore (that's
// governed by MEASUREMENT_BUDGET_US above, which always completes in
// bounded time). Set comfortably above the budget so it never fires during
// ordinary use.
const uint16_t SENSOR_TIMEOUT_MS = 150;

const int NO_TARGET_MM = 2000; // readings past this aren't a real hand distance

VL53L0X sensors[SENSOR_COUNT];
bool sensorReady[SENSOR_COUNT];
unsigned long lastPoll = 0;

void setup()
{
  Serial.begin(115200);
  Wire.begin();
  Serial.println("HELLO!");
  Serial.println("BOARD,GESTURE_SENSORS");

  // Keep every sensor powered off first, so bringing one up at a time
  // doesn't race against the others still sitting at the default 0x29
  // address.
  for (uint8_t i = 0; i < SENSOR_COUNT; i++)
  {
    pinMode(VIN_PINS[i], OUTPUT);
    digitalWrite(VIN_PINS[i], LOW);
  }
  delay(10);

  for (uint8_t i = 0; i < SENSOR_COUNT; i++)
  {
    digitalWrite(VIN_PINS[i], HIGH); // power this one on
    delay(10); // let it boot before talking to it

    sensorReady[i] = sensors[i].init();
    if (sensorReady[i])
    {
      sensors[i].setAddress(SENSOR_ADDR[i]);
      sensors[i].setMeasurementTimingBudget(MEASUREMENT_BUDGET_US);
      sensors[i].setTimeout(SENSOR_TIMEOUT_MS);
    }
    else
    {
      Serial.print(SENSOR_NAMES[i]);
      Serial.println(",ERROR,init_failed");
    }
  }
}

void read_identity_request()
{
  while (Serial.available() > 0)
  {
    if (Serial.read() == '?') Serial.println("BOARD,GESTURE_SENSORS");
  }
}

int read_sensor_mm(uint8_t index)
{
  if (!sensorReady[index]) return -1;

  uint16_t mm = sensors[index].readRangeSingleMillimeters();
  if (sensors[index].timeoutOccurred() || mm >= NO_TARGET_MM) return -1;
  return (int)mm;
}

void loop()
{
  read_identity_request();

  if (millis() - lastPoll < POLL_INTERVAL_MS) return;
  lastPoll = millis();

  Serial.print("G,");
  Serial.print(read_sensor_mm(0)); // left
  Serial.print(",");
  Serial.println(read_sensor_mm(1)); // right
}
