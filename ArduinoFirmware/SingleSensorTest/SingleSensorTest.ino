// ============================================================
//  Diagnostic sketch — ONE VL53L0X, plain wiring, no address juggling
// ============================================================
// For testing range/timeout in isolation before wiring all 4 sensors in
// GestureSensors.ino. With only one sensor on the bus there's no address
// conflict, so VIN goes straight to the shared power rail like any normal
// module — no per-sensor switch pin needed here.
//
// Wiring: VIN->5V, GND->GND, SCL->A5, SDA->A4.
// Library: Pololu VL53L0X (same as GestureSensors.ino).
//
// Prints one line per reading, ~20 Hz: either the distance in millimetres,
// or "TIMEOUT" if the sensor didn't answer in time (SENSOR_TIMEOUT_MS).

#include <Wire.h>
#include <VL53L0X.h>

// Same values as GestureSensors.ino — see the comments there for why.
const uint32_t MEASUREMENT_BUDGET_US = 66000;
const uint16_t SENSOR_TIMEOUT_MS = 150;
const int NO_TARGET_MM = 2000;

VL53L0X sensor;

void setup()
{
  Serial.begin(115200);
  Wire.begin();

  sensor.setMeasurementTimingBudget(MEASUREMENT_BUDGET_US);
  sensor.setTimeout(SENSOR_TIMEOUT_MS);
  if (!sensor.init())
  {
    Serial.println("ERROR: sensor init failed - check wiring");
    while (true) {}
  }

  Serial.println("Sensor ready.");
}

void loop()
{
  uint16_t mm = sensor.readRangeSingleMillimeters();

  if (sensor.timeoutOccurred())
    Serial.println("TIMEOUT");
  else if (mm >= NO_TARGET_MM)
    Serial.println("NO_TARGET");
  else
    Serial.println(mm);

  delay(50);
}
